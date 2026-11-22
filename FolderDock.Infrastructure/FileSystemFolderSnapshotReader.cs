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

    public Task<FolderSnapshot> ReadAsync(string folder, FolderSortOrder sortOrder = FolderSortOrder.Name,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(folder, sortOrder, cancellationToken), cancellationToken);

    private static FolderSnapshot Read(string folder, FolderSortOrder sortOrder, CancellationToken cancellationToken)
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
                catch (Exception exception) when (FileSystemFolderEntry.IsFileSystemError(exception))
                {
                    // Keep a disappearing/unreadable item visible, as before.
                    attributes = entry is DirectoryInfo ? FileAttributes.Directory : 0;
                }

                if ((attributes & FileAttributes.Hidden) != 0)
                    continue;

                entries.Add(FileSystemFolderEntry.Read(entry,
                    (attributes & FileAttributes.Directory) != 0, sortOrder));
            }

            cancellationToken.ThrowIfCancellationRequested();
            entries.Sort((left, right) => FolderSnapshotEntry.Compare(left, right, sortOrder));
            cancellationToken.ThrowIfCancellationRequested();
            return new FolderSnapshot(entries.ToArray(), Failed: false);
        }
        catch (Exception exception) when (FileSystemFolderEntry.IsFileSystemError(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FolderSnapshot.Unavailable;
        }
    }
}
