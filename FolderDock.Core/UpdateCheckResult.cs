namespace FolderDock.Core;

public abstract record UpdateCheckResult
{
    private UpdateCheckResult() { }

    public sealed record UpToDate(Version LatestVersion) : UpdateCheckResult;
    public sealed record Available(Version LatestVersion, ReleaseAsset Installer) : UpdateCheckResult;
    public sealed record Failure(UpdateError Error, string Details, string? Architecture = null)
        : UpdateCheckResult;
}
