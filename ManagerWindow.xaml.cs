using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FolderDock;

public sealed partial class ManagerWindow : Window
{
    private const int DefaultWidth = 720;
    private const int DefaultHeight = 560;

    private readonly Settings _settings = Settings.Load();
    private ExplorerBrowserHost? _explorer;
    private bool _updateCheckRunning;

    private static string ShortcutsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolderDock", "Shortcuts");

    public ManagerWindow()
    {
        InitializeComponent();
        WindowAumid.Apply(WindowNative.GetWindowHandle(this), WindowAumid.AppId);
        SetAppIcon();
        ApplyAppInfo();
        ApplySettings();
        // Встроенный вид Проводника создаём после первой компоновки:
        // нужны фактические размеры плейсхолдера
        ExplorerHostArea.Loaded += OnExplorerAreaLoaded;
        ExplorerHostArea.SizeChanged += (_, _) => UpdateExplorerBounds();
        Closed += (_, _) => { _explorer?.Dispose(); _explorer = null; };
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));

        if (_settings.AutoCheckUpdates)
            _ = CheckForUpdatesAsync(silentWhenUpToDate: true);
    }

    private void OnExplorerAreaLoaded(object sender, RoutedEventArgs e)
    {
        if (_explorer is not null) return;
        try
        {
            // Вид Проводника показывает папку ярлыков напрямую: переименование,
            // удаление, ПКМ «Закрепить на панели задач», drag&drop — всё родное.
            // Изменения на диске шелл подхватывает сам (change notifications).
            Directory.CreateDirectory(ShortcutsDir);
            var host = new ExplorerBrowserHost();
            host.Initialize(WindowNative.GetWindowHandle(this), ExplorerAreaRect(),
                ShortcutsDir, Loc.Get("Manager_EmptyText"));
            _explorer = host;
        }
        catch
        {
            // Без встроенного Проводника окно всё ещё полезно:
            // добавление папок и обновления работают
        }
    }

    private void UpdateExplorerBounds() => _explorer?.SetBounds(ExplorerAreaRect());

    /// Прямоугольник плейсхолдера в физических пикселях клиентской области окна.
    private RECT ExplorerAreaRect()
    {
        var scale = ExplorerHostArea.XamlRoot?.RasterizationScale ?? 1.0;
        var pos = ExplorerHostArea.TransformToVisual(null)
            .TransformPoint(new Windows.Foundation.Point(0, 0));
        return new RECT
        {
            Left = (int)Math.Round(pos.X * scale),
            Top = (int)Math.Round(pos.Y * scale),
            Right = (int)Math.Round((pos.X + ExplorerHostArea.ActualWidth) * scale),
            Bottom = (int)Math.Round((pos.Y + ExplorerHostArea.ActualHeight) * scale)
        };
    }

    private void ApplySettings()
    {
        // Checked/Unchecked подписаны в XAML и сработают на этой установке,
        // но запишут то же самое значение — безвредно
        AutoUpdateCheck.IsChecked = _settings.AutoCheckUpdates;
    }

    private void OnAutoUpdateToggled(object sender, RoutedEventArgs e)
    {
        var isOn = AutoUpdateCheck.IsChecked == true;
        if (_settings.AutoCheckUpdates == isOn) return;
        _settings.AutoCheckUpdates = isOn;
        _settings.Save();
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(silentWhenUpToDate: false);

    private async Task CheckForUpdatesAsync(bool silentWhenUpToDate)
    {
        if (_updateCheckRunning) return;
        _updateCheckRunning = true;
        CheckUpdatesButton.IsEnabled = false;
        ShowUpdateStatus(Loc.Get("Update_Checking"));
        try
        {
            var result = await UpdateService.CheckAsync();

            if (result.Error is not null)
            {
                ShowUpdateStatus(result.Error);
                if (!silentWhenUpToDate)
                    await ShowDialogAsync(Loc.Get("Update_CheckTitle"), result.Error);
                return;
            }

            if (!result.UpdateAvailable)
            {
                ShowUpdateStatus(Loc.Format("Update_UpToDateStatus", AppInfo.Version));
                if (!silentWhenUpToDate)
                    await ShowDialogAsync(Loc.Get("Update_CheckTitle"),
                        Loc.Format("Update_UpToDateDialog", AppInfo.Version));
                return;
            }

            ShowUpdateStatus(Loc.Format("Update_AvailableStatus", result.LatestVersion!));
            await OfferUpdateAsync(result);
        }
        finally
        {
            _updateCheckRunning = false;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async Task OfferUpdateAsync(UpdateCheckResult update)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.Get("Update_DialogTitle"),
            Content = Loc.Format("Update_DialogBody", update.LatestVersion!, AppInfo.Version),
            PrimaryButtonText = Loc.Get("Update_Now"),
            CloseButtonText = Loc.Get("Update_Later"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ShowUpdateStatus(Loc.Format("Update_Downloading", update.LatestVersion!));
        try
        {
            await UpdateService.DownloadAndInstallAsync(update);
            // Установщик убьёт процесс сам (taskkill в PrepareToInstall)
            ShowUpdateStatus(Loc.Get("Update_Installing"));
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Loc.Get("Update_DownloadFailed"));
            await ShowDialogAsync(Loc.Get("Update_ErrorTitle"), ex.Message);
        }
    }

    private void ShowUpdateStatus(string text)
    {
        UpdateStatusText.Text = text;
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void ApplyAppInfo()
    {
        // Заголовок: имя + версия (из трёх пунктов футера — только версия)
        Title = $"FolderDock {AppInfo.Version}";

        VersionText.Text = $"v{AppInfo.Version}";
        var stamp = AppInfo.BuildStampText;
        if (stamp.Length > 0)
        {
            BuildStampText.Text = Loc.Format("Build_Label", stamp);
        }
        else
        {
            // Локальная сборка без метаданных — не показываем пустой сегмент
            BuildStampText.Visibility = Visibility.Collapsed;
            BuildStampSeparator.Visibility = Visibility.Collapsed;
        }
        ToolTipService.SetToolTip(RepositoryLink, AppInfo.RepositoryUrl);
        RepositoryLink.Click += (_, _) => Shell.Open(AppInfo.RepositoryUrl);
    }

    private void SetAppIcon()
    {
        try
        {
            // Иконка заголовка/панели задач: .ico лежит рядом с exe (Content copy)
            var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "FolderDock.ico");
            if (File.Exists(ico)) AppWindow.SetIcon(ico);
        }
        catch
        {
            // Иконка — украшение; окно полноценно работает и без неё
        }
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        try
        {
            // Ярлык появится во встроенном виде Проводника сам —
            // шелл получает уведомление об изменении папки
            ShortcutFactory.CreateFolderShortcut(folder, ShortcutsDir);
            await ShowDialogAsync(Loc.Get("Dialog_ShortcutCreatedTitle"),
                Loc.Get("Dialog_ShortcutCreatedBody"));
        }
        catch (Exception ex)
        {
            await ShowDialogAsync(Loc.Get("Dialog_ErrorTitle"), ex.Message);
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
