namespace FolderDock.Core;

public static class FolderSearch
{
    public const int MaxResults = 500;

    public static IReadOnlyList<T> Filter<T>(
        IEnumerable<T> entries, string? query, Func<T, string> getName)
    {
        var search = query?.Trim() ?? "";
        var matches = search.Length == 0
            ? entries
            : entries.Where(entry => getName(entry).Contains(
                search, StringComparison.CurrentCultureIgnoreCase));

        return matches.Take(MaxResults).ToArray();
    }
}
