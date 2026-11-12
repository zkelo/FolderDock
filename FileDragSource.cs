using System;
using System.Linq;
using System.Runtime.InteropServices;
using FolderDock.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace FolderDock;

/// <summary>Hands mouse drags directly to the file-transfer adapter, without a competing WinUI drag.</summary>
internal sealed class FileDragSource : IDisposable
{
    private const int HorizontalDragMetric = 68;
    private const int VerticalDragMetric = 69;
    private const uint DefaultDpi = 96;

    private readonly ListViewBase _view;
    private readonly nint _owner;
    private readonly Action<string> _dragRequested;
    private readonly FileDragGesture _gesture = new();
    private readonly PointerEventHandler _pressed;
    private readonly PointerEventHandler _moved;
    private readonly PointerEventHandler _ended;
    private readonly KeyEventHandler _keyDown;
    private UIElement? _pressContainer;
    private Pointer? _pressedPointer;
    private bool _ownsCapture;
    private bool _dragInProgress;
    private bool _changingCapture;
    private bool _disposed;

    public FileDragSource(ListViewBase view, nint owner, Action<string> dragRequested)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(dragRequested);
        _view = view;
        _owner = owner;
        _dragRequested = dragRequested;
        _pressed = OnPointerPressed;
        _moved = OnPointerMoved;
        _ended = OnPointerEnded;
        _keyDown = OnKeyDown;
        _view.AddHandler(UIElement.PointerPressedEvent, _pressed, true);
        _view.AddHandler(UIElement.PointerMovedEvent, _moved, true);
        _view.AddHandler(UIElement.PointerReleasedEvent, _ended, true);
        _view.AddHandler(UIElement.PointerCanceledEvent, _ended, true);
        _view.AddHandler(UIElement.PointerCaptureLostEvent, _ended, true);
        _view.AddHandler(UIElement.KeyDownEvent, _keyDown, true);
    }

    public bool SuppressClick => _gesture.SuppressClick;

    public void Reset()
    {
        _gesture.Reset();
        ReleaseOwnedCapture();
        _pressContainer = null;
        _pressedPointer = null;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_disposed || _dragInProgress) return;
        Reset();
        _gesture.Begin(e.Pointer.PointerId, null, 0, 0, 0, 0);
        // Native OLE dragging accepts mouse input; leave touch and pen gestures
        // to WinUI so ordinary taps and scrolling keep their existing behavior.
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;

        var point = e.GetCurrentPoint(_view);
        if (!point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
            return;
        if (FindContainer(e.OriginalSource as DependencyObject) is not { } container ||
            _view.ItemFromContainer(container) is not FolderEntry entry)
            return;

        var (horizontal, vertical) = GetDragThresholds();
        _gesture.Begin(e.Pointer.PointerId, entry.FullPath,
            point.Position.X, point.Position.Y, horizontal, vertical);
        _pressContainer = container;
        _pressedPointer = e.Pointer;
        // Reuse the item control's capture. If its template did not capture,
        // capture on that same item, keeping ordinary item clicks intact.
        CaptureIfNeeded(container, e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_disposed || _dragInProgress || _pressedPointer?.PointerId != e.Pointer.PointerId)
            return;

        var point = e.GetCurrentPoint(_view);
        var path = _gesture.TryStart(e.Pointer.PointerId, point.Position.X, point.Position.Y,
            point.Properties.IsLeftButtonPressed);
        if (!point.Properties.IsLeftButtonPressed)
        {
            Reset();
            return;
        }
        if (path is null) return;

        e.Handled = true;
        _dragInProgress = true;
        try
        {
            // CaptureLost is synchronous and may reenter this adapter. The
            // gesture is already consumed and click suppression is retained.
            ReleaseCaptureForDrag();
            _dragRequested(path);
        }
        finally
        {
            Reset();
            _dragInProgress = false;
        }
    }

    private void OnPointerEnded(object sender, PointerRoutedEventArgs e)
    {
        if (_dragInProgress || _changingCapture ||
            _pressedPointer?.PointerId != e.Pointer.PointerId) return;
        _gesture.Cancel(e.Pointer.PointerId);
        Reset();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_dragInProgress && e.Key == VirtualKey.Escape) Reset();
    }

    private UIElement? FindContainer(DependencyObject? source)
    {
        for (var current = source; current is not null && current != _view;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is ListViewItem or GridViewItem) return (UIElement)current;
        }
        return null;
    }

    private (double Horizontal, double Vertical) GetDragThresholds()
    {
        var dpi = GetDpiForWindow(_owner);
        if (dpi == 0) dpi = DefaultDpi;
        var scale = dpi / (double)DefaultDpi;
        // Win32 returns physical pixels; XAML pointer positions are logical pixels.
        return (Math.Max(1, Math.Abs(GetSystemMetricsForDpi(HorizontalDragMetric, dpi))) / scale,
            Math.Max(1, Math.Abs(GetSystemMetricsForDpi(VerticalDragMetric, dpi))) / scale);
    }

    private void CaptureIfNeeded(UIElement container, Pointer pointer)
    {
        if (container.PointerCaptures.Any(captured => captured.PointerId == pointer.PointerId))
            return;
        _changingCapture = true;
        try
        {
            _ownsCapture = container.CapturePointer(pointer);
        }
        finally
        {
            _changingCapture = false;
        }
    }

    private void ReleaseCaptureForDrag()
    {
        _ownsCapture = false;
        if (_pressedPointer is { } pointer)
            _pressContainer?.ReleasePointerCapture(pointer);
        ReleaseCapture();
    }

    private void ReleaseOwnedCapture()
    {
        if (!_ownsCapture) return;
        _ownsCapture = false;
        if (_pressedPointer is { } pointer)
            _pressContainer?.ReleasePointerCapture(pointer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _view.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
        _view.RemoveHandler(UIElement.PointerMovedEvent, _moved);
        _view.RemoveHandler(UIElement.PointerReleasedEvent, _ended);
        _view.RemoveHandler(UIElement.PointerCanceledEvent, _ended);
        _view.RemoveHandler(UIElement.PointerCaptureLostEvent, _ended);
        _view.RemoveHandler(UIElement.KeyDownEvent, _keyDown);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();
}
