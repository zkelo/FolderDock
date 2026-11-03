namespace FolderDock.Core;

/// An expected failure of the login-startup storage boundary.
public sealed class LoginStartupException : Exception
{
    public LoginStartupException(LoginStartupError error, string message)
        : base(message)
    {
        Error = error;
    }

    public LoginStartupError Error { get; }
}
