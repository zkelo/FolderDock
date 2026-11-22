using System.Collections.Immutable;

namespace FolderDock.Core;

public sealed record AppSettings(bool AutoCheckUpdates = true)
{
    private static readonly ImmutableDictionary<string, FolderSortOrder> EmptyFolderSorts =
        ImmutableDictionary.Create<string, FolderSortOrder>(StringComparer.OrdinalIgnoreCase);

    public ImmutableDictionary<string, FolderSortOrder> FolderSorts { get; private init; } = EmptyFolderSorts;

    public FolderSortOrder GetFolderSort(string folder) =>
        FolderSorts.GetValueOrDefault(Path.TrimEndingDirectorySeparator(folder), FolderSortOrder.Name);

    public AppSettings WithFolderSort(string folder, FolderSortOrder order)
    {
        var key = Path.TrimEndingDirectorySeparator(folder);
        var sorts = order == FolderSortOrder.Name ? FolderSorts.Remove(key) : FolderSorts.SetItem(key, order);
        return this with { FolderSorts = sorts };
    }
}
