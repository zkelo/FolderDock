namespace FolderDock.Core;

/// Owns cancellation and latest-request-wins policy independently of a window.
/// All state is synchronized; no UI context is required by the reader or caller.
public sealed class FolderLoadSession
{
    private readonly IFolderSnapshotReader _reader;
    private readonly object _sync = new();
    private LoadRequest? _pending;
    private long _requestId;

    public FolderLoadSession(IFolderSnapshotReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public bool IsLoading
    {
        get { lock (_sync) return _pending is not null; }
    }

    /// Returns null for a cancelled or superseded read. The caller must check
    /// IsCurrent after awaiting, before presenting a completion on its UI thread.
    public async Task<FolderLoadCompletion?> LoadAsync(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        LoadRequest? previous;
        LoadRequest request;
        lock (_sync)
        {
            previous = _pending;
            request = new LoadRequest(++_requestId);
            _pending = request;
        }
        // Cancellation can invoke reader callbacks. Never do that while holding
        // the session lock, or a callback could deadlock a concurrent reload.
        try
        {
            previous?.Cancel();
            var snapshot = await _reader.ReadAsync(folder, request.Token).ConfigureAwait(false);
            lock (_sync)
            {
                return request.Id == _requestId && !request.Token.IsCancellationRequested
                    ? new FolderLoadCompletion(request.Id, snapshot)
                    : null;
            }
        }
        catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_pending, request)) _pending = null;
            }
            request.Dispose();
        }
    }

    public bool IsCurrent(FolderLoadCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (_sync) return completion.RequestId == _requestId;
    }

    public void Cancel()
    {
        LoadRequest? pending;
        lock (_sync)
        {
            ++_requestId;
            pending = _pending;
            _pending = null;
        }
        pending?.Cancel();
    }

    private sealed class LoadRequest : IDisposable
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = new();
        private bool _isCancelling;
        private bool _disposeRequested;
        private bool _disposed;

        public long Id { get; }
        public CancellationToken Token { get; }

        public LoadRequest(long id)
        {
            Id = id;
            Token = _cancellation.Token;
        }

        public void Cancel()
        {
            lock (_sync)
            {
                if (_disposeRequested || _isCancelling) return;
                _isCancelling = true;
            }
            try
            {
                // A reader callback may wait for LoadAsync to finish. Cancellation
                // must not hold a lock needed by that operation's final cleanup.
                _cancellation.Cancel();
            }
            finally
            {
                bool dispose;
                lock (_sync)
                {
                    _isCancelling = false;
                    dispose = ClaimDisposal();
                }
                if (dispose) _cancellation.Dispose();
            }
        }

        public void Dispose()
        {
            bool dispose;
            lock (_sync)
            {
                _disposeRequested = true;
                dispose = ClaimDisposal();
            }
            if (dispose) _cancellation.Dispose();
        }

        private bool ClaimDisposal()
        {
            if (!_disposeRequested || _isCancelling || _disposed) return false;
            _disposed = true;
            return true;
        }
    }
}
