using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("x64", "FolderDock-Setup-2.0.0-x64.exe")]
    [InlineData("arm64", "FolderDock-Setup-2.0.0-arm64.exe")]
    public async Task SelectsInstallerForRequestedArchitecture(string architecture, string expectedName)
    {
        var source = new StubReleaseSource(new ReleaseInfo("v2.0.0", [
            Asset("FolderDock-2.0.0-x64.zip"),
            Asset("FolderDock-Setup-2.0.0-arm64.exe"),
            Asset("FolderDock-Setup-2.0.0-x64.exe")
        ]));
        var installer = new RecordingInstaller();
        var service = new UpdateService(source, installer, "1.0.0", architecture);

        var available = Assert.IsType<UpdateCheckResult.Available>(await service.CheckAsync());

        Assert.Equal(new Version(2, 0, 0), available.LatestVersion);
        Assert.Equal(expectedName, available.Installer.Name);
        Assert.Null(installer.Installed);
    }

    [Fact]
    public async Task InstallerNameMatchingIsCaseInsensitive()
    {
        var source = new StubReleaseSource(new ReleaseInfo("V2.0.0", [
            Asset("folderdock-setup-2.0.0-X64.EXE")
        ]));

        var result = await new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
            .CheckAsync();

        Assert.IsType<UpdateCheckResult.Available>(result);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.9.0")]
    public async Task EqualAndOlderReleasesDoNotRequireInstaller(string tag)
    {
        var source = new StubReleaseSource(new ReleaseInfo(tag, []));

        var result = await new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
            .CheckAsync();

        Assert.Equal(new Version(tag), Assert.IsType<UpdateCheckResult.UpToDate>(result).LatestVersion);
    }

    [Fact]
    public async Task MissingArchitectureReportsReleaseAndArchitecture()
    {
        var source = new StubReleaseSource(new ReleaseInfo("v2.0.0", [
            Asset("FolderDock-Setup-2.0.0-arm64.exe")
        ]));

        var result = await new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
            .CheckAsync();

        Assert.Equal(new UpdateCheckResult.Failure(UpdateError.InstallerUnavailable, "v2.0.0", "x64"),
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("release-next")]
    public async Task InvalidReleaseVersionReturnsUnlocalizedFailure(string tag)
    {
        var source = new StubReleaseSource(new ReleaseInfo(tag, []));

        var result = await new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
            .CheckAsync();

        Assert.Equal(new UpdateCheckResult.Failure(UpdateError.InvalidReleaseVersion, tag), result);
    }

    [Fact]
    public async Task InvalidCurrentVersionReturnsUnlocalizedFailure()
    {
        var source = new StubReleaseSource(new ReleaseInfo("v2.0.0", []));

        var result = await new UpdateService(source, new RecordingInstaller(), "development", "x64")
            .CheckAsync();

        Assert.Equal(new UpdateCheckResult.Failure(UpdateError.InvalidCurrentVersion, "development"), result);
    }

    [Fact]
    public async Task AdapterFailureIsReturnedWithItsClassification()
    {
        var source = new StubReleaseSource(new ReleaseSourceException(UpdateError.HttpFailure, "HTTP 429"));

        var result = await new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
            .CheckAsync();

        Assert.Equal(new UpdateCheckResult.Failure(UpdateError.HttpFailure, "HTTP 429"), result);
    }

    [Fact]
    public async Task CheckPreservesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new StubReleaseSource(new ReleaseInfo("v2.0.0", []));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new UpdateService(source, new RecordingInstaller(), "1.0.0", "x64")
                .CheckAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, source.ReceivedToken);
    }

    [Fact]
    public async Task ExplicitInstallPassesSelectedAssetAndCancellationToAdapter()
    {
        using var cancellation = new CancellationTokenSource();
        var installer = new RecordingInstaller();
        var selected = new UpdateCheckResult.Available(new Version(2, 0, 0),
            Asset("FolderDock-Setup-2.0.0-x64.exe"));
        var service = new UpdateService(new StubReleaseSource(new ReleaseInfo("v2.0.0", [])),
            installer, "1.0.0", "x64");

        await service.InstallAsync(selected, cancellation.Token);

        Assert.Same(selected.Installer, installer.Installed);
        Assert.Equal(cancellation.Token, installer.ReceivedToken);
    }

    [Fact]
    public async Task CancelledInstallNeverCallsAdapter()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var installer = new RecordingInstaller();
        var selected = new UpdateCheckResult.Available(new Version(2, 0, 0), Asset("setup.exe"));
        var service = new UpdateService(new StubReleaseSource(new ReleaseInfo("v2.0.0", [])),
            installer, "1.0.0", "x64");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.InstallAsync(selected, cancellation.Token));

        Assert.Null(installer.Installed);
    }

    private static ReleaseAsset Asset(string name) => new(name, $"https://example.test/{name}");

    private sealed class StubReleaseSource : IReleaseSource
    {
        private readonly ReleaseInfo? _release;
        private readonly Exception? _exception;
        public CancellationToken ReceivedToken { get; private set; }

        public StubReleaseSource(ReleaseInfo release) => _release = release;
        public StubReleaseSource(Exception exception) => _exception = exception;

        public Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return _exception is null
                ? Task.FromResult(_release!)
                : Task.FromException<ReleaseInfo>(_exception);
        }
    }

    private sealed class RecordingInstaller : IUpdateInstaller
    {
        public ReleaseAsset? Installed { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task InstallAsync(ReleaseAsset installer, CancellationToken cancellationToken = default)
        {
            Installed = installer;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
