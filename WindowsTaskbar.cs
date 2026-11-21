using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using FolderDock.Core;

namespace FolderDock;

internal static class WindowsTaskbar
{
    private const uint AbmGetAutoHideBarEx = 0x0000000b;
    private static readonly TaskbarEdge[] AppBarEdges =
        [TaskbarEdge.Left, TaskbarEdge.Top, TaskbarEdge.Right, TaskbarEdge.Bottom];

    internal static TaskbarLayout Locate(Rectangle monitor, Rectangle workArea)
    {
        var taskbar = FindTaskbar(monitor);
        return TaskbarGeometry.Measure(monitor, workArea, taskbar?.Edge,
            taskbar?.Bounds ?? Rectangle.Empty, taskbar?.Visible == true);
    }

    private static (TaskbarEdge Edge, Rectangle Bounds, bool Visible)? FindTaskbar(Rectangle monitor)
    {
        var windows = FindTaskbars();
        if (windows.Count == 0)
            return null;

        var monitorRect = new ScreenRect(monitor);
        // An auto-hidden window can slide off its monitor or across a shared edge.
        // Explorer's appbar registration identifies its owning monitor even then.
        var query = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>(), Bounds = monitorRect };
        for (int i = 0; i < AppBarEdges.Length; i++)
        {
            query.Edge = (uint)i;
            var window = SHAppBarMessage(AbmGetAutoHideBarEx, ref query);
            if (windows.Contains(window))
                return (AppBarEdges[i], WindowBounds(window), IsWindowVisible(window));
        }

        var targetMonitor = MonitorFromRect(ref monitorRect, 0);
        foreach (var window in windows)
        {
            if (targetMonitor == 0 || MonitorFromWindow(window, 0) != targetMonitor || !IsWindowVisible(window))
                continue;

            var bounds = WindowBounds(window);
            if (bounds.Width > 0 && bounds.Height > 0 && monitor.IntersectsWith(bounds))
                return (TaskbarGeometry.FromTaskbarBounds(monitor, bounds), bounds, true);
        }

        return null;
    }

    private static List<nint> FindTaskbars()
    {
        var windows = new List<nint>();
        var primary = FindWindowExW(0, 0, "Shell_TrayWnd", null);
        if (primary != 0)
            windows.Add(primary);

        nint secondary = 0;
        while ((secondary = FindWindowExW(0, secondary, "Shell_SecondaryTrayWnd", null)) != 0)
            windows.Add(secondary);
        return windows;
    }

    private static Rectangle WindowBounds(nint window) =>
        GetWindowRect(window, out var rect)
            ? Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : Rectangle.Empty;

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenRect(Rectangle bounds)
    {
        public int Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public nint Window;
        public uint CallbackMessage;
        public uint Edge;
        public ScreenRect Bounds;
        public nint Parameter;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint FindWindowExW(nint parent, nint after, string className, string? title);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out ScreenRect bounds);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref ScreenRect bounds, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("shell32.dll")]
    private static extern nint SHAppBarMessage(uint message, ref AppBarData data);
}
