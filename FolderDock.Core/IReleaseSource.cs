namespace FolderDock.Core;

public interface IReleaseSource
{
    Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken = default);
}
