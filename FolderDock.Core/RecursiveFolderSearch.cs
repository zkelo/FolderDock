using System.Security;

namespace FolderDock.Core;

public sealed record RecursiveSearchEntry(string Name, string FullPath, bool IsDirectory);

public sealed record RecursiveSearchResult(
    IReadOnlyList<RecursiveSearchEntry> Entries,
    bool Failed,
    bool Incomplete,
    bool LimitReached);

public static class RecursiveFolderSearch
{
    private static readonly EnumerationOptions SearchOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    public static RecursiveSearchResult Search(
        string folder, string? query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var search = query?.Trim() ?? "";
        if (search.Length == 0)
            return new RecursiveSearchResult([], false, false, false);

        var matches = new List<RecursiveSearchEntry>();
        var folders = new Stack<(string Path, bool IsRoot)>();
        folders.Push((folder, true));
        var failed = false;
        var incomplete = false;

        while (folders.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Enumerate lazily so cancellation and the result limit also bound work
                // in very large individual directories.
                foreach (var entry in new DirectoryInfo(current.Path)
                             .EnumerateFileSystemInfos("*", SearchOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes;
                    try
                    {
                        attributes = entry.Attributes;
                    }
                    catch (Exception exception) when (IsFileSystemError(exception))
                    {
                        incomplete = true;
                        continue;
                    }

                    if ((attributes & FileAttributes.Hidden) != 0)
                        continue;

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    if (entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                    {
                        matches.Add(new RecursiveSearchEntry(entry.Name, entry.FullName, isDirectory));
                        if (matches.Count == FolderSearch.MaxResults)
                            return Result(matches, failed, incomplete, true);
                    }

                    // A matching directory is still searched. Links and junctions are
                    // returned as matches but never followed outside the selected tree.
                    if (isDirectory && (attributes & FileAttributes.ReparsePoint) == 0)
                        folders.Push((entry.FullName, false));
                }
            }
            catch (Exception exception) when (IsFileSystemError(exception))
            {
                if (current.IsRoot)
                    failed = true;
                else
                    incomplete = true;
            }
        }

        return Result(matches, failed, incomplete, false);
    }

    private static RecursiveSearchResult Result(
        List<RecursiveSearchEntry> matches, bool failed, bool incomplete, bool limitReached)
    {
        matches.Sort((left, right) =>
        {
            var comparison = right.IsDirectory.CompareTo(left.IsDirectory);
            if (comparison == 0)
                comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            if (comparison == 0)
                comparison = StringComparer.Ordinal.Compare(left.FullPath, right.FullPath);
            return comparison;
        });

        return new RecursiveSearchResult(matches.ToArray(), failed, incomplete, limitReached);
    }

    private static bool IsFileSystemError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or SecurityException;
}
