using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FolderDock.Infrastructure;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FolderDock;

public static class Program
{
    private const string SingleInstanceKey = "FolderDock-Main";
    private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(10);

    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var launch = CommandLine.Parse(args);
        ApplyTaskbarIdentity(launch.FolderPath);

        // Subscribe before publishing our instance key. A taskbar click can
        // arrive while the login process is still creating its first window.
        var activations = new ActivationQueue();
        var currentInstance = AppInstance.GetCurrent();
        currentInstance.Activated += (_, activation) =>
        {
            var arguments = activation.Kind == ExtendedActivationKind.Launch &&
                            activation.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch
                ? launch.Arguments
                : string.Empty;
            activations.Enqueue(arguments);
        };
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (!mainInstance.IsCurrent)
        {
            // A delayed login launch must not open the manager or dismiss a
            // folder that the user has already opened in the resident process.
            if (launch.Kind != LaunchKind.Background)
                ForwardActivation(mainInstance);
            return 0;
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread()));
            _ = new App(activations);
        });
        return 0;
    }

    private static void ApplyTaskbarIdentity(string? folder)
    {
        try
        {
            // Без --folder (меню Пуск, прямой запуск) процесс — «само приложение»,
            // иначе окна группировались бы под значком последней закреплённой папки
            Native.SetCurrentProcessExplicitAppUserModelID(
                folder is null ? WindowAumid.AppId : FolderAumid.For(folder));
        }
        catch
        {
            // AUMID не установился (экзотическая сборка Windows) — приложение
            // работает, страдает только группировка на панели задач
        }
    }

    private static void ForwardActivation(AppInstance mainInstance)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        // Redirect on a worker so the initial STA thread can wait without a
        // dispatcher. Task owns its completion signal, including after timeout.
        var redirect = Task.Run(async () =>
        {
            try
            {
                await mainInstance.RedirectActivationToAsync(activation);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"FolderDock: activation redirect failed: {exception}");
            }
        });
        if (!redirect.Wait(RedirectTimeout))
            Debug.WriteLine("FolderDock: activation redirect timed out, click is lost");
    }
}
