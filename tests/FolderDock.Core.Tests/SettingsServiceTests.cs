using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void StoredPreferenceIsLoadedWithoutWritingItBack()
    {
        var store = new FakeSettingsStore { Stored = new(false) };
        var settings = new SettingsService(store);

        var result = settings.Load();

        Assert.Equal(new AppSettings(false), settings.Current);
        Assert.Equal(new SettingsResult(new(false)), result);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void UnreadablePreferencesUseDefaultsAndReportTheFailureWithoutOverwriting()
    {
        var store = new FakeSettingsStore { ReadFailure = new("invalid settings") };
        var settings = new SettingsService(store);

        var result = settings.Load();

        Assert.True(settings.Current.AutoCheckUpdates);
        Assert.Equal("invalid settings", result.Error);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void InitialControlBindingDoesNotPersistAnUnchangedValue()
    {
        var store = new FakeSettingsStore { Stored = new(false) };
        var settings = new SettingsService(store);
        settings.Load();

        settings.SetAutoCheckUpdates(false);

        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ChangingPreferencePersistsItAndUpdatesTheCurrentSession()
    {
        var store = new FakeSettingsStore();
        var settings = new SettingsService(store);
        settings.Load();

        var result = settings.SetAutoCheckUpdates(false);

        Assert.Equal(new AppSettings(false), settings.Current);
        Assert.Equal(settings.Current, store.Stored);
        Assert.Null(result.Error);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void FailedWriteKeepsTheUserChoiceForThisSessionAndReportsTheFailure()
    {
        var store = new FakeSettingsStore { WriteFailure = new("write denied") };
        var settings = new SettingsService(store);
        settings.Load();

        var result = settings.SetAutoCheckUpdates(false);

        Assert.False(settings.Current.AutoCheckUpdates);
        Assert.True(store.Stored.AutoCheckUpdates);
        Assert.Equal("write denied", result.Error);
        Assert.Equal(settings.Current, result.Settings);
    }

    [Fact]
    public void SubsequentChoiceCanBeSavedAfterStorageRecovers()
    {
        var store = new FakeSettingsStore { WriteFailure = new("write denied") };
        var settings = new SettingsService(store);
        settings.Load();
        settings.SetAutoCheckUpdates(false);
        store.WriteFailure = null;

        var result = settings.SetAutoCheckUpdates(true);

        Assert.Null(result.Error);
        Assert.Equal(settings.Current, store.Stored);
        Assert.Equal(2, store.Writes);
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public AppSettings Stored { get; set; } = new();
        public SettingsStoreException? ReadFailure { get; set; }
        public SettingsStoreException? WriteFailure { get; set; }
        public int Writes { get; private set; }

        public AppSettings Read() => ReadFailure is null ? Stored : throw ReadFailure;

        public void Write(AppSettings settings)
        {
            Writes++;
            if (WriteFailure is not null) throw WriteFailure;
            Stored = settings;
        }
    }
}
