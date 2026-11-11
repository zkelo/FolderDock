using System.Net;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class GitHubReleaseSourceTests
{
    [Fact]
    public async Task ReadsReleaseAndSendsGitHubHeaders()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("https://api.github.com/repos/zkelo/FolderDock/releases/latest",
                request.RequestUri!.AbsoluteUri);
            Assert.Contains(request.Headers.UserAgent, value => value.Product?.Name == "FolderDock");
            Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/vnd.github+json");
            return Json("""
                { "tag_name": "v2.0.0", "assets": [
                  { "name": "FolderDock-Setup-2.0.0-x64.exe",
                    "browser_download_url": "https://example.test/setup.exe" }
                ] }
                """);
        });
        using var http = new HttpClient(handler);

        var release = await new GitHubReleaseSource(http, "1.0.0").GetLatestAsync();

        Assert.Equal("v2.0.0", release.Tag);
        Assert.Equal(new ReleaseAsset("FolderDock-Setup-2.0.0-x64.exe", "https://example.test/setup.exe"),
            Assert.Single(release.Assets));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":42,\"assets\":42}")]
    [InlineData("{\"tag_name\":null,\"assets\":null}")]
    public async Task MissingOrInvalidPropertiesDoNotCrash(string payload)
    {
        using var http = new HttpClient(new StubHandler((_, _) => Json(payload)));

        var release = await new GitHubReleaseSource(http, "1.0.0").GetLatestAsync();

        Assert.Equal(string.Empty, release.Tag);
        Assert.Empty(release.Assets);
    }

    [Fact]
    public async Task MalformedAndUnsafeAssetsAreSkipped()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Json("""
            { "tag_name": "v2.0.0", "assets": [
              null, 42, {},
              { "name": 3, "browser_download_url": "https://example.test/a" },
              { "name": "setup.exe", "browser_download_url": 3 },
              { "name": "../setup.exe", "browser_download_url": "https://example.test/a" },
              { "name": "folder\\setup.exe", "browser_download_url": "https://example.test/a" },
              { "name": "setup.exe", "browser_download_url": "file:///tmp/setup.exe" },
              { "name": "setup.exe", "browser_download_url": "https://example.test/setup.exe" }
            ] }
            """)));

        var release = await new GitHubReleaseSource(http, "1.0.0").GetLatestAsync();

        Assert.Single(release.Assets);
    }

    [Theory]
    [InlineData("invalid JSON")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task InvalidPayloadProducesTypedFailure(string payload)
    {
        using var http = new HttpClient(new StubHandler((_, _) => Json(payload)));

        var exception = await Assert.ThrowsAsync<ReleaseSourceException>(() =>
            new GitHubReleaseSource(http, "1.0.0").GetLatestAsync());

        Assert.Equal(UpdateError.ReleaseUnavailable, exception.Error);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task HttpFailurePreservesStatus()
    {
        using var http = new HttpClient(new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<ReleaseSourceException>(() =>
            new GitHubReleaseSource(http, "1.0.0").GetLatestAsync());

        Assert.Equal(UpdateError.HttpFailure, exception.Error);
        Assert.Equal("GitHub API: HTTP 429", exception.Message);
    }

    [Fact]
    public async Task NetworkFailurePreservesInnerException()
    {
        var failure = new HttpRequestException("Network unavailable");
        using var http = new HttpClient(new StubHandler((_, _) => throw failure));

        var exception = await Assert.ThrowsAsync<ReleaseSourceException>(() =>
            new GitHubReleaseSource(http, "1.0.0").GetLatestAsync());

        Assert.Equal(UpdateError.ReleaseUnavailable, exception.Error);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task TimeoutIsAReleaseFailure()
    {
        using var http = new HttpClient(new StubHandler((_, _) =>
            throw new TaskCanceledException("Timed out")));

        var exception = await Assert.ThrowsAsync<ReleaseSourceException>(() =>
            new GitHubReleaseSource(http, "1.0.0").GetLatestAsync());

        Assert.Equal(UpdateError.ReleaseUnavailable, exception.Error);
    }

    [Fact]
    public async Task CallerCancellationIsNotConvertedToNetworkFailure()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new StubHandler((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Json("{}");
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitHubReleaseSource(http, "1.0.0").GetLatestAsync(cancellation.Token));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request, cancellationToken));
    }
}
