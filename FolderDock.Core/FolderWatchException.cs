namespace FolderDock.Core;

public sealed class FolderWatchException : Exception
{
    public FolderWatchException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
