using System;
using System.Drawing;
using FolderDock.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace FolderDock;

public sealed class PopupChrome
{
    public IntPtr Hwnd { get; }
    public AppWindow AppWindow { get; }

    private sealed record Anchor(Point Click, Rectangle Monitor, Rectangle WorkArea, TaskbarLayout Taskbar);
    private Anchor? _anchor;

    public PopupChrome(Window window)
    {
        Hwnd = WindowNative.GetWindowHandle(window);
        AppWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Hwnd));

        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        HideFromAltTab();
        RoundCorners();
    }

    public void Show()
    {
        AppWindow.Show();
        LogState("AppWindow.Show returned");
        var foregroundSet = Native.SetForegroundWindow(Hwnd);
        if (PopupDiagnostics.IsEnabled)
            LogState("SetForegroundWindow returned", $"success={foregroundSet}");
    }

    public void Hide() => AppWindow.Hide();

    public bool IsVisible => AppWindow.IsVisible;

    // Capture the click and taskbar together, before sizing the loading view.
    public void AnchorToCursor() => _anchor = ReadAnchor(CursorPoint());

    private static Point CursorPoint()
    {
        Native.GetCursorPos(out var cursor);
        return new Point(cursor.X, cursor.Y);
    }

    private Anchor ReadAnchor(Point click, Anchor? previous = null)
    {
        var nativeBounds = WindowsTaskbar.ReadMonitor(click);
        // During a display change, the native monitor handle can become invalid.
        // Use the SDK's screen-space outer bounds and let taskbar detection trim them.
        var fallbackDisplay = nativeBounds is null ? FindDisplay(click) : null;
        var monitor = nativeBounds?.OuterBounds ?? ToRectangle(fallbackDisplay!.OuterBounds);
        var workArea = nativeBounds?.WorkArea ?? monitor;
        // Keep the captured taskbar still while auto-hide animates. Refresh the
        // snapshot if a display is disconnected, resized or its work area changes.
        if (previous is not null && previous.Monitor == monitor && previous.WorkArea == workArea)
            return previous;

        var taskbar = WindowsTaskbar.Locate(monitor, workArea);
        if (PopupDiagnostics.IsEnabled)
        {
            var display = fallbackDisplay ?? FindDisplay(click);
            var source = nativeBounds is null ? "DisplayArea" : "GetMonitorInfo";
            LogState("Anchor", $"click={click} OuterBounds={ToRectangle(display.OuterBounds)} " +
                $"WorkArea={ToRectangle(display.WorkArea)} monitorSource={source} " +
                $"desktopOuterBounds={monitor} desktopWorkArea={workArea} " +
                $"taskbarEdge={taskbar.Edge} taskbarWorkArea={taskbar.WorkArea}");
        }
        return new Anchor(click, monitor, workArea, taskbar);
    }

    private static DisplayArea FindDisplay(Point point) =>
        DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(point.X, point.Y), DisplayAreaFallback.Nearest);

    private static Rectangle ToRectangle(Windows.Graphics.RectInt32 bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private Anchor CurrentAnchor() => _anchor = ReadAnchor(_anchor?.Click ?? CursorPoint(), _anchor);

    public void FitToTaskbar(int layoutWidth, Func<int, int> measureHeight)
    {
        var anchor = CurrentAnchor();
        var dpi = Native.GetDpiForWindow(Hwnd);
        Arrange(anchor, dpi, layoutWidth, measureHeight);
        // Moving a window to another display can change both the DPI and the
        // bounds suggested by Windows. Remeasure the header at the new width.
        var targetDpi = Native.GetDpiForWindow(Hwnd);
        if (PopupDiagnostics.IsEnabled)
            LogState("FitToTaskbar after move", $"beforeDpi={dpi} afterDpi={targetDpi}");
        if (targetDpi != dpi)
            Arrange(anchor, targetDpi, layoutWidth, measureHeight);
    }

    private void Arrange(Anchor anchor, uint dpi, int width, Func<int, int> measureHeight)
    {
        var taskbar = anchor.Taskbar;
        var available = PopupPlacement.MaximumSize(taskbar.WorkArea, taskbar.Edge);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        var availableWidth = Math.Min(width, Math.Max(1, (int)Math.Floor(available.Width / scale)));
        var height = measureHeight(availableWidth);
        var requested = new Size((int)(width * scale), (int)(height * scale));
        var bounds = PopupPlacement.Calculate(taskbar.WorkArea, taskbar.Edge, anchor.Click, requested);
        if (PopupDiagnostics.IsEnabled)
            LogState("MoveAndResize target", $"layoutDpi={dpi} bounds={bounds}");
        MoveAndResize(bounds);
        LogState("MoveAndResize completed");
    }

    internal void LogState(string stage, string details = "")
    {
        if (!PopupDiagnostics.IsEnabled) return;

        Native.GetCursorPos(out var cursor);
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        PopupDiagnostics.Write(Hwnd, stage,
            $"cursor=({cursor.X},{cursor.Y}) foreground=0x{Native.GetForegroundWindow():X} " +
            $"dpi={Native.GetDpiForWindow(Hwnd)} visible={IsVisible} " +
            $"position=({position.X},{position.Y}) size=({size.Width},{size.Height}) {details}");
    }

    private void MoveAndResize(Rectangle bounds)
    {
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        if (position.X == bounds.X && position.Y == bounds.Y &&
            size.Width == bounds.Width && size.Height == bounds.Height)
            return;

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public DataTransferManager GetShareManager()
    {
        var interop = DataTransferManager.As<Native.IDataTransferManagerInterop>();
        var iid = Native.DataTransferManagerIid;
        var abi = interop.GetForWindow(Hwnd, ref iid);
        try
        {
            return WinRT.MarshalInterface<DataTransferManager>.FromAbi(abi);
        }
        finally
        {
            // GetForWindow возвращает указатель с ref-count +1; FromAbi
            // делает собственный AddRef и владение не забирает
            System.Runtime.InteropServices.Marshal.Release(abi);
        }
    }

    public void ShowShareUI()
    {
        var interop = DataTransferManager.As<Native.IDataTransferManagerInterop>();
        interop.ShowShareUIForWindow(Hwnd);
    }

    private void HideFromAltTab()
    {
        var style = Native.GetWindowLong(Hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(Hwnd, Native.GWL_EXSTYLE, style | Native.WS_EX_TOOLWINDOW);
    }

    private void RoundCorners()
    {
        var preference = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference, sizeof(int));
    }
}
