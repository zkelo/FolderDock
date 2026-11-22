using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderPreferencesServiceTests
{
    [Fact]
    public void OpeningAndSelectingTheCurrentChoicesNeverWrites()
    {
        var store = new FakeStore();
        var service = new FolderPreferencesService(store);

        Assert.Null(service.Open("Downloads"));
        Assert.Null(service.SetSort(FolderSortOrder.Name));
        Assert.Null(service.SetView(FolderViewMode.Grid));
        Assert.Equal(new FolderPreferences(), service.Current);
        Assert.Equal(1, store.Reads);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void EditingEitherChoicePreservesTheOtherAndFollowsTheSelectedFolder()
    {
        var store = new FakeStore();
        var service = new FolderPreferencesService(store);
        service.Open("Downloads");
        service.SetView(FolderViewMode.List);
        service.SetSort(FolderSortOrder.DateModified);
        service.Open("Documents");
        Assert.Equal(new FolderPreferences(), service.Current);
        service.SetSort(FolderSortOrder.DateAdded);

        var reopened = new FolderPreferencesService(store);
        reopened.Open("Downloads");
        Assert.Equal(new FolderPreferences(FolderSortOrder.DateModified, FolderViewMode.List), reopened.Current);
        reopened.SetSort(FolderSortOrder.Name);
        Assert.Equal(FolderViewMode.List, store.Read("Downloads").View);
        reopened.Open("Documents");
        Assert.Equal(new FolderPreferences(FolderSortOrder.DateAdded, FolderViewMode.Grid), reopened.Current);
    }

    [Fact]
    public void FailedWritesKeepTheChoiceOnReturnAndLaterChangesSaveBothPreferences()
    {
        var store = new FakeStore { WriteFailure = new("write denied") };
        var service = new FolderPreferencesService(store);
        service.Open("Downloads");
        Assert.Equal("write denied", service.SetView(FolderViewMode.List));

        service.Open("Documents");
        Assert.Equal(new FolderPreferences(), service.Current);
        service.Open("downloads" + Path.DirectorySeparatorChar);
        Assert.Equal(FolderViewMode.List, service.Current.View);
        store.WriteFailure = null;
        Assert.Null(service.SetSort(FolderSortOrder.DateAdded));

        Assert.Equal(new FolderPreferences(FolderSortOrder.DateAdded, FolderViewMode.List), store.Read("Downloads"));
    }

    [Fact]
    public void ReadFailureUsesDefaultsWithoutSavingOverTheUnreadableFolder()
    {
        var store = new FakeStore();
        var service = new FolderPreferencesService(store);
        service.Open("Downloads");
        service.SetView(FolderViewMode.List);
        store.ReadFailure = new("cannot read folder preferences");

        Assert.Equal("cannot read folder preferences", service.Open("Documents"));
        Assert.Equal(new FolderPreferences(), service.Current);
        Assert.Equal(1, store.Writes);
    }

    private sealed class FakeStore : IFolderPreferencesStore
    {
        private readonly Dictionary<string, FolderPreferences> _saved = new(StringComparer.OrdinalIgnoreCase);

        public SettingsStoreException? ReadFailure { get; set; }
        public SettingsStoreException? WriteFailure { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }

        public FolderPreferences Read(string folder)
        {
            Reads++;
            if (ReadFailure is not null) throw ReadFailure;
            return _saved.GetValueOrDefault(folder) ?? new FolderPreferences();
        }

        public void Write(string folder, FolderPreferences preferences)
        {
            Writes++;
            if (WriteFailure is not null) throw WriteFailure;
            _saved[folder] = preferences;
        }
    }
}
