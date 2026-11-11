namespace FolderDock.Core;

public sealed class PinnedFolderService
{
    private readonly IFolderShortcutStore _shortcuts;

    public PinnedFolderService(IFolderShortcutStore shortcuts)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        _shortcuts = shortcuts;
    }

    public IReadOnlyList<FolderShortcut> GetAvailable() =>
        _shortcuts.ReadAll().Where(shortcut => _shortcuts.FolderExists(shortcut.FolderPath)).ToArray();

    public FolderShortcut Create(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        return _shortcuts.Create(folderPath);
    }
}
