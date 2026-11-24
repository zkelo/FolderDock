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

    [Theory]
    [InlineData(2026, 11, 30, NewYearThemeState.Hidden)]
    [InlineData(2026, 12, 1, NewYearThemeState.Enabled)]
    [InlineData(2026, 12, 31, NewYearThemeState.Enabled)]
    [InlineData(2027, 1, 1, NewYearThemeState.Enabled)]
    [InlineData(2027, 1, 14, NewYearThemeState.Enabled)]
    [InlineData(2027, 1, 15, NewYearThemeState.Hidden)]
    [InlineData(2027, 12, 1, NewYearThemeState.Enabled)]
    public void EnabledThemeFollowsTheLocalCalendarEveryYear(int year, int month, int day, NewYearThemeState state)
    {
        var settings = new AppSettings(NewYearThemeEnabled: true);

        Assert.Equal(state, settings.GetNewYearThemeState(new DateOnly(year, month, day)));
    }

    [Fact]
    public void ThemeCanBeEnabledAndDisabledInJanuaryWithoutChangingUpdateChecks()
    {
        var date = new DateOnly(2027, 1, 14);
        var store = new FakeSettingsStore { Stored = new(false) };
        var settings = new SettingsService(store);
        Assert.Equal(NewYearThemeState.Available, settings.Load().Settings.GetNewYearThemeState(date));

        settings.ToggleNewYearTheme(date);
        Assert.Equal(new AppSettings(false, NewYearThemeEnabled: true), store.Stored);
        Assert.Equal(NewYearThemeState.Enabled, settings.Load().Settings.GetNewYearThemeState(date));

        settings.ToggleNewYearTheme(date);
        Assert.Equal(new AppSettings(false), store.Stored);
        Assert.Equal(NewYearThemeState.Available, settings.Current.GetNewYearThemeState(date));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutOfSeasonTogglePreservesThePreference(bool enabled)
    {
        var store = new FakeSettingsStore { Stored = new(NewYearThemeEnabled: enabled) };
        var settings = new SettingsService(store);
        settings.Load();

        settings.ToggleNewYearTheme(new DateOnly(2027, 1, 15));

        Assert.Equal(0, store.Writes);
        Assert.Equal(enabled, settings.Current.NewYearThemeEnabled);
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
