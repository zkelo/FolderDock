namespace FolderDock.Core;

public sealed class FolderShortcutException : Exception
{
    public FolderShortcutException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
