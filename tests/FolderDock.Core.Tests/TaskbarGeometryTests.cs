using System.Drawing;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class TaskbarGeometryTests
{
    [Theory]
    [InlineData(-1800, -100, 48, 900, TaskbarEdge.Left)]
    [InlineData(-1800, -100, 1600, 48, TaskbarEdge.Top)]
    [InlineData(-248, -100, 48, 900, TaskbarEdge.Right)]
    [InlineData(-1800, 752, 1600, 48, TaskbarEdge.Bottom)]
    [InlineData(-1846, -100, 48, 900, TaskbarEdge.Left)]
    [InlineData(-1800, -146, 1600, 48, TaskbarEdge.Top)]
    [InlineData(-202, -100, 48, 900, TaskbarEdge.Right)]
    [InlineData(-1800, 798, 1600, 48, TaskbarEdge.Bottom)]
    public void RecognizesVisibleAndAutoHiddenTaskbarOnSecondaryMonitor(
        int x, int y, int width, int height, TaskbarEdge expected)
    {
        var monitor = new Rectangle(-1800, -100, 1600, 900);
        Assert.Equal(expected, TaskbarGeometry.FromTaskbarBounds(monitor, new Rectangle(x, y, width, height)));
    }

    [Theory]
    [InlineData(-1752, -100, 1552, 900, TaskbarEdge.Left)]
    [InlineData(-1800, -52, 1600, 852, TaskbarEdge.Top)]
    [InlineData(-1800, -100, 1552, 900, TaskbarEdge.Right)]
    [InlineData(-1800, -100, 1600, 852, TaskbarEdge.Bottom)]
    [InlineData(-1800, -100, 1600, 900, TaskbarEdge.Bottom)]
    public void UsesReservedWorkAreaEdgeAndDefaultsToBottom(
        int x, int y, int width, int height, TaskbarEdge expected)
    {
        var monitor = new Rectangle(-1800, -100, 1600, 900);
        Assert.Equal(expected, TaskbarGeometry.FromWorkArea(monitor, new Rectangle(x, y, width, height)));
    }

    [Theory]
    [InlineData(TaskbarEdge.Left, -1800, -100, 64, 900, -1736, -100, 1536, 900)]
    [InlineData(TaskbarEdge.Top, -1800, -100, 1600, 32, -1800, -68, 1600, 868)]
    [InlineData(TaskbarEdge.Right, -280, -100, 80, 900, -1800, -100, 1520, 900)]
    [InlineData(TaskbarEdge.Bottom, -1800, 752, 1600, 48, -1800, -100, 1600, 852)]
    public void MeasuresVisibleTaskbarOnceEvenIfWorkAreaAlreadyExcludesIt(
        TaskbarEdge edge, int barX, int barY, int barWidth, int barHeight,
        int workX, int workY, int workWidth, int workHeight)
    {
        var monitor = new Rectangle(-1800, -100, 1600, 900);
        var bounds = new Rectangle(barX, barY, barWidth, barHeight);
        var available = new Rectangle(workX, workY, workWidth, workHeight);
        var expected = new TaskbarLayout(edge, available);

        Assert.Equal(expected, TaskbarGeometry.Measure(monitor, monitor, edge, bounds, true));
        Assert.Equal(expected, TaskbarGeometry.Measure(monitor, available, edge, bounds, true));
        Assert.Equal(expected, TaskbarGeometry.Measure(monitor, available, edge, Rectangle.Empty, false));
    }

    [Theory]
    [InlineData(-1800, -100, 1300, 900, TaskbarEdge.Right)]
    [InlineData(-1800, 80, 1600, 720, TaskbarEdge.Top)]
    [InlineData(-1800, -100, 1600, 900, TaskbarEdge.Bottom)]
    public void MonitorWithoutTaskbarRetainsWorkArea(
        int x, int y, int width, int height, TaskbarEdge expectedEdge)
    {
        var monitor = new Rectangle(-1800, -100, 1600, 900);
        var workArea = new Rectangle(x, y, width, height);

        var layout = TaskbarGeometry.Measure(monitor, workArea, null, Rectangle.Empty, false);

        Assert.Equal(new TaskbarLayout(expectedEdge, workArea), layout);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(48)]
    public void AutoHideStripPreservesAlreadyReservedWorkArea(int reservedWidth)
    {
        var monitor = new Rectangle(1920, -300, 1440, 900);
        var work = new Rectangle(1920 + reservedWidth, -300, 1440 - reservedWidth, 900);
        var strip = new Rectangle(1920, -300, 2, 900);

        var layout = TaskbarGeometry.Measure(monitor, work, TaskbarEdge.Left, strip, true);

        Assert.Equal(new TaskbarLayout(TaskbarEdge.Left, work), layout);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void AutoHiddenTaskbarExcludesOnlyItsVisibleOverlap(bool visible, int inset)
    {
        var monitor = new Rectangle(-1600, 100, 1600, 900);
        var topBar = new Rectangle(-1600, 54, 1600, 48);
        var available = new Rectangle(-1600, 100 + inset, 1600, 900 - inset);

        var layout = TaskbarGeometry.Measure(monitor, monitor, TaskbarEdge.Top, topBar, visible);

        Assert.Equal(new TaskbarLayout(TaskbarEdge.Top, available), layout);
    }

    [Theory]
    [InlineData(TaskbarEdge.Left, 0, 0, 32, 800, 44, 300)]
    [InlineData(TaskbarEdge.Left, 0, 0, 128, 800, 140, 300)]
    [InlineData(TaskbarEdge.Top, 0, 0, 1200, 48, 450, 60)]
    [InlineData(TaskbarEdge.Right, 960, 0, 240, 800, 648, 300)]
    [InlineData(TaskbarEdge.Bottom, 0, 500, 1200, 300, 450, 288)]
    public void TaskbarThicknessChangesPositionAndPreservesRequestedPopupSize(
        TaskbarEdge edge, int x, int y, int width, int height, int popupX, int popupY)
    {
        var monitor = new Rectangle(0, 0, 1200, 800);
        var layout = TaskbarGeometry.Measure(monitor, monitor, edge, new Rectangle(x, y, width, height), true);
        var requestedSize = new Size(300, 200);

        var popup = PopupPlacement.Calculate(layout.WorkArea, layout.Edge, new Point(600, 400), requestedSize);

        Assert.Equal(new Rectangle(new Point(popupX, popupY), requestedSize), popup);
    }
}
