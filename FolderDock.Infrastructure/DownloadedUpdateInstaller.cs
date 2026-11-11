using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class DownloadedUpdateInstaller : IUpdateInstaller
{
    private readonly HttpClient _http;
    private readonly IInstallerLauncher _launcher;
    private readonly string _downloadRoot;

    public DownloadedUpdateInstaller(HttpClient http, IInstallerLauncher launcher, string downloadRoot)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadRoot);
        _http = http;
        _launcher = launcher;
        _downloadRoot = Path.GetFullPath(downloadRoot);
    }

    public async Task InstallAsync(ReleaseAsset installer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installer);
        if (!InstallerAssetValidation.IsValid(installer.Name, installer.DownloadUrl))
            throw new ArgumentException("The installer must have a file name and an HTTPS download URL.",
                nameof(installer));
        cancellationToken.ThrowIfCancellationRequested();

        // Each attempt owns its directory, so a failed download cannot overwrite
        // another updater's installer or leave a partial executable for reuse.
        var directory = Path.Combine(_downloadRoot, $"FolderDock-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, installer.Name);
        var launched = false;
        try
        {
            await DownloadAsync(installer.DownloadUrl, path, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _launcher.Launch(path);
            launched = true;
        }
        finally
        {
            // A launched installer still needs its file after this method returns.
            if (!launched) TryRemoveDownload(directory);
        }
    }

    private async Task DownloadAsync(string url, string path, CancellationToken cancellationToken)
    {
        await using var source = await _http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, bufferSize: 81920, useAsync: true);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    private static void TryRemoveDownload(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup must not hide the download or launch failure.
            System.Diagnostics.Debug.WriteLine($"FolderDock: update cleanup failed: {exception}");
        }
    }
}
