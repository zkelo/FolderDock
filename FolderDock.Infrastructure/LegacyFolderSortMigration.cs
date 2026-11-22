using System.Diagnostics;
using System.Security;
using System.Text.Json;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

internal sealed class LegacyFolderSortMigration(JsonFolderPreferencesStore preferences)
{
    public void Run(Dictionary<string, JsonElement> legacy, Action<Dictionary<string, JsonElement>?> checkpoint)
    {
        var pending = new Dictionary<string, JsonElement>();
        var existing = new List<string>();
        foreach (var (folder, value) in legacy)
        {
            if (!TryReadLegacySort(folder, value, out var sort)) continue;
            try
            {
                if (!preferences.ImportSort(folder, sort)) existing.Add(folder);
            }
            catch (SettingsStoreException exception)
            {
                ReportFailure(exception);
                pending.Add(folder, value);
            }
        }

        if (pending.Count == legacy.Count && pending.Count > 0)
        {
            preferences.ProtectLegacyDefaults(pending.Keys);
            return;
        }
        try
        {
            checkpoint(pending.Count == 0 ? null : pending);
        }
        catch (SettingsStoreException exception)
        {
            // The original batch remains on disk. An explicit reset must survive a retry.
            preferences.ProtectLegacyDefaults(legacy.Where(pair => TryReadLegacySort(pair.Key, pair.Value, out _))
                .Select(pair => pair.Key));
            ReportFailure(exception);
            return;
        }

        preferences.ProtectLegacyDefaults(pending.Keys);
        foreach (var folder in existing)
            RemoveDefaultMarker(folder);
    }

    private void RemoveDefaultMarker(string folder)
    {
        try
        {
            preferences.RemoveLegacyDefaultMarker(folder);
        }
        catch (SettingsStoreException exception)
        {
            ReportFailure(exception);
        }
    }

    private static bool TryReadLegacySort(string folder, JsonElement value, out FolderSortOrder sort)
    {
        sort = FolderSortOrder.Name;
        if (value.ValueKind != JsonValueKind.String || !Enum.TryParse(value.GetString(), out sort)
            || sort is not (FolderSortOrder.DateAdded or FolderSortOrder.DateModified)
            || !Path.IsPathFullyQualified(folder)) return false;

        try
        {
            _ = Path.GetFullPath(folder);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or IOException or SecurityException)
        {
            return false;
        }
    }

    private static void ReportFailure(SettingsStoreException exception) =>
        Trace.WriteLine($"FolderDock: legacy folder sort migration failed: {exception.Message}");
}
