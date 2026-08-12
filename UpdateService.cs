using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace FolderDock;

/// Результат проверки обновлений.
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version? LatestVersion,
    string? InstallerUrl,
    string? InstallerName,
    string? Error)
{
    public static UpdateCheckResult UpToDate(Version latest) =>
        new(false, latest, null, null, null);

    public static UpdateCheckResult Available(Version latest, string url, string name) =>
        new(true, latest, url, name, null);

    public static UpdateCheckResult Failed(string error) =>
        new(false, null, null, null, error);
}

/// Проверка и установка обновлений через GitHub Releases.
public static class UpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/zkelo/FolderDock/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // GitHub API требует User-Agent
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("FolderDock", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// Запрашивает последний релиз и сравнивает с текущей версией.
    /// Никогда не бросает — ошибки возвращаются в результате.
    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var response = await Http.GetAsync(LatestReleaseApi);
            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Failed($"GitHub API: HTTP {(int)response.StatusCode}");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                return UpdateCheckResult.Failed($"Непонятный тег релиза: «{tag}»");

            if (!Version.TryParse(AppInfo.Version, out var current))
                return UpdateCheckResult.Failed($"Непонятная текущая версия: «{AppInfo.Version}»");

            if (latest <= current)
                return UpdateCheckResult.UpToDate(latest);

            var installer = FindInstallerAsset(root);
            if (installer is null)
                return UpdateCheckResult.Failed(
                    $"В релизе {tag} нет установщика для {ArchitectureSuffix()}");

            return UpdateCheckResult.Available(latest, installer.Value.Url, installer.Value.Name);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return UpdateCheckResult.Failed($"Не удалось проверить обновления: {ex.Message}");
        }
    }

    /// Скачивает установщик во временную папку и запускает тихое обновление.
    /// Установщик сам закроет работающее приложение (taskkill в PrepareToInstall).
    public static async Task DownloadAndInstallAsync(UpdateCheckResult update)
    {
        if (update.InstallerUrl is null || update.InstallerName is null)
            throw new InvalidOperationException("Нет данных об установщике");

        var path = Path.Combine(Path.GetTempPath(), update.InstallerName);
        await using (var target = File.Create(path))
        await using (var source = await Http.GetStreamAsync(update.InstallerUrl))
            await source.CopyToAsync(target);

        // /SILENT — прогресс-бар без вопросов; страницы wizard не нужны,
        // путь установки берётся из предыдущей установки (реестр Inno Setup)
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            Arguments = "/SILENT /NORESTART",
            UseShellExecute = true
        });
    }

    private static (string Url, string Name)? FindInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)) return null;

        var suffix = $"-{ArchitectureSuffix()}.exe";
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.StartsWith("FolderDock-Setup-", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var url = asset.GetProperty("browser_download_url").GetString();
                if (url is not null) return (url, name);
            }
        }
        return null;
    }

    private static string ArchitectureSuffix() =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
}
