namespace FolderDock.Core;

public sealed record FolderSnapshotEntry(
    string Name, string FullPath, bool IsDirectory, DateTime? SortTimestampUtc = null)
{
    public static int Compare(FolderSnapshotEntry left, FolderSnapshotEntry right, FolderSortOrder order)
    {
        var comparison = order switch
        {
            FolderSortOrder.DateAdded or FolderSortOrder.DateModified =>
                Nullable.Compare(right.SortTimestampUtc, left.SortTimestampUtc),
            _ => right.IsDirectory.CompareTo(left.IsDirectory)
        };
        if (comparison != 0) return comparison;

        comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.FullPath, right.FullPath);
    }
}

public sealed record FolderSnapshot(IReadOnlyList<FolderSnapshotEntry> Entries, bool Failed)
{
    public static FolderSnapshot Empty { get; } = new(Array.Empty<FolderSnapshotEntry>(), Failed: false);
    public static FolderSnapshot Unavailable { get; } = new(Array.Empty<FolderSnapshotEntry>(), Failed: true);
}
