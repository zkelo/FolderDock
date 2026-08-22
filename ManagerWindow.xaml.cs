using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FolderDock;

public sealed partial class ManagerWindow : Window
{
    private const int DefaultWidth = 720;
    private const int DefaultHeight = 560;

    private readonly ObservableCollection<PinInfo> _pins = new();
    private readonly Settings _settings = Settings.Load();
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
        PinsList.ItemsSource = _pins;
        LoadExistingShortcuts();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));
        Activated += OnActivated;

        if (_settings.AutoCheckUpdates)
            _ = CheckForUpdatesAsync(silentWhenUpToDate: true);
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

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        // Актуализируем список при каждом возврате к окну:
        // ярлыки и папки могли удалить, пока менеджер был в фоне
        if (e.WindowActivationState != WindowActivationState.Deactivated)
            LoadExistingShortcuts();
    }

    private void LoadExistingShortcuts()
    {
        _pins.Clear();
        if (!Directory.Exists(ShortcutsDir)) return;

        foreach (var lnk in Directory.EnumerateFiles(ShortcutsDir, "*.lnk"))
        {
            // Показываем только ярлыки, чья целевая папка существует на диске
            var folder = ShortcutFactory.ReadFolderFromShortcut(lnk);
            if (folder is null || !Directory.Exists(folder)) continue;

            _pins.Add(PinInfo.FromShortcut(lnk, folder));
        }
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        try
        {
            var lnk = ShortcutFactory.CreateFolderShortcut(folder, ShortcutsDir);
            _pins.Add(PinInfo.FromShortcut(lnk, folder));
            // Проводник больше не открываем: закрепление доступно прямо здесь —
            // ПКМ по строке списка → системное меню шелла → «Закрепить на панели задач»
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

    private void OnRevealShortcut(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string lnk)
            Shell.RevealInExplorer(lnk);
    }

    private void OnPinRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is not PinInfo pin) return;

        try
        {
            // Настоящее меню Проводника для .lnk: «Закрепить на панели задач»,
            // переименование, удаление, свойства — без перехода в Проводник
            ShellContextMenu.Show(WindowNative.GetWindowHandle(this), pin.Lnk);
        }
        catch
        {
            // Сломанное shell-расширение или исчезнувший ярлык — окно важнее меню
        }

        // Команда могла удалить/переименовать ярлык; InvokeCommand асинхронен
        // на стороне шелла, но обновиться дёшево
        LoadExistingShortcuts();
    }
}
