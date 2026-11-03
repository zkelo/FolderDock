namespace FolderDock.Core;

/// A completed read, identifiable even when a folder is reopened at the same path.
public sealed class FolderLoadCompletion
{
    internal long RequestId { get; }
    public FolderSnapshot Snapshot { get; }

    internal FolderLoadCompletion(long requestId, FolderSnapshot snapshot)
    {
        RequestId = requestId;
        Snapshot = snapshot;
    }
}
