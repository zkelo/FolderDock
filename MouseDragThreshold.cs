using System;
using System.Runtime.InteropServices;

namespace FolderDock;

internal static class MouseDragThreshold
{
    private const int HorizontalDragMetric = 68;
    private const int VerticalDragMetric = 69;
    private const uint DefaultDpi = 96;

    public static (double Horizontal, double Vertical) ForWindow(nint window)
    {
        var dpi = Native.GetDpiForWindow(window);
        if (dpi == 0) dpi = DefaultDpi;
        var scale = dpi / (double)DefaultDpi;
        // Win32 returns physical pixels; XAML pointer positions are logical pixels.
        return (Math.Max(1, Math.Abs(GetSystemMetricsForDpi(HorizontalDragMetric, dpi))) / scale,
            Math.Max(1, Math.Abs(GetSystemMetricsForDpi(VerticalDragMetric, dpi))) / scale);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
