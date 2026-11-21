using System.Drawing;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class PopupPlacementTests
{
    private static readonly Rectangle WorkArea = new(-1500, -700, 1200, 800);
    private static readonly Point Anchor = new(-1000, -300);

    [Theory]
    [InlineData(TaskbarEdge.Left,   -1488, -350, -1488, -400)]
    [InlineData(TaskbarEdge.Top,    -1100, -688, -1150, -688)]
    [InlineData(TaskbarEdge.Right,   -512, -350,  -612, -400)]
    [InlineData(TaskbarEdge.Bottom, -1100,  -12, -1150, -112)]
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
    [InlineData(TaskbarEdge.Left,   12,  8, 80, 64)]
    [InlineData(TaskbarEdge.Top,     8, 12, 84, 60)]
    [InlineData(TaskbarEdge.Right,   8,  8, 80, 64)]
    [InlineData(TaskbarEdge.Bottom,  8,  8, 84, 60)]
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
