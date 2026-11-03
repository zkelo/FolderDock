using System.Collections.Generic;
using System.Linq;
using FolderDock.Core;

namespace FolderDock;

/// Результат чтения папки: элементы + признак ошибки доступа.
/// Позволяет UI отличать «папка пуста» от «нет доступа / диск отключён».
public sealed record FolderContents(IReadOnlyList<FolderEntry> Entries, bool Failed)
{
    public static FolderContents FromSnapshot(FolderSnapshot snapshot) => new(
        // Reuse the display limit before allocating view entries on the UI thread.
        // Directory enumeration/sorting remains complete in the reader.
        FolderSearch.Filter(snapshot.Entries, null, entry => entry.Name).Select(entry => new FolderEntry
        {
            Name = entry.Name,
            FullPath = entry.FullPath,
            IsDirectory = entry.IsDirectory
        }).ToArray(), snapshot.Failed);
}
