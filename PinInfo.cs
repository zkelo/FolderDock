using System.IO;

namespace FolderDock;

/// Строка списка менеджера: закреплённая папка и её ярлык.
public sealed class PinInfo
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required string Lnk { get; init; }

    public static PinInfo FromShortcut(string lnk, string folder) => new()
    {
        Name = Path.GetFileNameWithoutExtension(lnk),
        Folder = folder,
        Lnk = lnk
    };
}
