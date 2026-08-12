using System;
using System.Collections.Generic;

namespace FolderDock;

/// Результат чтения папки: элементы + признак ошибки доступа.
/// Позволяет UI отличать «папка пуста» от «нет доступа / диск отключён».
public sealed record FolderContents(IReadOnlyList<FolderEntry> Entries, bool Failed)
{
    public static readonly FolderContents Error = new(Array.Empty<FolderEntry>(), Failed: true);
}
