using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly LegacyFolderSortMigration _migration;

    public JsonSettingsStore(string filePath, JsonFolderPreferencesStore folderPreferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(folderPreferences);
        _filePath = Path.GetFullPath(filePath);
        _migration = new LegacyFolderSortMigration(folderPreferences);
    }

    public AppSettings Read()
    {
        var document = ReadDocument(recoverInvalid: false);
        if (document.FolderSorts is { } legacy)
        {
            _migration.Run(legacy, pending =>
            {
                document.FolderSorts = pending;
                AtomicJsonFile.Write(_filePath, document);
            });
        }
        return new AppSettings(document.AutoCheckUpdates);
    }

    public void Write(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // A failed migration can leave entries to retry at the next startup.
        var document = ReadDocument(recoverInvalid: true);
        document.AutoCheckUpdates = settings.AutoCheckUpdates;
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

        [JsonPropertyName("folderSorts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, JsonElement>? FolderSorts { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? OtherPreferences { get; set; }
    }
}
