using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderNavigationTests
{
    [Fact]
    public void NavigatingKeepsRootIdentityAndClearsSearch()
    {
        var navigation = new FolderNavigation();
        navigation.Begin("photos");
        navigation.SetSearchQuery("trip");

        navigation.NavigateTo("photos/trip");

        Assert.Equal("photos", navigation.RootFolder);
        Assert.Equal("photos/trip", navigation.CurrentFolder);
        Assert.True(navigation.CanNavigateBack);
        Assert.Empty(navigation.SearchQuery);
    }

    [Fact]
    public void BackTraversesVisitedFoldersAndStopsAtTheRoot()
    {
        var navigation = new FolderNavigation();
        navigation.Begin("photos");
        navigation.NavigateTo("photos/trip");
        navigation.NavigateTo("photos/trip/day-one");
        navigation.SetSearchQuery("sunset");

        Assert.True(navigation.TryNavigateBack());
        Assert.Equal("photos/trip", navigation.CurrentFolder);
        Assert.Empty(navigation.SearchQuery);
        Assert.True(navigation.TryNavigateBack());
        Assert.Equal("photos", navigation.CurrentFolder);
        Assert.False(navigation.CanNavigateBack);
        Assert.False(navigation.TryNavigateBack());
        Assert.Equal("photos", navigation.CurrentFolder);
    }

    [Fact]
    public void ReopeningAnotherRootDiscardsThePreviousNavigationAndQuery()
    {
        var navigation = new FolderNavigation();
        navigation.Begin("photos");
        navigation.NavigateTo("photos/trip");
        navigation.SetSearchQuery("summer");

        navigation.Begin("documents");

        Assert.Equal("documents", navigation.RootFolder);
        Assert.Equal("documents", navigation.CurrentFolder);
        Assert.False(navigation.CanNavigateBack);
        Assert.Empty(navigation.SearchQuery);
    }

    [Fact]
    public void QueryTrimmingDoesNotRestartAnUnchangedSearch()
    {
        var navigation = new FolderNavigation();
        navigation.Begin("photos");

        Assert.True(navigation.SetSearchQuery("  summer  "));
        Assert.Equal("summer", navigation.SearchQuery);
        Assert.False(navigation.SetSearchQuery("summer "));
        Assert.True(navigation.SetSearchQuery(" "));
        Assert.Empty(navigation.SearchQuery);
    }

    [Fact]
    public void BackAtTheRootLeavesTheActiveSearchUntouched()
    {
        var navigation = new FolderNavigation();
        navigation.Begin("photos");
        navigation.SetSearchQuery("summer");

        Assert.False(navigation.TryNavigateBack());

        Assert.Equal("summer", navigation.SearchQuery);
    }

    [Fact]
    public void NavigationRequiresAnOpenedRoot()
    {
        var navigation = new FolderNavigation();

        Assert.Throws<InvalidOperationException>(() => navigation.NavigateTo("photos"));
        Assert.Null(navigation.RootFolder);
        Assert.Null(navigation.CurrentFolder);
        Assert.False(navigation.CanNavigateBack);
    }
}
