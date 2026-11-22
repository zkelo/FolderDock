using System;
using System.Collections.Generic;
using System.Linq;
using FolderDock.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;

namespace FolderDock;

/// <summary>Selects folder entries by dragging across the background of an item view.</summary>
internal sealed class MarqueeSelection : IDisposable
{
    private const double ScrollEdge = 20;
    private const double ScrollStep = 16;

    private readonly Grid _host;
    private readonly Func<ListViewBase> _activeView;
    private readonly Rectangle _rectangle;
    private readonly FolderItemSelection _selection;
    private readonly nint _owner;
    private readonly PointerEventHandler _pressed;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(32) };
    private MarqueeSelectionSession<FolderEntry>? _session;
    private (double Horizontal, double Vertical) _dragThreshold;
    private ScrollViewer? _scroller;
    private Pointer? _pointer;
    private Point _pressPosition;
    private Point _cursor;
    private double _pressOffset;
    private bool _addToSelection;
    private bool _dragging;

    public bool IsTracking => _pointer is not null;

    public MarqueeSelection(Grid host, Func<ListViewBase> activeView, Rectangle rectangle,
        FolderItemSelection selection, nint owner)
    {
        _host = host;
        _activeView = activeView;
        _rectangle = rectangle;
        _selection = selection;
        _owner = owner;
        _pressed = OnPressed;
        _host.AddHandler(UIElement.PointerPressedEvent, _pressed, true);
        _host.PointerMoved += OnMoved;
        _host.PointerReleased += OnReleased;
        _host.PointerCanceled += OnCanceled;
        _host.PointerCaptureLost += OnCanceled;
        _scrollTimer.Tick += OnScrollTick;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer is not null || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;
        var point = e.GetCurrentPoint(_host);
        if (!point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed ||
            IsItemOrScrollBar(e.OriginalSource as DependencyObject) || !_host.CapturePointer(e.Pointer))
            return;

        var view = _activeView();
        _pointer = e.Pointer;
        _pressPosition = _cursor = point.Position;
        _addToSelection = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        _session = new MarqueeSelectionSession<FolderEntry>(_selection.Items, _addToSelection);
        _dragThreshold = MouseDragThreshold.ForWindow(_owner);
        _scroller = Ancestors(view.ItemsPanelRoot).OfType<ScrollViewer>().FirstOrDefault();
        _pressOffset = _scroller?.VerticalOffset ?? 0;
        if (_scroller is not null) _scroller.ViewChanged += OnViewChanged;
        e.Handled = true;
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            yield return element;
    }

    private static bool IsItemOrScrollBar(DependencyObject? source) =>
        Ancestors(source).Any(element => element is SelectorItem or ScrollBar);

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer?.PointerId != e.Pointer.PointerId) return;
        var point = e.GetCurrentPoint(_host);
        if (point.Properties.IsLeftButtonPressed)
            UpdatePointer(point.Position);
        else
            Cancel();
        e.Handled = true;
    }

    private void UpdatePointer(Point position)
    {
        _cursor = position;
        if (!_dragging && Math.Abs(_cursor.X - _pressPosition.X) <= _dragThreshold.Horizontal &&
            Math.Abs(_cursor.Y - _pressPosition.Y) <= _dragThreshold.Vertical) return;

        _dragging = true;
        UpdateRectangle();
        if (_scroller is not null && ScrollDirection != 0)
            _scrollTimer.Start();
        else
            _scrollTimer.Stop();
    }

    private int ScrollDirection => _cursor.Y < ScrollEdge ? -1 :
        _cursor.Y > _host.ActualHeight - ScrollEdge ? 1 : 0;

    private void OnScrollTick(object? sender, object e)
    {
        if (_scroller is null) return;
        var offset = Math.Clamp(_scroller.VerticalOffset + ScrollDirection * ScrollStep,
            0, _scroller.ScrollableHeight);
        if (offset == _scroller.VerticalOffset) _scrollTimer.Stop();
        else _scroller.ChangeView(null, offset, null, disableAnimation: true);
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_dragging) UpdateRectangle();
    }

    private void UpdateRectangle()
    {
        var offset = _scroller?.VerticalOffset ?? 0;
        var cursorX = Math.Clamp(_cursor.X, 0, _host.ActualWidth);
        var cursorY = Math.Clamp(_cursor.Y, 0, _host.ActualHeight) + offset;
        var anchorY = _pressPosition.Y + _pressOffset;
        var bounds = SelectionBounds.Between(_pressPosition.X, anchorY, cursorX, cursorY);

        var top = Math.Clamp(bounds.Top - offset, 0, _host.ActualHeight);
        Canvas.SetLeft(_rectangle, bounds.Left);
        Canvas.SetTop(_rectangle, top);
        _rectangle.Width = bounds.Width;
        _rectangle.Height = Math.Clamp(bounds.Bottom - offset, 0, _host.ActualHeight) - top;
        _rectangle.Visibility = Visibility.Visible;

        // Remember each realized item in content coordinates so items scrolled
        // offscreen can remain selected, or be deselected when the band shrinks.
        var view = _activeView();
        if (view.ItemsPanelRoot is not { } panel || _session is null) return;
        foreach (var container in panel.Children.OfType<SelectorItem>())
        {
            if (view.ItemFromContainer(container) is not FolderEntry entry ||
                container.ActualWidth == 0 || container.ActualHeight == 0) continue;
            var origin = container.TransformToVisual(_host).TransformPoint(default);
            var topInContent = origin.Y + offset;
            _session.RecordBounds(entry, new SelectionBounds(origin.X, topInContent,
                origin.X + container.ActualWidth, topInContent + container.ActualHeight));
        }

        _selection.Replace(_session.ItemsIn(bounds));
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer?.PointerId != e.Pointer.PointerId) return;
        var point = e.GetCurrentPoint(_host);
        if (point.Properties.IsLeftButtonPressed) return;
        UpdatePointer(point.Position);
        if (!_dragging && !_addToSelection) _selection.Replace(Array.Empty<FolderEntry>());
        EndGesture();
        e.Handled = true;
    }

    private void OnCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer?.PointerId == e.Pointer.PointerId) Cancel();
    }

    public bool Cancel()
    {
        if (_pointer is null) return false;
        if (_dragging && _session is not null) _selection.Replace(_session.PreviousSelection);
        EndGesture();
        return true;
    }

    private void EndGesture()
    {
        var pointer = _pointer;
        _pointer = null;
        _dragging = false;
        _scrollTimer.Stop();
        if (_scroller is not null) _scroller.ViewChanged -= OnViewChanged;
        _scroller = null;
        _session = null;
        _rectangle.Visibility = Visibility.Collapsed;
        if (pointer is not null) _host.ReleasePointerCapture(pointer);
    }

    public void Dispose()
    {
        Cancel();
        _scrollTimer.Tick -= OnScrollTick;
        _host.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
        _host.PointerMoved -= OnMoved;
        _host.PointerReleased -= OnReleased;
        _host.PointerCanceled -= OnCanceled;
        _host.PointerCaptureLost -= OnCanceled;
    }
}
