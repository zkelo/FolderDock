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
        Native.SetForegroundWindow(Hwnd);
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

    private static Anchor ReadAnchor(Point click, Anchor? previous = null)
    {
        var display = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(click.X, click.Y),
            DisplayAreaFallback.Nearest);
        var outer = display.OuterBounds;
        var work = display.WorkArea;
        var monitor = new Rectangle(outer.X, outer.Y, outer.Width, outer.Height);
        // WorkArea is relative to this monitor. AppWindow uses desktop coordinates.
        var workArea = new Rectangle(outer.X + work.X, outer.Y + work.Y, work.Width, work.Height);
        // Keep the captured taskbar still while auto-hide animates. Refresh the
        // snapshot if a display is disconnected, resized or its work area changes.
        if (previous is not null && previous.Monitor == monitor && previous.WorkArea == workArea)
            return previous;

        return new Anchor(click, monitor, workArea, WindowsTaskbar.Locate(monitor, workArea));
    }

    private Anchor CurrentAnchor() => _anchor = ReadAnchor(_anchor?.Click ?? CursorPoint(), _anchor);

    public int AvailableLayoutWidth(int desiredWidth)
    {
        var anchor = CurrentAnchor();
        var scale = PixelsPerLayoutUnit(anchor);
        var bounds = Measure(anchor, desiredWidth, 1, scale);
        return Math.Max(1, (int)Math.Floor(bounds.Width / scale));
    }

    public SizeF FitToTaskbar(int layoutWidth, int layoutHeight)
    {
        var anchor = CurrentAnchor();
        var scale = PixelsPerLayoutUnit(anchor);
        var bounds = Measure(anchor, layoutWidth, layoutHeight, scale);
        MoveAndResize(bounds);
        // A move between monitors can change the window DPI. This matters when
        // the shell could not supply a taskbar thickness for the target screen.
        var targetScale = PixelsPerLayoutUnit(anchor);
        if (targetScale != scale)
        {
            scale = targetScale;
            bounds = Measure(anchor, layoutWidth, layoutHeight, scale);
            MoveAndResize(bounds);
        }
        return new SizeF((float)(bounds.Width / scale), (float)(bounds.Height / scale));
    }

    private double PixelsPerLayoutUnit(Anchor anchor) =>
        PopupScale.PixelsPerLayoutUnit(anchor.Taskbar.Thickness, Native.GetDpiForWindow(Hwnd) / 96.0);

    private static Rectangle Measure(Anchor anchor, int width, int height, double scale)
    {
        var requested = new Size((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        return PopupPlacement.Calculate(anchor.Taskbar.WorkArea, anchor.Taskbar.Edge, anchor.Click, requested);
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
