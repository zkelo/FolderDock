namespace FolderDock.Core;

/// Keeps the active preferences usable even when persistence is unavailable.
public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public AppSettings Current { get; private set; } = new();

    public SettingsResult Load()
    {
        try
        {
            Current = _store.Read();
            return new(Current);
        }
        catch (SettingsStoreException exception)
        {
            Current = new AppSettings();
            return new(Current, exception.Message);
        }
    }

    public SettingsResult SetAutoCheckUpdates(bool enabled)
    {
        if (Current.AutoCheckUpdates == enabled)
            return new(Current);

        Current = Current with { AutoCheckUpdates = enabled };
        try
        {
            _store.Write(Current);
            return new(Current);
        }
        catch (SettingsStoreException exception)
        {
            // A write failure must not undo the user's preference for this session.
            return new(Current, exception.Message);
        }
    }
}
