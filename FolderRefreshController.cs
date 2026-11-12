using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FolderDock.Core;
using Microsoft.UI.Dispatching;

namespace FolderDock;

/// <summary>Coalesces filesystem notifications on the UI queue for the visible folder.</summary>
internal sealed class FolderRefreshController : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly IFolderChangeSource _changes;
    private readonly Action _refresh;
    private readonly Func<bool> _canRefresh;
    private readonly DispatcherQueueTimer _timer;
    private IDisposable? _subscription;
    private string? _folder;
    private bool _recursive;
    private int _version;
    private bool _disposed;

    public FolderRefreshController(DispatcherQueue queue, IFolderChangeSource changes,
        Action refresh, Func<bool> canRefresh)
    {
        _queue = queue;
        _changes = changes;
        _refresh = refresh;
        _canRefresh = canRefresh;
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.IsRepeating = false;
        _timer.Tick += OnTick;
    }

    public void Watch(string? folder, bool recursive)
    {
        if (_disposed) return;
        if (_folder == folder && _recursive == recursive)
            return;

        Stop();
        if (folder is null) return;
        _folder = folder;
        _recursive = recursive;
        _ = StartWatchingAsync(folder, recursive, _version);
    }

    private async Task StartWatchingAsync(string folder, bool recursive, int version)
    {
        try
        {
            // Opening a watcher can access a network path; keep that work off
            // the UI thread and dispose results belonging to an earlier view.
            var subscription = await Task.Run(() =>
                _changes.Watch(folder, recursive, () => QueueRefresh(version)));
            if (_disposed || version != _version)
            {
                subscription.Dispose();
                return;
            }
            _subscription = subscription;
            // Close the gap between the initial folder read and registration.
            QueueRefresh(version);
        }
        catch (FolderWatchException ex)
        {
            if (_disposed || version != _version) return;
            _folder = null;
            // Folder loading reports access failures separately. A failed watcher
            // must not prevent opening a folder or starting a drag.
            Debug.WriteLine($"FolderDock: folder monitoring unavailable: {ex.Message}");
        }
    }

    private void QueueRefresh(int version)
    {
        _queue.TryEnqueue(() =>
        {
            if (_disposed || version != _version) return;
            _timer.Stop();
            _timer.Start();
        });
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        // The owner refreshes after its native drag loop exits. Rebuilding the
        // collection inside that loop would invalidate the gesture source.
        if (!_disposed && _canRefresh()) _refresh();
    }

    public void Stop()
    {
        _version++;
        _timer.Stop();
        _subscription?.Dispose();
        _subscription = null;
        _folder = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _timer.Tick -= OnTick;
    }
}
