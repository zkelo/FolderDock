using System.Drawing;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class PopupPlacementTests
{
    private static readonly Rectangle WorkArea = new(-1500, -700, 1200, 800);
    private static readonly Point Anchor = new(-1000, -300);

    [Theory]
    [InlineData(0, 0, 1920, 1200, 60, 144, 600, 1170, 345, 834)]
    [InlineData(1920, -915, 1080, 1920, 40, 96, 2400, 985, 2230, 757)]
    [InlineData(-1920, -2160, 3840, 2160, 80, 192, -1200, -40, -1540, -484)]
    public void MixedDpiPopupStaysInItsMonitorUsingDesktopWorkAreaCoordinates(
        int x, int y, int width, int height, int taskbarHeight, int dpi,
        int clickX, int clickY, int expectedX, int expectedY)
    {
        var monitor = new Rectangle(x, y, width, height);
        var workArea = new Rectangle(x, y, width, height - taskbarHeight);
        var taskbarBounds = Rectangle.FromLTRB(x, workArea.Bottom, monitor.Right, monitor.Bottom);
        var taskbar = TaskbarGeometry.Measure(monitor, workArea, TaskbarEdge.Bottom, taskbarBounds, true);
        var desiredSize = new Size(340 * dpi / 96, 196 * dpi / 96);

        var popup = PopupPlacement.Calculate(taskbar.WorkArea, taskbar.Edge, new Point(clickX, clickY), desiredSize);

        Assert.Equal(new Rectangle(new Point(expectedX, expectedY), desiredSize), popup);
        Assert.True(workArea.Contains(popup));
    }

    [Theory]
    [InlineData(TaskbarEdge.Left, -1488, -350, -1488, -400)]
    [InlineData(TaskbarEdge.Top, -1100, -688, -1150, -688)]
    [InlineData(TaskbarEdge.Right, -512, -350, -612, -400)]
    [InlineData(TaskbarEdge.Bottom, -1100, -12, -1150, -112)]
    public void ResizeKeepsTheSameTaskbarEdgeAndClickAnchor(
        TaskbarEdge edge, int x, int y, int resizedX, int resizedY)
    {
        var bounds = PopupPlacement.Calculate(WorkArea, edge, Anchor, new Size(200, 100));
        var resized = PopupPlacement.Calculate(WorkArea, edge, Anchor, new Size(300, 200));

        Assert.Equal(new Rectangle(x, y, 200, 100), bounds);
        Assert.Equal(new Rectangle(resizedX, resizedY, 300, 200), resized);
    }

    [Theory]
    [InlineData(TaskbarEdge.Left, 100, 100, 112, 208)]
    [InlineData(TaskbarEdge.Right, 1100, 900, 888, 692)]
    [InlineData(TaskbarEdge.Top, 100, 100, 108, 212)]
    [InlineData(TaskbarEdge.Bottom, 1100, 900, 892, 688)]
    public void ClickNearCornerKeepsPopupInsideWorkArea(
        TaskbarEdge edge, int clickX, int clickY, int x, int y)
    {
        var bounds = PopupPlacement.Calculate(new Rectangle(100, 200, 1000, 600),
            edge, new Point(clickX, clickY), new Size(200, 100));

        Assert.Equal(new Rectangle(x, y, 200, 100), bounds);
    }

    [Theory]
    [InlineData(TaskbarEdge.Left, 12, 8, 80, 64)]
    [InlineData(TaskbarEdge.Top, 8, 12, 84, 60)]
    [InlineData(TaskbarEdge.Right, 8, 8, 80, 64)]
    [InlineData(TaskbarEdge.Bottom, 8, 8, 84, 60)]
    public void OversizedPopupFitsWithTaskbarAndScreenMargins(
        TaskbarEdge edge, int x, int y, int width, int height)
    {
        var work = new Rectangle(0, 0, 100, 80);
        var bounds = PopupPlacement.Calculate(work, edge, Point.Empty, new Size(2000, 3000));

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
        Assert.Equal(new Size(width, height), PopupPlacement.MaximumSize(work, edge));
    }

    [Theory]
    [InlineData(TaskbarEdge.Left)]
    [InlineData(TaskbarEdge.Top)]
    [InlineData(TaskbarEdge.Right)]
    [InlineData(TaskbarEdge.Bottom)]
    public void TinyWorkAreaStillFitsAPixel(TaskbarEdge edge)
    {
        var pixel = new Rectangle(-20, 40, 1, 1);
        Assert.Equal(pixel, PopupPlacement.Calculate(pixel, edge, Anchor, new Size(500, 500)));
    }
}
