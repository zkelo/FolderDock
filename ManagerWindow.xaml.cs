using System;
using System.Collections.ObjectModel;
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
        ShowUpdateStatus("Проверка обновлений…");
        try
        {
            var result = await UpdateService.CheckAsync();

            if (result.Error is not null)
            {
                ShowUpdateStatus(result.Error);
                if (!silentWhenUpToDate)
                    await ShowDialogAsync("Проверка обновлений", result.Error);
                return;
            }

            if (!result.UpdateAvailable)
            {
                ShowUpdateStatus($"Установлена последняя версия (v{AppInfo.Version})");
                if (!silentWhenUpToDate)
                    await ShowDialogAsync("Проверка обновлений",
                        $"У вас последняя версия — v{AppInfo.Version}.");
                return;
            }

            ShowUpdateStatus($"Доступна версия v{result.LatestVersion}");
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
            Title = "Доступно обновление",
            Content = $"Вышла версия v{update.LatestVersion} (у вас v{AppInfo.Version}).\n\n" +
                      "Скачать и установить? Приложение будет перезапущено.",
            PrimaryButtonText = "Обновить",
            CloseButtonText = "Позже",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ShowUpdateStatus($"Скачивание v{update.LatestVersion}…");
        try
        {
            await UpdateService.DownloadAndInstallAsync(update);
            // Установщик убьёт процесс сам (taskkill в PrepareToInstall)
            ShowUpdateStatus("Установка запущена…");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus("Не удалось скачать обновление");
            await ShowDialogAsync("Ошибка обновления", ex.Message);
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
            BuildStampText.Text = $"Сборка: {stamp}";
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
            Shell.RevealInExplorer(lnk);
            await ShowDialogAsync("Ярлык создан",
                "В открывшемся Проводнике: ПКМ по ярлыку → «Показать дополнительные параметры» → " +
                "«Закрепить на панели задач».\n\n" +
                "После закрепления клик по значку откроет папку каскадом.");
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("Ошибка", ex.Message);
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
}
