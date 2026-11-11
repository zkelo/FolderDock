using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class PinnedFolderServiceTests
{
    [Fact]
    public void OnlyShortcutsWhoseTargetFoldersStillExistAreShown()
    {
        var first = new FolderShortcut("First", "first-folder", "first-shortcut");
        var missing = new FolderShortcut("Deleted", "deleted-folder", "deleted-shortcut");
        var second = new FolderShortcut("Second", "second-folder", "second-shortcut");
        var store = new FakeShortcutStore(first, missing, second);
        store.MissingFolders.Add(missing.FolderPath);

        var available = new PinnedFolderService(store).GetAvailable();

        Assert.Equal(new[] { first, second }, available);
        Assert.Equal(0, store.Creates);
    }

    [Fact]
    public void RefreshReadsCurrentShortcutsInsteadOfKeepingAStaleList()
    {
        var shortcut = new FolderShortcut("First", "first-folder", "first-shortcut");
        var store = new FakeShortcutStore(shortcut);
        var pins = new PinnedFolderService(store);
        Assert.Single(pins.GetAvailable());
        store.MissingFolders.Add(shortcut.FolderPath);

        Assert.Empty(pins.GetAvailable());
    }

    [Fact]
    public void CreatedShortcutUsesTheStoresActualNameAndPath()
    {
        var expected = new FolderShortcut("Folder (unique)", "folder", "unique-shortcut");
        var store = new FakeShortcutStore { Created = expected };

        var created = new PinnedFolderService(store).Create("folder");

        Assert.Equal(expected, created);
        Assert.Equal("folder", store.CreatedFolder);
        Assert.Equal(1, store.Creates);
    }

    [Fact]
    public void StorageFailureIsNotReportedAsAnEmptyShortcutDirectory()
    {
        var failure = new FolderShortcutException("unreadable shortcut directory");
        var store = new FakeShortcutStore { ReadFailure = failure };

        Assert.Same(failure,
            Assert.Throws<FolderShortcutException>(() => new PinnedFolderService(store).GetAvailable()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyFolderSelectionDoesNotCreateAShortcut(string folder)
    {
        var store = new FakeShortcutStore();

        Assert.Throws<ArgumentException>(() => new PinnedFolderService(store).Create(folder));
        Assert.Equal(0, store.Creates);
    }

    private sealed class FakeShortcutStore(params FolderShortcut[] shortcuts) : IFolderShortcutStore
    {
        public HashSet<string> MissingFolders { get; } = new();
        public FolderShortcut? Created { get; set; }
        public string? CreatedFolder { get; private set; }
        public int Creates { get; private set; }
        public FolderShortcutException? ReadFailure { get; set; }

        public IReadOnlyList<FolderShortcut> ReadAll() =>
            ReadFailure is null ? shortcuts : throw ReadFailure;

        public bool FolderExists(string folderPath) => !MissingFolders.Contains(folderPath);

        public FolderShortcut Create(string folderPath)
        {
            Creates++;
            CreatedFolder = folderPath;
            return Created!;
        }
    }
}
