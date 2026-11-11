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

        var settings = new JsonSettingsStore(path).Read();

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

        var settings = new JsonSettingsStore(path).Read();

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

        var settings = new JsonSettingsStore(path).Read();

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

        var exception = Assert.Throws<SettingsStoreException>(() => new JsonSettingsStore(path).Read());

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WritesCompatibleSchemaAndRoundTripsThePreference(bool enabled)
    {
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonSettingsStore(path);

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
        var store = new JsonSettingsStore(path);

        store.Write(new AppSettings(false));

        Assert.False(store.Read().AutoCheckUpdates);
        Assert.Equal(new[] { path }, Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void SuccessfulWriteReplacesExistingSettingsWithoutLeavingTemporaryFiles()
    {
        var path = WriteSettings("{\"autoCheckUpdates\":true}");
        var store = new JsonSettingsStore(path);

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
            new JsonSettingsStore(destination).Write(new AppSettings(false)));

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
            new JsonSettingsStore(path).Write(new AppSettings(false)));

        Assert.Equal("preserve this", File.ReadAllText(obstruction));
        Assert.Equal(new[] { obstruction }, Directory.EnumerateFileSystemEntries(_root));
    }

    private string WriteSettings(string json)
    {
        var path = Path.Combine(_root, "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
