namespace FolderDock.Core;

/// Compares releases and selects an installer without depending on HTTP, JSON or Windows.
public sealed class UpdateService
{
    private const string InstallerPrefix = "FolderDock-Setup-";
    private readonly IReleaseSource _releases;
    private readonly IUpdateInstaller _installer;
    private readonly string _currentVersion;
    private readonly string _architecture;

    public UpdateService(IReleaseSource releases, IUpdateInstaller installer,
        string currentVersion, string architecture)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        _releases = releases;
        _installer = installer;
        _currentVersion = currentVersion;
        _architecture = architecture;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        ReleaseInfo release;
        try
        {
            release = await _releases.GetLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReleaseSourceException exception)
        {
            return new UpdateCheckResult.Failure(exception.Error, exception.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!Version.TryParse(release.Tag.TrimStart('v', 'V'), out var latest))
            return new UpdateCheckResult.Failure(UpdateError.InvalidReleaseVersion, release.Tag);
        if (!Version.TryParse(_currentVersion, out var current))
            return new UpdateCheckResult.Failure(UpdateError.InvalidCurrentVersion, _currentVersion);
        if (latest <= current)
            return new UpdateCheckResult.UpToDate(latest);

        var suffix = $"-{_architecture}.exe";
        var asset = release.Assets.FirstOrDefault(candidate =>
            candidate.Name.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase) &&
            candidate.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        return asset is null
            ? new UpdateCheckResult.Failure(UpdateError.InstallerUnavailable, release.Tag, _architecture)
            : new UpdateCheckResult.Available(latest, asset);
    }

    public Task InstallAsync(UpdateCheckResult.Available update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();
        return _installer.InstallAsync(update.Installer, cancellationToken);
    }
}
