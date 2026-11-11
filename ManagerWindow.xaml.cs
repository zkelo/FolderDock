using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FolderDock.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace FolderDock;

public sealed partial class ManagerWindow : Window
{
    private const int DefaultWidth = 720;
    private const int DefaultHeight = 620;

    private readonly ObservableCollection<PinInfo> _pins = new();
    private readonly SettingsService _settings;
    private readonly PinnedFolderService _pinnedFolders;
    private readonly UpdateService _updates;
    private readonly UISettings _uiSettings = new();
    private readonly LoginStartupService _loginStartup;
    private bool _updateCheckRunning;
    private bool _appearanceClosed;
    private bool _refreshingStartup;

    public ManagerWindow(LoginStartupService loginStartup, SettingsService settings,
        PinnedFolderService pinnedFolders, UpdateService updates)
    {
        ArgumentNullException.ThrowIfNull(loginStartup);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pinnedFolders);
        ArgumentNullException.ThrowIfNull(updates);
        _loginStartup = loginStartup;
        _settings = settings;
        _pinnedFolders = pinnedFolders;
        _updates = updates;
        ReportSettingsError(_settings.Load());
        InitializeComponent();
        // Настоящий desktop acrylic: Windows управляет размытием, темой,
        // активацией и непрозрачным фоном при отключённой прозрачности.
        SystemBackdrop = new DesktopAcrylicBackdrop();
        InitializeTitleBar();
        WindowAumid.Apply(WindowNative.GetWindowHandle(this), WindowAumid.AppId);
        SetAppIcon();
        ApplyAppInfo();
        ApplySettings();
        PinsList.ItemsSource = _pins;
        LoadExistingShortcuts();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));
        Activated += OnActivated;

        if (_settings.Current.AutoCheckUpdates)
            _ = CheckForUpdatesAsync(silentWhenUpToDate: true);
    }

    private void InitializeTitleBar()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            TitleBarDragRegion.Visibility = Visibility.Collapsed;
            return;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        ManagerRoot.Loaded += OnTitleBarLoaded;
        ManagerRoot.ActualThemeChanged += OnTitleBarThemeChanged;
        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;
        Closed += OnAppearanceClosed;
        UpdateTitleBarColors();
    }

    private void OnTitleBarLoaded(object sender, RoutedEventArgs e) => UpdateTitleBarColors();

    private void OnTitleBarThemeChanged(FrameworkElement sender, object args) => UpdateTitleBarColors();

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        // Событие Windows приходит с фонового потока, в том числе при смене
        // контрастной темы. Цвета читаем заново только в очереди окна.
        DispatcherQueue.TryEnqueue(UpdateTitleBarColors);
    }

    private void UpdateTitleBarColors()
    {
        if (_appearanceClosed) return;
        var titleBar = AppWindow.TitleBar;
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = _uiSettings.UIElementColor(UIElementType.GrayText);
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        // Hover/pressed backgrounds and the close button retain native behavior.
    }

    private void OnAppearanceClosed(object sender, WindowEventArgs args)
    {
        _appearanceClosed = true;
        _uiSettings.ColorValuesChanged -= OnSystemColorsChanged;
        ManagerRoot.Loaded -= OnTitleBarLoaded;
        ManagerRoot.ActualThemeChanged -= OnTitleBarThemeChanged;
        Closed -= OnAppearanceClosed;
    }

    private void ApplySettings()
    {
        // Checked/Unchecked подписаны в XAML и сработают на этой установке,
        // но запишут то же самое значение — безвредно
        AutoUpdateCheck.IsChecked = _settings.Current.AutoCheckUpdates;
        ToolTipService.SetToolTip(StartAtLoginCheck, Loc.Get("Startup_Help"));
        RefreshStartupSetting();
    }

    private void RefreshStartupSetting() => RenderStartupResult(_loginStartup.GetStatus());

    public void ShowStartupResult(LoginStartupResult result)
    {
        // A failed write can partially succeed. Query the actual registration
        // before showing that operation's error, without storing a global error.
        if (result is LoginStartupResult.Failure)
            RefreshStartupSetting();
        RenderStartupResult(result);
    }

    private void RenderStartupResult(LoginStartupResult result)
    {
        if (result is LoginStartupResult.Success status)
        {
            _refreshingStartup = true;
            try
            {
                StartAtLoginCheck.IsChecked = status.IsRegistered;
            }
            finally
            {
                _refreshingStartup = false;
            }
            StartupStatusText.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(StartupStatusText, null);
        }
        else if (result is LoginStartupResult.Failure failure)
        {
            StartupStatusText.Text = Loc.Get("Startup_StatusFailed");
            StartupStatusText.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(StartupStatusText, failure.Details);
        }
    }

    private async void OnStartAtLoginToggled(object sender, RoutedEventArgs e)
    {
        if (_refreshingStartup) return;
        var enabled = StartAtLoginCheck.IsChecked == true;
        var result = _loginStartup.SetEnabled(enabled);
        ShowStartupResult(result);
        if (result is LoginStartupResult.Failure failure)
            await ShowDialogAsync(Loc.Get("Startup_ErrorTitle"),
                Loc.Format("Startup_ErrorBody", failure.Details));
    }

    private void OnAutoUpdateToggled(object sender, RoutedEventArgs e)
    {
        ReportSettingsError(_settings.SetAutoCheckUpdates(AutoUpdateCheck.IsChecked == true));
    }

    private static void ReportSettingsError(SettingsResult result)
    {
        if (result.Error is not null)
            Trace.WriteLine($"FolderDock: settings persistence failed: {result.Error}");
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
            var result = await _updates.CheckAsync();

            if (result is UpdateCheckResult.Failure failure)
            {
                var message = UpdateCheckMessages.Format(failure);
                ShowUpdateStatus(message);
                if (!silentWhenUpToDate)
                    await ShowDialogAsync(Loc.Get("Update_CheckTitle"), message);
                return;
            }

            if (result is UpdateCheckResult.UpToDate)
            {
                ShowUpdateStatus(Loc.Format("Update_UpToDateStatus", AppInfo.Version));
                if (!silentWhenUpToDate)
                    await ShowDialogAsync(Loc.Get("Update_CheckTitle"),
                        Loc.Format("Update_UpToDateDialog", AppInfo.Version));
                return;
            }

            if (result is UpdateCheckResult.Available available)
            {
                ShowUpdateStatus(Loc.Format("Update_AvailableStatus", available.LatestVersion));
                await OfferUpdateAsync(available);
            }
        }
        finally
        {
            _updateCheckRunning = false;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async Task OfferUpdateAsync(UpdateCheckResult.Available update)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.Get("Update_DialogTitle"),
            Content = Loc.Format("Update_DialogBody", update.LatestVersion, AppInfo.Version),
            PrimaryButtonText = Loc.Get("Update_Now"),
            CloseButtonText = Loc.Get("Update_Later"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ShowUpdateStatus(Loc.Format("Update_Downloading", update.LatestVersion));
        try
        {
            await _updates.InstallAsync(update);
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
        TitleBarText.Opacity = e.WindowActivationState == WindowActivationState.Deactivated ? 0.6 : 1;
        // Актуализируем список при каждом возврате к окну:
        // ярлыки и папки могли удалить, пока менеджер был в фоне
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            RefreshStartupSetting();
            LoadExistingShortcuts();
        }
    }

    private void LoadExistingShortcuts()
    {
        _pins.Clear();
        try
        {
            foreach (var shortcut in _pinnedFolders.GetAvailable())
                _pins.Add(PinInfo.FromShortcut(shortcut));
        }
        catch (FolderShortcutException exception)
        {
            Trace.WriteLine($"FolderDock: shortcuts could not be loaded: {exception}");
        }
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        try
        {
            _pins.Add(PinInfo.FromShortcut(_pinnedFolders.Create(folder)));
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
