using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FolderDock;

public partial class App : Application
{
    private readonly ActivationQueue _activations;
    private readonly DispatcherQueue _uiQueue;
    private readonly LoginStartupService _loginStartup;
    // One client for the application's lifetime keeps pooled connections reusable.
    private HttpClient? _updateHttp;
    private PopupWindow? _popup;
    private ManagerWindow? _manager;
    private bool _startupConfigurationQueued;

    public App(ActivationQueue activations)
    {
        _activations = activations;
        _loginStartup = new LoginStartupService(
            new WindowsLoginStartupStore(Environment.ProcessPath ?? string.Empty));
        InitializeComponent();
        _uiQueue = DispatcherQueue.GetForCurrentThread();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Route(Environment.CommandLine);
        _activations.Start(arguments => _uiQueue.TryEnqueue(() => Route(arguments)));
    }

    private void Route(string arguments)
    {
        var request = CommandLine.Parse(arguments);
        if (request.Kind == LaunchKind.Background)
        {
            PreparePopup();
            return;
        }

        var folder = request.FolderPath is { } path ? NormalizeFolderPath(path) : null;
        if (folder is not null)
            TogglePopup(folder);
        else
            ShowManager();

        QueueStartupPreparation();
    }

    private static string? NormalizeFolderPath(string folder)
    {
        try
        {
            // Normalize without touching the filesystem. Invalid command-line
            // paths keep the old manager fallback; missing folders reach the
            // popup's asynchronous read and its ordinary error state.
            return Path.GetFullPath(folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                   or IOException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private void QueueStartupPreparation()
    {
        if (_startupConfigurationQueued) return;
        _startupConfigurationQueued = _uiQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            ConfigureLoginStartup();
            PreparePopup();
        });
    }

    private void ConfigureLoginStartup()
    {
        var result = _loginStartup.EnsureConfigured();
        if (result is LoginStartupResult.Failure failure)
            Debug.WriteLine($"FolderDock: login startup failed ({failure.Error}): {failure.Details}");
        _manager?.ShowStartupResult(result);
    }

    private void PreparePopup()
    {
        if (_popup is not null) return;
        _popup = CreatePopup();
        _popup.PrepareForFirstShow();
    }

    private static PopupWindow CreatePopup() => new(
        new FolderLoadSession(new FileSystemFolderSnapshotReader()),
        new FolderSearchSession(new FileSystemFolderSearch()));

    private void TogglePopup(string folder)
    {
        if (_popup is not null)
        {
            if (_popup.IsOpenFor(folder))
            {
                _popup.Dismiss();
                return;
            }
            if (_popup.WasJustDismissed(folder))
                return;
        }
        _popup ??= CreatePopup();
        _popup.ShowForFolder(folder);
    }

    private void ShowManager()
    {
        if (_manager is null)
        {
            _manager = CreateManager();
            _manager.Closed += (_, _) => _manager = null;
        }
        _manager.Activate();
    }

    private ManagerWindow CreateManager()
    {
        _updateHttp ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderDock");
        var settings = new SettingsService(
            new JsonSettingsStore(Path.Combine(dataDirectory, "settings.json")));
        var pinnedFolders = new PinnedFolderService(
            new WindowsFolderShortcutStore(Path.Combine(dataDirectory, "Shortcuts")));
        var updates = new UpdateService(
            new GitHubReleaseSource(_updateHttp, AppInfo.Version),
            new DownloadedUpdateInstaller(_updateHttp, new ProcessUpdateInstallerLauncher(),
                Path.GetTempPath()),
            AppInfo.Version,
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
        return new ManagerWindow(_loginStartup, settings, pinnedFolders, updates);
    }
}
