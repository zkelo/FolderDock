using System.Security;

namespace FolderDock.Core;

public sealed record FolderSnapshotEntry(string Name, string FullPath, bool IsDirectory);

public sealed record FolderSnapshot(IReadOnlyList<FolderSnapshotEntry> Entries, bool Failed);

public static class FolderSnapshotReader
{
    private static readonly EnumerationOptions ReadOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    public static FolderSnapshot Read(string folder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var entries = new List<FolderSnapshotEntry>();
            // FileSystemInfo carries the metadata returned by directory enumeration.
            // One pass avoids reopening the directory and querying every path again.
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", ReadOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes;
                try
                {
                    attributes = entry.Attributes;
                }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    // Keep a disappearing/unreadable item visible, as before.
                    attributes = entry is DirectoryInfo ? FileAttributes.Directory : 0;
                }

                if ((attributes & FileAttributes.Hidden) != 0)
                    continue;

                entries.Add(new FolderSnapshotEntry(entry.Name, entry.FullName,
                    (attributes & FileAttributes.Directory) != 0));
            }

            cancellationToken.ThrowIfCancellationRequested();
            entries.Sort((left, right) =>
            {
                var comparison = right.IsDirectory.CompareTo(left.IsDirectory);
                return comparison != 0 ? comparison
                    : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            });
            cancellationToken.ThrowIfCancellationRequested();
            return new FolderSnapshot(entries, Failed: false);
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new FolderSnapshot(Array.Empty<FolderSnapshotEntry>(), Failed: true);
        }
    }

    private static bool IsFileSystemError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or SecurityException;
}
