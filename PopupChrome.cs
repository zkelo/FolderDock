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
        // Якорь — точка клика по значку панели задач, зафиксированная в момент
        // показа попапа. Навигация по папкам вызывает Reload → MoveToTaskbarArea,
        // когда курсор уже ВНУТРИ попапа: без якоря окно «уезжало» за курсором
        // (и клампом прижималось к левому краю экрана).
        var anchor = _anchor ?? CursorPoint();
        var workArea = DisplayArea.GetFromPoint(anchor, DisplayAreaFallback.Nearest).WorkArea;
        var size = AppWindow.Size;

        var minX = workArea.X + 8;
        var maxX = Math.Max(minX, workArea.X + workArea.Width - size.Width - 8);
        var x = Math.Clamp(anchor.X - size.Width / 2, minX, maxX);
        var y = Math.Max(workArea.Y + 8, workArea.Y + workArea.Height - size.Height - 12);

        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    /// Зафиксировать точку привязки попапа по текущей позиции курсора.
    /// Вызывать при показе (клик по значку панели задач), до Reload.
    public void AnchorToCursor() => _anchor = CursorPoint();

    private Windows.Graphics.PointInt32? _anchor;

    private static Windows.Graphics.PointInt32 CursorPoint()
    {
        Native.GetCursorPos(out var cursor);
        return new Windows.Graphics.PointInt32(cursor.X, cursor.Y);
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
