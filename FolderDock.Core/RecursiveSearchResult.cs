namespace FolderDock.Core;

public sealed record RecursiveSearchResult(
    IReadOnlyList<FolderSnapshotEntry> Entries,
    bool Failed,
    bool Incomplete,
    bool LimitReached);
