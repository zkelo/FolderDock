namespace FolderDock.Infrastructure;

/// Preserves launch order while the UI dispatcher is unavailable or rejecting
/// work. Only one caller drains the queue; dispatch never runs under its lock.
public sealed class ActivationQueue
{
    private readonly object _sync = new();
    private readonly Queue<string> _pending = new();
    private Func<string, bool>? _dispatch;
    private bool _isDraining;

    public void Enqueue(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        lock (_sync)
            _pending.Enqueue(arguments);
        Drain();
    }

    public void Start(Func<string, bool> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        lock (_sync)
        {
            if (_dispatch is not null)
                throw new InvalidOperationException("Activation dispatch has already started.");
            _dispatch = dispatch;
        }
        Drain();
    }

    private void Drain()
    {
        Func<string, bool> dispatch;
        lock (_sync)
        {
            if (_isDraining || _dispatch is null || _pending.Count == 0) return;
            _isDraining = true;
            dispatch = _dispatch;
        }

        try
        {
            while (true)
            {
                string arguments;
                lock (_sync)
                {
                    if (_pending.Count == 0)
                    {
                        _isDraining = false;
                        return;
                    }
                    arguments = _pending.Peek();
                }

                // A rejected scheduling attempt remains queued for the next
                // activation instead of silently losing the user's click.
                if (!dispatch(arguments))
                {
                    lock (_sync) _isDraining = false;
                    return;
                }
                lock (_sync) _pending.Dequeue();
            }
        }
        catch
        {
            lock (_sync) _isDraining = false;
            throw;
        }
    }
}
