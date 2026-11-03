using System.Security;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class FileSystemFolderSnapshotReader : IFolderSnapshotReader
{
    private static readonly EnumerationOptions ReadOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    public Task<FolderSnapshot> ReadAsync(string folder, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(folder, cancellationToken), cancellationToken);

    private static FolderSnapshot Read(string folder, CancellationToken cancellationToken)
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
            return new FolderSnapshot(entries.ToArray(), Failed: false);
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FolderSnapshot.Unavailable;
        }
    }

    private static bool IsFileSystemError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or SecurityException;
}
