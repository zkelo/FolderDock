using System;
using System.Threading;
using System.Threading.Tasks;
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
        ApplyTaskbarIdentity(CommandLine.FolderFrom(args));

        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (!mainInstance.IsCurrent)
        {
            ForwardActivation(mainInstance);
            return 0;
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread()));
            _ = new App(mainInstance);
        });
        return 0;
    }

    private static void ApplyTaskbarIdentity(string? folder)
    {
        if (folder is null) return;
        try
        {
            Native.SetCurrentProcessExplicitAppUserModelID(FolderAumid.For(folder));
        }
        catch { }
    }

    private static void ForwardActivation(AppInstance mainInstance)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var completed = new SemaphoreSlim(0, 1);
        Task.Run(async () =>
        {
            try { await mainInstance.RedirectActivationToAsync(activation); }
            finally { completed.Release(); }
        });
        completed.Wait(RedirectTimeout);
    }
}
