using System.Linq;
using System.Threading;
using FolderDock.Core;

namespace FolderDock;

public static class FolderReader
{
    public static FolderContents Read(string folder, CancellationToken cancellationToken = default)
    {
        var snapshot = FolderSnapshotReader.Read(folder, cancellationToken);
        var entries = snapshot.Entries.Select(entry =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new FolderEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                IsDirectory = entry.IsDirectory
            };
        }).ToArray();
        return new FolderContents(entries, snapshot.Failed);
    }
}
