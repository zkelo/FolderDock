namespace FolderDock.Core;

public interface IFolderShortcutStore
{
    IReadOnlyList<FolderShortcut> ReadAll();
    bool FolderExists(string folderPath);
    FolderShortcut Create(string folderPath);
}
