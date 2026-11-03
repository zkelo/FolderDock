namespace FolderDock.Core;

public sealed record RecursiveSearchResult(
    IReadOnlyList<RecursiveSearchEntry> Entries,
    bool Failed,
    bool Incomplete,
    bool LimitReached);
