using System.Drawing;

namespace FolderDock.Core;

public static class PopupPlacement
{
    private const int ScreenEdgeMargin = 8;
    private const int TaskbarGap = 12;

    public static Size MaximumSize(Rectangle workArea, TaskbarEdge edge)
    {
        bool verticalTaskbar = edge is TaskbarEdge.Left or TaskbarEdge.Right;
        int width = workArea.Width - ScreenEdgeMargin - (verticalTaskbar ? TaskbarGap : ScreenEdgeMargin);
        int height = workArea.Height - ScreenEdgeMargin - (verticalTaskbar ? ScreenEdgeMargin : TaskbarGap);
        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    // All coordinates are physical pixels in the virtual desktop.
    public static Rectangle Calculate(Rectangle workArea, TaskbarEdge edge, Point anchor, Size requestedSize)
    {
        var maximum = MaximumSize(workArea, edge);
        int width = Math.Clamp(requestedSize.Width, 1, maximum.Width);
        int height = Math.Clamp(requestedSize.Height, 1, maximum.Height);
        int x = edge switch
        {
            TaskbarEdge.Left => workArea.Left + TaskbarGap,
            TaskbarEdge.Right => workArea.Right - TaskbarGap - width,
            _ => anchor.X - width / 2
        };
        int y = edge switch
        {
            TaskbarEdge.Top => workArea.Top + TaskbarGap,
            TaskbarEdge.Bottom => workArea.Bottom - TaskbarGap - height,
            _ => anchor.Y - height / 2
        };

        return new Rectangle(
            FitPosition(x, workArea.Left, workArea.Width, width),
            FitPosition(y, workArea.Top, workArea.Height, height),
            width, height);
    }

    private static int FitPosition(int position, int start, int available, int size)
    {
        int spare = Math.Max(0, available - size);
        int margin = Math.Min(ScreenEdgeMargin, spare / 2);
        return Math.Clamp(position, start + margin, start + spare - margin);
    }
}
