namespace FolderDock.Core;

public abstract record LoginStartupResult
{
    private LoginStartupResult() { }

    public sealed record Success(bool IsRegistered) : LoginStartupResult;
    public sealed record Failure(LoginStartupError Error, string Details) : LoginStartupResult;
}
