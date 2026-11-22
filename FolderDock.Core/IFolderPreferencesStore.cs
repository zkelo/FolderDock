namespace FolderDock.Core;

public interface IFolderPreferencesStore
{
    FolderPreferences Read(string folder);
    void Write(string folder, FolderPreferences preferences);
}
