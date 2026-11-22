using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class JsonFolderPreferencesStore : IFolderPreferencesStore
{
    private readonly string _directory;

    public JsonFolderPreferencesStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    public FolderPreferences Read(string folder)
    {
        try
        {
            var document = AtomicJsonFile.Read<Document>(FilePath(folder));
            return document?.ToPreferences() ?? new FolderPreferences();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new FolderPreferences();
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
        if (preferences == new FolderPreferences())
            AtomicJsonFile.Delete(path);
        else
            AtomicJsonFile.Write(path, Document.From(preferences));
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
        public FolderPreferences ToPreferences() => new(Sort switch
        {
            "name" => FolderSortOrder.Name,
            "dateAdded" => FolderSortOrder.DateAdded,
            "dateModified" => FolderSortOrder.DateModified,
            _ => throw new JsonException($"Unknown folder sort order '{Sort}'.")
        }, View switch
        {
            "grid" => FolderViewMode.Grid,
            "list" => FolderViewMode.List,
            _ => throw new JsonException($"Unknown folder view '{View}'.")
        });

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
