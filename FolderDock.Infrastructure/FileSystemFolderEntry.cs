using System.Security;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

internal static class FileSystemFolderEntry
{
    public static FolderSnapshotEntry Read(FileSystemInfo entry, bool isDirectory, FolderSortOrder order) =>
        new(entry.Name, entry.FullName, isDirectory, ReadSortTimestamp(entry, order));

    private static DateTime? ReadSortTimestamp(FileSystemInfo entry, FolderSortOrder order)
    {
        try
        {
            return order switch
            {
                FolderSortOrder.DateAdded => entry.CreationTimeUtc,
                FolderSortOrder.DateModified => entry.LastWriteTimeUtc,
                _ => null
            };
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            // A missing timestamp sorts after the known dates without hiding the entry.
            return null;
        }
    }

    public static bool IsFileSystemError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or SecurityException;
}
