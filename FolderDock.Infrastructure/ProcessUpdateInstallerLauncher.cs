using System.Diagnostics;

namespace FolderDock.Infrastructure;

public sealed class ProcessUpdateInstallerLauncher : IInstallerLauncher
{
    public void Launch(string installerPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /NORESTART",
            UseShellExecute = true
        });
    }
}
