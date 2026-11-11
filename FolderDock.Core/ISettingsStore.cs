namespace FolderDock.Core;

public interface ISettingsStore
{
    AppSettings Read();
    void Write(AppSettings settings);
}
