using System.Drawing;

namespace FolderDock.Core;

public static class TaskbarGeometry
{
    public static TaskbarLayout Measure(Rectangle monitor, Rectangle workArea,
        TaskbarEdge? edge, Rectangle bounds, bool visible)
    {
        // Another docked application can reserve space even on a monitor without a taskbar.
        if (edge is not TaskbarEdge resolvedEdge)
            return new TaskbarLayout(FromWorkArea(monitor, workArea), workArea);

        var available = visible ? ExcludeTaskbar(workArea, bounds, resolvedEdge) : workArea;
        return new TaskbarLayout(resolvedEdge, available);
    }

    public static TaskbarEdge FromTaskbarBounds(Rectangle monitor, Rectangle taskbar)
    {
        if (taskbar.Width >= taskbar.Height)
            return Math.Abs(taskbar.Top - monitor.Top) <= Math.Abs(taskbar.Bottom - monitor.Bottom)
                ? TaskbarEdge.Top : TaskbarEdge.Bottom;

        return Math.Abs(taskbar.Left - monitor.Left) <= Math.Abs(taskbar.Right - monitor.Right)
            ? TaskbarEdge.Left : TaskbarEdge.Right;
    }

    public static TaskbarEdge FromWorkArea(Rectangle monitor, Rectangle workArea)
    {
        int left = workArea.Left - monitor.Left;
        int top = workArea.Top - monitor.Top;
        int right = monitor.Right - workArea.Right;
        int bottom = monitor.Bottom - workArea.Bottom;
        int largest = Math.Max(Math.Max(left, right), Math.Max(top, bottom));

        if (largest <= 0 || largest == bottom) return TaskbarEdge.Bottom;
        if (largest == left) return TaskbarEdge.Left;
        return largest == top ? TaskbarEdge.Top : TaskbarEdge.Right;
    }

    private static Rectangle ExcludeTaskbar(Rectangle work, Rectangle taskbar, TaskbarEdge edge)
    {
        var overlap = Rectangle.Intersect(work, taskbar);
        if (overlap.Width <= 0 || overlap.Height <= 0)
            return work;

        return edge switch
        {
            TaskbarEdge.Left when overlap.Right < work.Right =>
                Rectangle.FromLTRB(overlap.Right, work.Top, work.Right, work.Bottom),
            TaskbarEdge.Top when overlap.Bottom < work.Bottom =>
                Rectangle.FromLTRB(work.Left, overlap.Bottom, work.Right, work.Bottom),
            TaskbarEdge.Right when overlap.Left > work.Left =>
                Rectangle.FromLTRB(work.Left, work.Top, overlap.Left, work.Bottom),
            TaskbarEdge.Bottom when overlap.Top > work.Top =>
                Rectangle.FromLTRB(work.Left, work.Top, work.Right, overlap.Top),
            _ => work
        };
    }
}
