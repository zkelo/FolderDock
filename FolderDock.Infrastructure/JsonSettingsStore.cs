using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public AppSettings Read()
    {
        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(_filePath));
            return new AppSettings(document?.AutoCheckUpdates ?? true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException || IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
    }

    public void Write(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_filePath)!;
        var temporaryPath = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            var document = new SettingsDocument { AutoCheckUpdates = settings.AutoCheckUpdates };
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
        finally
        {
            RemoveTemporaryFile(temporaryPath);
        }
    }

    private static void RemoveTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // Cleanup cannot replace the outcome of writing the preference.
            System.Diagnostics.Trace.WriteLine($"FolderDock: settings cleanup failed: {exception}");
        }
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;

    private sealed class SettingsDocument
    {
        public SettingsDocument() { }

        [JsonPropertyName("autoCheckUpdates")]
        public bool AutoCheckUpdates { get; set; } = true;
    }
}
