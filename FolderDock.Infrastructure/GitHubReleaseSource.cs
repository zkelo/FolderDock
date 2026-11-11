using System.Net.Http.Headers;
using System.Text.Json;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class GitHubReleaseSource : IReleaseSource
{
    private static readonly Uri LatestRelease =
        new("https://api.github.com/repos/zkelo/FolderDock/releases/latest");
    private readonly HttpClient _http;
    private readonly string _applicationVersion;

    public GitHubReleaseSource(HttpClient http, string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        _http = http;
        _applicationVersion = applicationVersion;
    }

    public async Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("FolderDock", _applicationVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new ReleaseSourceException(UpdateError.HttpFailure,
                    $"GitHub API: HTTP {(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return ReadRelease(json.RootElement);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReleaseSourceException(UpdateError.ReleaseUnavailable, exception.Message, exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            throw new ReleaseSourceException(UpdateError.ReleaseUnavailable, exception.Message, exception);
        }
    }

    private static ReleaseInfo ReadRelease(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("The release response must be an object.");

        var tag = ReadString(root, "tag_name") ?? string.Empty;
        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                var name = ReadString(entry, "name");
                var url = ReadString(entry, "browser_download_url");
                if (name is not null && url is not null && InstallerAssetValidation.IsValid(name, url))
                    assets.Add(new ReleaseAsset(name, url));
            }
        }
        return new ReleaseInfo(tag, assets);
    }

    private static string? ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.String
            ? child.GetString()
            : null;
}
