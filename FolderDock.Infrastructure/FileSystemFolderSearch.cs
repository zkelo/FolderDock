using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class FileSystemFolderSearch : IFolderSearch
{
    private static readonly EnumerationOptions SearchOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    public Task<RecursiveSearchResult> SearchAsync(
        string folder, string? query, FolderSortOrder sortOrder = FolderSortOrder.Name,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Search(folder, query, sortOrder, cancellationToken), cancellationToken);

    private static RecursiveSearchResult Search(
        string folder, string? query, FolderSortOrder sortOrder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var search = query?.Trim() ?? "";
        if (search.Length == 0)
            return new RecursiveSearchResult([], false, false, false);

        var matches = new List<FolderSnapshotEntry>();
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
                    catch (Exception exception) when (FileSystemFolderEntry.IsFileSystemError(exception))
                    {
                        incomplete = true;
                        continue;
                    }

                    if ((attributes & FileAttributes.Hidden) != 0)
                        continue;

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    if (entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                    {
                        matches.Add(FileSystemFolderEntry.Read(entry, isDirectory, sortOrder));
                        if (matches.Count == FolderSearch.MaxResults)
                            return Result(matches, sortOrder, failed, incomplete, true);
                    }

                    // A matching directory is still searched. Links and junctions are
                    // returned as matches but never followed outside the selected tree.
                    if (isDirectory && (attributes & FileAttributes.ReparsePoint) == 0)
                        folders.Push((entry.FullName, false));
                }
            }
            catch (Exception exception) when (FileSystemFolderEntry.IsFileSystemError(exception))
            {
                if (current.IsRoot)
                    failed = true;
                else
                    incomplete = true;
            }
        }

        return Result(matches, sortOrder, failed, incomplete, false);
    }

    private static RecursiveSearchResult Result(
        List<FolderSnapshotEntry> matches, FolderSortOrder sortOrder, bool failed, bool incomplete,
        bool limitReached)
    {
        matches.Sort((left, right) => FolderSnapshotEntry.Compare(left, right, sortOrder));
        return new RecursiveSearchResult(matches.ToArray(), failed, incomplete, limitReached);
    }
}
