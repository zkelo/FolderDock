using System.Drawing;

namespace FolderDock.Core;

public static class TaskbarGeometry
{
    // Explorer can report only the narrow activation strip of an auto-hidden taskbar.
    private const int MinimumTaskbarThickness = 8;

    public static TaskbarLayout Measure(Rectangle monitor, Rectangle workArea,
        TaskbarEdge? edge, Rectangle bounds, bool visible)
    {
        // Another docked application can reserve space even on a monitor without a taskbar.
        if (edge is not TaskbarEdge resolvedEdge)
            return new TaskbarLayout(FromWorkArea(monitor, workArea), workArea, null);

        int thickness = resolvedEdge is TaskbarEdge.Left or TaskbarEdge.Right ? bounds.Width : bounds.Height;
        if (thickness < MinimumTaskbarThickness)
            thickness = ReservedThickness(monitor, workArea, resolvedEdge);

        var available = visible ? ExcludeTaskbar(workArea, bounds, resolvedEdge) : workArea;
        return new TaskbarLayout(resolvedEdge, available, thickness >= MinimumTaskbarThickness ? thickness : null);
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
        int left = ReservedThickness(monitor, workArea, TaskbarEdge.Left);
        int top = ReservedThickness(monitor, workArea, TaskbarEdge.Top);
        int right = ReservedThickness(monitor, workArea, TaskbarEdge.Right);
        int bottom = ReservedThickness(monitor, workArea, TaskbarEdge.Bottom);
        int largest = Math.Max(Math.Max(left, right), Math.Max(top, bottom));

        if (largest <= 0 || largest == bottom) return TaskbarEdge.Bottom;
        if (largest == left) return TaskbarEdge.Left;
        return largest == top ? TaskbarEdge.Top : TaskbarEdge.Right;
    }

    private static int ReservedThickness(Rectangle monitor, Rectangle workArea, TaskbarEdge edge) => edge switch
    {
        TaskbarEdge.Left => workArea.Left - monitor.Left,
        TaskbarEdge.Top => workArea.Top - monitor.Top,
        TaskbarEdge.Right => monitor.Right - workArea.Right,
        _ => monitor.Bottom - workArea.Bottom
    };

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
