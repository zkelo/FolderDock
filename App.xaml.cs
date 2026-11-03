using System;
using System.Diagnostics;
using System.IO;
using FolderDock.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FolderDock;

public partial class App : Application
{
    private readonly ActivationQueue _activations;
    private readonly DispatcherQueue _uiQueue;
    private PopupWindow? _popup;
    private ManagerWindow? _manager;
    private bool _startupConfigurationQueued;

    public App(ActivationQueue activations)
    {
        _activations = activations;
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
        if (CommandLine.IsBackground(arguments))
        {
            PreparePopup();
            return;
        }

        var folder = CommandLine.FolderFrom(arguments);
        try
        {
            // Normalize without touching the filesystem. Invalid command-line
            // paths keep the old manager fallback; missing folders reach the
            // popup's asynchronous read and its ordinary error state.
            if (folder is not null) folder = Path.GetFullPath(folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                   or IOException or System.Security.SecurityException)
        {
            folder = null;
        }
        // The popup reads and validates the path asynchronously. In particular,
        // an offline network path must not block the first visible response.
        if (folder is not null)
            TogglePopup(folder);
        else
            ShowManager();

        if (_startupConfigurationQueued) return;
        _startupConfigurationQueued = true;
        _uiQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            var error = LoginStartup.EnsureConfigured();
            if (error is not null)
                Debug.WriteLine($"FolderDock: login startup registration failed: {error}");
            _manager?.RefreshStartupSetting();
            PreparePopup();
        });
    }

    private void PreparePopup()
    {
        if (_popup is not null) return;
        _popup = new PopupWindow();
        _popup.PrepareForFirstShow();
    }

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
        _popup ??= new PopupWindow();
        _popup.ShowForFolder(folder);
    }

    private void ShowManager()
    {
        if (_manager is null)
        {
            _manager = new ManagerWindow();
            _manager.Closed += (_, _) => _manager = null;
        }
        _manager.Activate();
    }
}
