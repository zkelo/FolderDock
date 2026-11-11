using FolderDock.Core;

namespace FolderDock;

/// Строка списка менеджера: закреплённая папка и её ярлык.
public sealed class PinInfo
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required string Lnk { get; init; }

    public static PinInfo FromShortcut(FolderShortcut shortcut) => new()
    {
        Name = shortcut.Name,
        Folder = shortcut.FolderPath,
        Lnk = shortcut.ShortcutPath
    };
}
