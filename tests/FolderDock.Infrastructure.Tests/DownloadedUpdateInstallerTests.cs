using System.Net;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class DownloadedUpdateInstallerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        "FolderDock-installer-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly ReleaseAsset Installer = new("FolderDock-Setup-2.0.0-x64.exe",
        "https://example.test/setup.exe");

    [Fact]
    public async Task LaunchesOnlyAfterCompleteDownloadAndClosesFile()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Response("complete installer")));
        var launcher = new RecordingLauncher(path =>
        {
            using var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var reader = new StreamReader(file);
            Assert.Equal("complete installer", reader.ReadToEnd());
        });

        await new DownloadedUpdateInstaller(http, launcher, _directory).InstallAsync(Installer);

        Assert.NotNull(launcher.Path);
        Assert.Equal(Installer.Name, Path.GetFileName(launcher.Path));
        Assert.True(File.Exists(launcher.Path));
    }

    [Fact]
    public async Task SeparateAttemptsUseDifferentFiles()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Response("installer")));
        var launcher = new RecordingLauncher();
        var updater = new DownloadedUpdateInstaller(http, launcher, _directory);

        await updater.InstallAsync(Installer);
        var first = launcher.Path;
        await updater.InstallAsync(Installer);

        Assert.NotEqual(first, launcher.Path);
        Assert.True(File.Exists(first));
    }

    [Fact]
    public async Task FailedDownloadNeverLaunchesAndRemovesAttempt()
    {
        using var http = new HttpClient(new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var launcher = new RecordingLauncher();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new DownloadedUpdateInstaller(http, launcher, _directory).InstallAsync(Installer));

        Assert.Null(launcher.Path);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public async Task FailedLaunchRemovesDownloadedFile()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Response("installer")));
        var launcher = new RecordingLauncher(_ => throw new InvalidOperationException("Launch rejected"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DownloadedUpdateInstaller(http, launcher, _directory).InstallAsync(Installer));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public async Task CancellationDuringDownloadNeverLaunches()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new StubHandler((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Response("installer");
        }));
        var launcher = new RecordingLauncher();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DownloadedUpdateInstaller(http, launcher, _directory)
                .InstallAsync(Installer, cancellation.Token));

        Assert.Null(launcher.Path);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Theory]
    [InlineData("../setup.exe", "https://example.test/setup.exe")]
    [InlineData("folder\\setup.exe", "https://example.test/setup.exe")]
    [InlineData("C:setup.exe", "https://example.test/setup.exe")]
    [InlineData("setup.exe", "file:///tmp/setup.exe")]
    [InlineData("setup.exe", "http://example.test/setup.exe")]
    public async Task RejectsInvalidAssetBeforeFilesystemOrNetworkWork(string name, string url)
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new Exception("Unexpected request")));
        var launcher = new RecordingLauncher();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DownloadedUpdateInstaller(http, launcher, _directory)
                .InstallAsync(new ReleaseAsset(name, url)));

        Assert.False(Directory.Exists(_directory));
        Assert.Null(launcher.Path);
    }

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content)
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class RecordingLauncher(Action<string>? launch = null) : IInstallerLauncher
    {
        public string? Path { get; private set; }

        public void Launch(string installerPath)
        {
            Path = installerPath;
            launch?.Invoke(installerPath);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request, cancellationToken));
    }
}
