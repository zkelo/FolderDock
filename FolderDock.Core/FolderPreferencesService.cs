namespace FolderDock.Core;

public sealed class FolderPreferencesService
{
    private readonly IFolderPreferencesStore _store;
    private readonly Dictionary<string, FolderPreferences> _unsaved = new(StringComparer.OrdinalIgnoreCase);
    private string? _folder;

    public FolderPreferencesService(IFolderPreferencesStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public FolderPreferences Current { get; private set; } = new();

    public string? Open(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = Path.TrimEndingDirectorySeparator(folder);
        try
        {
            Current = _unsaved.TryGetValue(_folder, out var pending) ? pending : _store.Read(_folder);
            return null;
        }
        catch (SettingsStoreException exception)
        {
            Current = new FolderPreferences();
            return exception.Message;
        }
    }

    public string? SetSort(FolderSortOrder sort) => Save(Current with { Sort = sort });

    public string? SetView(FolderViewMode view) => Save(Current with { View = view });

    private string? Save(FolderPreferences preferences)
    {
        if (_folder is null) throw new InvalidOperationException("Open a folder before changing its preferences.");
        if (preferences == Current) return null;

        Current = preferences;
        try
        {
            _store.Write(_folder, preferences);
            _unsaved.Remove(_folder);
            return null;
        }
        catch (SettingsStoreException exception)
        {
            _unsaved[_folder] = preferences;
            return exception.Message;
        }
    }
}
