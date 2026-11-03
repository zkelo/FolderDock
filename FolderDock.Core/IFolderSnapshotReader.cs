namespace FolderDock.Core;

public interface IFolderSnapshotReader
{
    /// Reads direct children without blocking the calling thread. Expected
    /// access/path failures return an unavailable snapshot; cancellation throws.
    Task<FolderSnapshot> ReadAsync(string folder, CancellationToken cancellationToken = default);
}
