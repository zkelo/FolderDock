using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class JsonFolderPreferencesStore : IFolderPreferencesStore
{
    private readonly string _directory;
    private HashSet<string> _legacyFiles = [];

    public JsonFolderPreferencesStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    public FolderPreferences Read(string folder) =>
        ReadDocument(FilePath(folder))?.ToPreferences() ?? new FolderPreferences();

    private static Document? ReadDocument(string path)
    {
        try
        {
            return AtomicJsonFile.Read<Document>(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is JsonException || AtomicJsonFile.IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
    }

    public void Write(string folder, FolderPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var path = FilePath(folder);
        if (preferences == new FolderPreferences() && !_legacyFiles.Contains(path))
            AtomicJsonFile.Delete(path);
        else
            AtomicJsonFile.Write(path, Document.From(preferences));
    }

    internal bool ImportSort(string folder, FolderSortOrder sort) =>
        sort != FolderSortOrder.Name &&
        AtomicJsonFile.Write(FilePath(folder), Document.From(new FolderPreferences(sort)), overwrite: false);

    // If migration could not retire the old settings, an explicit default prevents
    // a later retry from restoring a customization that the user has since reset.
    internal void ProtectLegacyDefaults(IEnumerable<string> folders) =>
        _legacyFiles = folders.Select(FilePath).ToHashSet(StringComparer.Ordinal);

    internal void RemoveLegacyDefaultMarker(string folder)
    {
        var path = FilePath(folder);
        if (ReadDocument(path) is
            { Sort: "name" or "default", View: "grid" or "default", ExtraFields: null or { Count: 0 } })
            AtomicJsonFile.Delete(path);
    }

    private string FilePath(string folder)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
        return Path.Combine(_directory, hash + ".json");
    }

    private sealed record Document(
        [property: JsonPropertyName("sort")] string? Sort,
        [property: JsonPropertyName("view")] string? View)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }

        public FolderPreferences ToPreferences() => new(Sort switch
        {
            "dateAdded" => FolderSortOrder.DateAdded,
            "dateModified" => FolderSortOrder.DateModified,
            _ => FolderSortOrder.Name
        }, View == "list" ? FolderViewMode.List : FolderViewMode.Grid);

        public static Document From(FolderPreferences preferences) => new(preferences.Sort switch
        {
            FolderSortOrder.Name => "name",
            FolderSortOrder.DateAdded => "dateAdded",
            FolderSortOrder.DateModified => "dateModified",
            _ => throw new ArgumentOutOfRangeException(nameof(preferences), preferences.Sort, null)
        }, preferences.View switch
        {
            FolderViewMode.Grid => "grid",
            FolderViewMode.List => "list",
            _ => throw new ArgumentOutOfRangeException(nameof(preferences), preferences.View, null)
        });
    }
}
