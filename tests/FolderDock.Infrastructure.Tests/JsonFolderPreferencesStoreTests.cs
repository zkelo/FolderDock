using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class JsonFolderPreferencesStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FolderDock-preferences-" + Guid.NewGuid().ToString("N"));
    private readonly string _directory;
    private readonly string _folder;
    private readonly JsonFolderPreferencesStore _store;

    public JsonFolderPreferencesStoreTests()
    {
        Directory.CreateDirectory(_root);
        _directory = Path.Combine(_root, "FolderPreferences");
        _folder = Path.Combine(_root, "Downloads");
        _store = new JsonFolderPreferencesStore(_directory);
    }

    [Fact]
    public void ReadingOrResettingAnUncustomizedFolderCreatesNothing()
    {
        Assert.Equal(new FolderPreferences(), _store.Read(_folder));
        _store.Write(_folder, new FolderPreferences());
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Theory]
    [InlineData(FolderSortOrder.Name, FolderViewMode.List, "default", "list")]
    [InlineData(FolderSortOrder.DateAdded, FolderViewMode.Default, "dateAdded", "default")]
    [InlineData(FolderSortOrder.Name, FolderViewMode.Grid, "default", "grid")]
    [InlineData(FolderSortOrder.DateAdded, FolderViewMode.Grid, "dateAdded", "grid")]
    [InlineData(FolderSortOrder.DateModified, FolderViewMode.List, "dateModified", "list")]
    public void WritesOneHashedFileAndRestoresBothPreferences(FolderSortOrder sort, FolderViewMode view,
        string savedSort, string savedView)
    {
        var preferences = new FolderPreferences(sort, view);
        _store.Write(_folder, preferences);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_folder.ToUpperInvariant())));
        var expectedPath = Path.Combine(_directory, hash + ".json");
        Assert.Equal(new[] { _directory }, Directory.EnumerateFileSystemEntries(_root));
        Assert.Equal(new[] { expectedPath }, Directory.EnumerateFileSystemEntries(_directory));
        using var document = JsonDocument.Parse(File.ReadAllText(expectedPath));
        Assert.Equal(savedSort, document.RootElement.GetProperty("sort").GetString());
        Assert.Equal(savedView, document.RootElement.GetProperty("view").GetString());
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.Equal(preferences, new JsonFolderPreferencesStore(_directory).Read(_folder));
    }

    [Fact]
    public void ChangingSortKeepsAnExplicitGridView()
    {
        _store.Write(_folder, new FolderPreferences(View: FolderViewMode.Grid));
        var service = new FolderPreferencesService(_store);
        Assert.Null(service.Open(_folder));
        Assert.Null(service.SetSort(FolderSortOrder.DateAdded));

        var path = Assert.Single(Directory.EnumerateFiles(_directory));
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("grid", json.RootElement.GetProperty("view").GetString());
        Assert.Equal(new FolderPreferences(FolderSortOrder.DateAdded, FolderViewMode.Grid), _store.Read(_folder));
    }

    [Fact]
    public async Task ConcurrentReadsAndReplacementsSeeOnlyCompletePreferences()
    {
        var first = new FolderPreferences(FolderSortOrder.DateAdded);
        var second = new FolderPreferences(FolderSortOrder.DateModified, FolderViewMode.List);
        var expected = new[] { first, second };
        _store.Write(_folder, first);
        using var start = new Barrier(2);
        var reader = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 160; i++)
                Assert.Contains(_store.Read(_folder), expected);
        });
        var writer = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 40; i++)
                _store.Write(_folder, expected[i % 2]);
        });

        await Task.WhenAll(reader, writer);
        Assert.Equal(second, _store.Read(_folder));
        Assert.Single(Directory.EnumerateFiles(_directory));
    }

    [Fact]
    public void CaseTrailingSeparatorAndDotSegmentsShareTheSameFile()
    {
        var alias = Path.Combine(_folder.ToLowerInvariant(), "nested", "..") + Path.DirectorySeparatorChar;
        var first = new FolderPreferences(FolderSortOrder.DateAdded);
        var second = new FolderPreferences(FolderSortOrder.DateModified, FolderViewMode.List);

        _store.Write(_folder, first);
        Assert.Equal(first, _store.Read(alias));
        _store.Write(alias, second);
        Assert.Equal(second, _store.Read(_folder));
        Assert.Single(Directory.EnumerateFiles(_directory));
    }

    [Fact]
    public void WindowsRootAliasesCanBeLookedUpWithoutAccessingTheDriveOrShare()
    {
        if (!OperatingSystem.IsWindows()) return;
        var drive = new FolderPreferences(FolderSortOrder.DateAdded);
        var share = new FolderPreferences(FolderSortOrder.DateModified);

        _store.Write(@"Q:\", drive);
        _store.Write(@"\\FileServer\Shared", share);

        Assert.Equal(drive, _store.Read("q:/"));
        Assert.Equal(share, _store.Read(@"\\fileserver\shared\"));
        Assert.Equal(2, Directory.EnumerateFiles(_directory).Count());
    }

    [Fact]
    public void ResetDeletesOnlyThatFoldersFile()
    {
        var nested = Path.Combine(_folder, "Archive");
        var childSettings = new FolderPreferences(FolderSortOrder.Name, FolderViewMode.List);
        _store.Write(_folder, new FolderPreferences(FolderSortOrder.DateAdded));
        _store.Write(nested, childSettings);

        _store.Write(_folder, new FolderPreferences());

        Assert.Equal(new FolderPreferences(), _store.Read(_folder));
        Assert.Equal(childSettings, _store.Read(nested));
        Assert.Single(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public void CorruptDocumentDoesNotAffectOtherFolders()
    {
        _store.Write(_folder, new FolderPreferences(FolderSortOrder.DateModified));
        var path = Assert.Single(Directory.EnumerateFiles(_directory));
        File.WriteAllText(path, "{");

        Assert.Throws<SettingsStoreException>(() => _store.Read(_folder));
        Assert.Equal("{", File.ReadAllText(path));
        Assert.Equal(new FolderPreferences(), _store.Read(Path.Combine(_folder, "Archive")));
    }

    [Fact]
    public void FailedReplacementPreservesTheDestinationAndCleansUpTemporaryFiles()
    {
        _store.Write(_folder, new FolderPreferences(FolderSortOrder.DateAdded));
        var path = Assert.Single(Directory.EnumerateFiles(_directory));
        File.Delete(path);
        Directory.CreateDirectory(path);
        var existing = Path.Combine(path, "keep.txt");
        File.WriteAllText(existing, "keep");

        Assert.Throws<SettingsStoreException>(() =>
            _store.Write(_folder, new FolderPreferences(FolderSortOrder.DateModified)));
        Assert.Equal("keep", File.ReadAllText(existing));
        Assert.Equal(new[] { path }, Directory.EnumerateFileSystemEntries(_directory));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
