namespace FolderDock.Core;

/// A release adapter failed to retrieve or decode a release.
public sealed class ReleaseSourceException : Exception
{
    public UpdateError Error { get; }

    public ReleaseSourceException(UpdateError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }
}
