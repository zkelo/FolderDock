using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public AppSettings Read()
    {
        var document = ReadDocument(recoverInvalid: false);
        return new AppSettings(document.AutoCheckUpdates, document.NewYearThemeSeason);
    }

    public void Write(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = ReadDocument(recoverInvalid: true);
        document.AutoCheckUpdates = settings.AutoCheckUpdates;
        document.NewYearThemeSeason = settings.NewYearThemeSeason;
        AtomicJsonFile.Write(_filePath, document);
    }

    private SettingsDocument ReadDocument(bool recoverInvalid)
    {
        try
        {
            return AtomicJsonFile.Read<SettingsDocument>(_filePath) ?? new SettingsDocument();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SettingsDocument();
        }
        catch (JsonException) when (recoverInvalid)
        {
            // An explicit global settings change repairs an unreadable JSON document.
            return new SettingsDocument();
        }
        catch (Exception exception) when (exception is JsonException || AtomicJsonFile.IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
    }

    private sealed class SettingsDocument
    {
        public SettingsDocument() { }

        [JsonPropertyName("autoCheckUpdates")]
        public bool AutoCheckUpdates { get; set; } = true;

        [JsonPropertyName("newYearThemeSeason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NewYearThemeSeason { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? OtherPreferences { get; set; }
    }
}
