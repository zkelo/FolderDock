namespace FolderDock.Core;

public sealed class SettingsStoreException : Exception
{
    public SettingsStoreException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
