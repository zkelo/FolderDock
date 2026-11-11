namespace FolderDock.Core;

public interface IUpdateInstaller
{
    Task InstallAsync(ReleaseAsset installer, CancellationToken cancellationToken = default);
}
