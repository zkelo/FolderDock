using System;
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

    public double Scale => Native.GetDpiForWindow(Hwnd) / 96.0;

    public void Show()
    {
        AppWindow.Show();
        Native.SetForegroundWindow(Hwnd);
    }

    public void Hide() => AppWindow.Hide();

    public bool IsVisible => AppWindow.IsVisible;

    public void Resize(int width, int height) =>
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

    public void MoveToTaskbarArea()
    {
        Native.GetCursorPos(out var cursor);
        var workArea = DisplayArea.GetFromPoint(
            new Windows.Graphics.PointInt32(cursor.X, cursor.Y),
            DisplayAreaFallback.Nearest).WorkArea;
        var size = AppWindow.Size;

        var minX = workArea.X + 8;
        var maxX = Math.Max(minX, workArea.X + workArea.Width - size.Width - 8);
        var x = Math.Clamp(cursor.X - size.Width / 2, minX, maxX);
        var y = Math.Max(workArea.Y + 8, workArea.Y + workArea.Height - size.Height - 12);

        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    public DataTransferManager GetShareManager()
    {
        var interop = DataTransferManager.As<Native.IDataTransferManagerInterop>();
        var iid = Native.DataTransferManagerIid;
        var abi = interop.GetForWindow(Hwnd, ref iid);
        return WinRT.MarshalInterface<DataTransferManager>.FromAbi(abi);
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
