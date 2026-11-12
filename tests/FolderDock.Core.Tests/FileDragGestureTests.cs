using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FileDragGestureTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(104, 100)]
    [InlineData(96, 100)]
    [InlineData(100, 106)]
    [InlineData(100, 94)]
    [InlineData(104, 106)]
    public void MovementInsideTheThresholdRemainsAnOrdinaryClick(double x, double y)
    {
        var gesture = Begin();

        Assert.Null(gesture.TryStart(7, x, y, true));
        Assert.False(gesture.SuppressClick);
    }

    [Theory]
    [InlineData(104.1, 100)]
    [InlineData(95.9, 100)]
    [InlineData(100, 106.1)]
    [InlineData(100, 93.9)]
    public void CrossingEitherThresholdStartsTheOriginalItemOnce(double x, double y)
    {
        var gesture = Begin();

        Assert.Equal("original", gesture.TryStart(7, x, y, true));
        Assert.True(gesture.SuppressClick);
        Assert.Null(gesture.TryStart(7, x + 20, y + 20, true));
    }

    [Fact]
    public void AnotherPointerCannotStartOrCancelThePressedItem()
    {
        var gesture = Begin();

        Assert.Null(gesture.TryStart(8, 200, 200, true));
        gesture.Cancel(8);

        Assert.Equal("original", gesture.TryStart(7, 200, 200, true));
    }

    [Fact]
    public void ObservingAReleasedButtonDiscardsThePendingGesture()
    {
        var gesture = Begin();

        Assert.Null(gesture.TryStart(7, 200, 200, false));
        Assert.Null(gesture.TryStart(7, 200, 200, true));
        Assert.False(gesture.SuppressClick);
    }

    [Fact]
    public void CancelledPointerCannotStartLater()
    {
        var gesture = Begin();

        gesture.Cancel(7);

        Assert.Null(gesture.TryStart(7, 200, 200, true));
        Assert.False(gesture.SuppressClick);
    }

    [Fact]
    public void ReplacingTheViewInvalidatesThePressedItem()
    {
        var gesture = Begin();

        gesture.Reset();

        Assert.Null(gesture.TryStart(7, 200, 200, true));
    }

    [Fact]
    public void RefreshAndCaptureLossDuringADragDoNotEnableItsReleaseClick()
    {
        var gesture = Begin();
        Assert.Equal("original", gesture.TryStart(7, 200, 200, true));

        gesture.Reset();
        gesture.Cancel(7);

        Assert.True(gesture.SuppressClick);
        Assert.Null(gesture.TryStart(7, 300, 300, true));
    }

    [Fact]
    public void ANewPressRestoresOrdinaryClickBehavior()
    {
        var gesture = Begin();
        gesture.TryStart(7, 200, 200, true);

        gesture.Begin(7, "next", 50, 50, 4, 6);

        Assert.False(gesture.SuppressClick);
        Assert.Null(gesture.TryStart(7, 51, 51, true));
        Assert.Equal("next", gesture.TryStart(7, 60, 50, true));
    }

    [Fact]
    public void PressingNonDraggableContentClearsSuppressionWithoutArmingADrag()
    {
        var gesture = Begin();
        gesture.TryStart(7, 200, 200, true);

        gesture.Begin(8, null, 0, 0, 0, 0);

        Assert.False(gesture.SuppressClick);
        Assert.Null(gesture.TryStart(8, 200, 200, true));
        Assert.Null(gesture.TryStart(7, 200, 200, true));
    }

    [Fact]
    public void FractionalLogicalThresholdsArePreserved()
    {
        var gesture = new FileDragGesture();
        gesture.Begin(1, "scaled", 10, 10, 2.5, 3.5);

        Assert.Null(gesture.TryStart(1, 12.5, 13.5, true));
        Assert.Equal("scaled", gesture.TryStart(1, 12.6, 13.5, true));
    }

    private static FileDragGesture Begin()
    {
        var gesture = new FileDragGesture();
        gesture.Begin(7, "original", 100, 100, 4, 6);
        return gesture;
    }
}
