namespace FolderDock.Core;

public sealed record FolderSnapshotEntry(string Name, string FullPath, bool IsDirectory);

public sealed record FolderSnapshot(IReadOnlyList<FolderSnapshotEntry> Entries, bool Failed)
{
    public static FolderSnapshot Empty { get; } = new(Array.Empty<FolderSnapshotEntry>(), Failed: false);
    public static FolderSnapshot Unavailable { get; } = new(Array.Empty<FolderSnapshotEntry>(), Failed: true);
}
