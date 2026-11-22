using System.Text.Json;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "FolderDock-settings-tests-" + Guid.NewGuid().ToString("N"));

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void MissingFileUsesDefaultsWithoutCreatingAnything()
    {
        var path = Path.Combine(_root, "not-created", "settings.json");

        var settings = OpenStore(path).Read();

        Assert.True(settings.AutoCheckUpdates);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"futurePreference\":123}")]
    public void MissingPreferenceUsesTheExistingDefault(string json)
    {
        var path = WriteSettings(json);

        var settings = OpenStore(path).Read();

        Assert.True(settings.AutoCheckUpdates);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsExistingSettingsSchemaAndIgnoresUnknownProperties(bool enabled)
    {
        var path = WriteSettings(
            $"{{\"autoCheckUpdates\":{enabled.ToString().ToLowerInvariant()},\"futurePreference\":{{\"nested\":true}}}}");

        var settings = OpenStore(path).Read();

        Assert.Equal(enabled, settings.AutoCheckUpdates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"settings\"")]
    [InlineData("{\"autoCheckUpdates\":\"false\"}")]
    [InlineData("{\"autoCheckUpdates\":null}")]
    public void InvalidSettingsReportFailureWithoutReplacingTheFile(string json)
    {
        var path = WriteSettings(json);

        var exception = Assert.Throws<SettingsStoreException>(() => OpenStore(path).Read());

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void ExplicitGlobalSettingChangeRepairsInvalidJson()
    {
        var path = WriteSettings("{");
        var store = OpenStore(path);

        Assert.Throws<SettingsStoreException>(() => store.Read());
        store.Write(new AppSettings(false));

        Assert.False(store.Read().AutoCheckUpdates);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WritesCompatibleSchemaAndRoundTripsThePreference(bool enabled)
    {
        var path = Path.Combine(_root, "settings.json");
        var store = OpenStore(path);

        store.Write(new AppSettings(enabled));

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(enabled, json.RootElement.GetProperty("autoCheckUpdates").GetBoolean());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal(new AppSettings(enabled), store.Read());
        Assert.Equal(new[] { path }, Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void WriteCreatesTheSettingsDirectory()
    {
        var path = Path.Combine(_root, "nested", "FolderDock", "settings.json");
        var store = OpenStore(path);

        store.Write(new AppSettings(false));

        Assert.False(store.Read().AutoCheckUpdates);
        Assert.Equal(new[] { path }, Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void MigratesLegacySortsAndPreservesNewerFolderPreferences()
    {
        var downloads = Path.Combine(_root, "Downloads");
        var desktop = Path.Combine(_root, "Desktop");
        var path = WriteLegacySorts((downloads, "DateAdded"), (desktop, "DateModified"));
        var preferences = OpenFolderStore();
        var newerChoice = new FolderPreferences(View: FolderViewMode.List);
        preferences.Write(desktop, newerChoice);

        Assert.False(OpenStore(path).Read().AutoCheckUpdates);

        Assert.Equal(new FolderPreferences(FolderSortOrder.DateAdded), preferences.Read(downloads));
        Assert.Equal(newerChoice, preferences.Read(desktop));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Single(document.RootElement.EnumerateObject());
        Assert.False(document.RootElement.GetProperty("autoCheckUpdates").GetBoolean());

        preferences.Write(downloads, new FolderPreferences());
        Assert.False(OpenStore(path).Read().AutoCheckUpdates);
        Assert.Equal(new FolderPreferences(), preferences.Read(downloads));
    }

    [Fact]
    public void SkipsUnknownValuesAndInvalidPathsWithoutLosingValidSorts()
    {
        var unknown = Path.Combine(_root, "future");
        var valid = Path.Combine(_root, "Downloads");
        var path = WriteLegacySorts((unknown, "FutureOrder"), ("relative", "DateAdded"),
            (Path.Combine(_root, "invalid") + '\0', "DateAdded"), (valid, "DateModified"));

        Assert.False(OpenStore(path).Read().AutoCheckUpdates);

        Assert.Equal(new FolderPreferences(), OpenFolderStore().Read(unknown));
        Assert.Equal(FolderSortOrder.DateModified, OpenFolderStore().Read(valid).Sort);
        Assert.Single(Directory.GetFiles(PreferencesDirectory));
    }

    [Fact]
    public void MigrationKeepsUnrelatedGlobalFields()
    {
        var path = WriteSettings("""{"autoCheckUpdates":false,"folderSorts":{},"future":{"key":42}}""");

        Assert.False(OpenStore(path).Read().AutoCheckUpdates);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(42, document.RootElement.GetProperty("future").GetProperty("key").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("folderSorts", out _));
    }

    [Fact]
    public void OnlyFailedImportsSurviveAGlobalSettingChangeAndRetryOnRestart()
    {
        var blocked = Path.Combine(_root, "Downloads");
        var successful = Path.Combine(_root, "Pictures");
        var path = WriteLegacySorts((successful, "DateModified"), (blocked, "DateAdded"));
        var preferences = OpenFolderStore();
        preferences.Write(blocked, new FolderPreferences(FolderSortOrder.DateAdded));
        var blockedFile = Assert.Single(Directory.GetFiles(PreferencesDirectory));
        File.Delete(blockedFile);
        Directory.CreateDirectory(blockedFile);
        var store = OpenStore(path);

        Assert.False(store.Read().AutoCheckUpdates);
        store.Write(new AppSettings(true));
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            var pending = document.RootElement.GetProperty("folderSorts");
            Assert.Single(pending.EnumerateObject());
            Assert.Equal("DateAdded", pending.GetProperty(blocked).GetString());
        }
        Assert.Equal(FolderSortOrder.DateModified, preferences.Read(successful).Sort);
        preferences.Write(successful, new FolderPreferences());

        Directory.Delete(blockedFile);
        Assert.True(OpenStore(path).Read().AutoCheckUpdates);
        Assert.Equal(FolderSortOrder.DateAdded, preferences.Read(blocked).Sort);
        Assert.Equal(new FolderPreferences(), preferences.Read(successful));
    }

    [Fact]
    public void ResetAfterInterruptedCleanupSurvivesTheNextMigration()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(_root, "Downloads");
        var path = WriteLegacySorts((folder, "DateAdded"));
        var preferences = OpenFolderStore();
        var store = new JsonSettingsStore(path, preferences);

        using (var blockReplacement = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(store.Read().AutoCheckUpdates);
            Assert.Equal(FolderSortOrder.DateAdded, preferences.Read(folder).Sort);
            preferences.Write(folder, new FolderPreferences());
            Assert.Single(Directory.GetFiles(PreferencesDirectory));
        }
        Assert.False(OpenStore(path).Read().AutoCheckUpdates);
        Assert.Equal(new FolderPreferences(), preferences.Read(folder));
        Assert.Empty(Directory.GetFiles(PreferencesDirectory));
    }

    [Fact]
    public void SuccessfulWriteReplacesExistingSettingsWithoutLeavingTemporaryFiles()
    {
        var path = WriteSettings("{\"autoCheckUpdates\":true}");
        var store = OpenStore(path);

        store.Write(new AppSettings(false));

        Assert.False(store.Read().AutoCheckUpdates);
        Assert.Equal(new[] { path }, Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void FailedReplacementPreservesTheDestinationAndCleansUpTemporaryFiles()
    {
        var destination = Path.Combine(_root, "settings.json");
        Directory.CreateDirectory(destination);
        var existingFile = Path.Combine(destination, "existing.txt");
        File.WriteAllText(existingFile, "preserve this");

        var exception = Assert.Throws<SettingsStoreException>(() =>
            OpenStore(destination).Write(new AppSettings(false)));

        Assert.NotNull(exception.InnerException);
        Assert.Equal("preserve this", File.ReadAllText(existingFile));
        Assert.Equal(new[] { destination }, Directory.EnumerateFileSystemEntries(_root));
        Assert.Equal(new[] { existingFile }, Directory.EnumerateFileSystemEntries(destination));
    }

    [Fact]
    public void FailureToCreateTheDirectoryDoesNotReplaceTheObstructingFile()
    {
        var obstruction = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(obstruction, "preserve this");
        var path = Path.Combine(obstruction, "settings.json");

        Assert.Throws<SettingsStoreException>(() =>
            OpenStore(path).Write(new AppSettings(false)));

        Assert.Equal("preserve this", File.ReadAllText(obstruction));
        Assert.Equal(new[] { obstruction }, Directory.EnumerateFileSystemEntries(_root));
    }

    private string PreferencesDirectory => Path.Combine(_root, "FolderPreferences");

    private JsonFolderPreferencesStore OpenFolderStore() => new(PreferencesDirectory);

    private JsonSettingsStore OpenStore(string path) => new(path, OpenFolderStore());

    private string WriteLegacySorts(params (string Folder, string Sort)[] values) =>
        WriteSettings(JsonSerializer.Serialize(new
        {
            autoCheckUpdates = false,
            folderSorts = values.ToDictionary(entry => entry.Folder, entry => entry.Sort)
        }));

    private string WriteSettings(string json)
    {
        var path = Path.Combine(_root, "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
