using System;
using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FolderDock;

public partial class App : Application
{
    private readonly AppInstance _instance;
    private readonly DispatcherQueue _uiQueue;
    private PopupWindow? _popup;
    private ManagerWindow? _manager;

    public App(AppInstance instance)
    {
        _instance = instance;
        InitializeComponent();
        _uiQueue = DispatcherQueue.GetForCurrentThread();
        _instance.Activated += OnRedirectedActivation;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Route(CommandLine.FolderFrom(Environment.CommandLine));
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments e)
    {
        var folder = e.Kind == ExtendedActivationKind.Launch &&
                     e.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch
            ? CommandLine.FolderFrom(launch.Arguments)
            : null;

        _uiQueue.TryEnqueue(() => Route(folder));
    }

    private void Route(string? folder)
    {
        if (folder is not null && Directory.Exists(folder))
            TogglePopup(folder);
        else
            ShowManager();
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
