namespace FolderDock.Core;

/// Owns cancellation and latest-request-wins policy independently of a window.
/// All state is synchronized; no UI context is required by the operation or caller.
internal sealed class LatestRequestSession<TResult>
{
    private readonly object _sync = new();
    private Request? _pending;
    private long _requestId;

    public bool IsPending
    {
        get { lock (_sync) return _pending is not null; }
    }

    /// Returns null for a cancelled or superseded operation. The caller must check
    /// IsCurrent after awaiting, before presenting a completion on its UI thread.
    public async Task<Completion?> RunAsync(Func<CancellationToken, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Request? previous;
        Request request;
        lock (_sync)
        {
            previous = _pending;
            request = new Request(++_requestId);
            _pending = request;
        }
        // Cancellation can invoke operation callbacks. Never do that while holding
        // the session lock, or a callback could deadlock a concurrent reload.
        try
        {
            previous?.Cancel();
            var result = await operation(request.Token).ConfigureAwait(false);
            lock (_sync)
            {
                return request.Id == _requestId && !request.Token.IsCancellationRequested
                    ? new Completion(request.Id, result)
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

    public bool IsCurrent(long requestId)
    {
        lock (_sync) return requestId == _requestId;
    }

    public void Cancel()
    {
        Request? pending;
        lock (_sync)
        {
            ++_requestId;
            pending = _pending;
            _pending = null;
        }
        pending?.Cancel();
    }

    internal sealed record Completion(long RequestId, TResult Result);

    private sealed class Request : IDisposable
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = new();
        private bool _isCancelling;
        private bool _disposeRequested;
        private bool _disposed;

        public long Id { get; }
        public CancellationToken Token { get; }

        public Request(long id)
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
                // An operation callback may wait for RunAsync to finish. Cancellation
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
