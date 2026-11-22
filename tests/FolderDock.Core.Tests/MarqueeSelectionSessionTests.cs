using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class MarqueeSelectionSessionTests
{
    [Theory]
    [InlineData(0, 0, 25, 35)]
    [InlineData(25, 35, 0, 0)]
    [InlineData(25, 0, 0, 35)]
    [InlineData(0, 35, 25, 0)]
    public void DraggingInAnyDirectionSelectsPartiallyCoveredItems(
        double startX, double startY, double endX, double endY)
    {
        var session = new MarqueeSelectionSession<string>(["previous"], addToSelection: false);
        session.RecordBounds("inside", new SelectionBounds(5, 5, 10, 10));
        session.RecordBounds("partial", new SelectionBounds(20.5, 30.5, 40, 40));
        session.RecordBounds("adjacent", new SelectionBounds(25, 15, 35, 20));
        session.RecordBounds("outside", new SelectionBounds(30, 40, 35, 50));

        var bounds = SelectionBounds.Between(startX, startY, endX, endY);

        Assert.Equal(new SelectionBounds(0, 0, 25, 35), bounds);
        Assert.Equal(new[] { "inside", "partial" }, session.ItemsIn(bounds).Order());
    }

    [Fact]
    public void ScrollingRetainsOffscreenItemsUntilTheRectangleMovesPastThem()
    {
        var session = new MarqueeSelectionSession<string>([], addToSelection: false);
        session.RecordBounds("first", new SelectionBounds(1, 1, 20, 18));
        var initial = session.ItemsIn(new SelectionBounds(0, 0, 15, 20));
        Assert.Equal("first", Assert.Single(initial));

        session.RecordBounds("second", new SelectionBounds(1, 50, 20, 68));
        var expanded = session.ItemsIn(new SelectionBounds(0, 0, 15, 70));
        var retracted = session.ItemsIn(new SelectionBounds(0, 20, 15, 70));

        Assert.Equal(new[] { "first", "second" }, expanded.Order());
        Assert.Equal("second", Assert.Single(retracted));
    }

    [Fact]
    public void ExtendingASelectionUsesTheOriginalSelectionOnEveryMove()
    {
        var previous = new List<string> { "outside", "inside" };
        var session = new MarqueeSelectionSession<string>(previous, addToSelection: true);
        previous.Clear();
        session.RecordBounds("inside", new SelectionBounds(0, 0, 10, 10));
        session.RecordBounds("new", new SelectionBounds(10, 10, 20, 20));

        var expanded = session.ItemsIn(new SelectionBounds(0, 0, 20, 20));
        var retracted = session.ItemsIn(new SelectionBounds(0, 0, 5, 5));

        Assert.Equal(new[] { "inside", "new", "outside" }, expanded.Order());
        Assert.Equal(new[] { "inside", "outside" }, retracted.Order());
        Assert.Equal(new[] { "inside", "outside" }, session.PreviousSelection.Order());
    }

    [Fact]
    public void RelayoutReplacesTheOldBoundsForTheSameItem()
    {
        var session = new MarqueeSelectionSession<int>([], addToSelection: false);
        var oldBounds = new SelectionBounds(0, 0, 10, 10);
        var newBounds = new SelectionBounds(20, 20, 30, 30);
        session.RecordBounds(1, oldBounds);
        session.RecordBounds(1, newBounds);

        Assert.Empty(session.ItemsIn(oldBounds));
        Assert.Equal(1, Assert.Single(session.ItemsIn(newBounds)));
    }
}
