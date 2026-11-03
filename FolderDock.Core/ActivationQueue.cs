namespace FolderDock.Core;

/// Buffers redirected launches until the initial launch has finished setting
/// up the UI. The dispatcher callback schedules work; it must not run it inline.
public sealed class ActivationQueue
{
    private readonly object _sync = new();
    private readonly Queue<string> _pending = new();
    private Action<string>? _dispatch;

    public void Enqueue(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        lock (_sync)
        {
            if (_dispatch is null)
                _pending.Enqueue(arguments);
            else
                _dispatch(arguments);
        }
    }

    public void Start(Action<string> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        lock (_sync)
        {
            if (_dispatch is not null)
                throw new InvalidOperationException("Activation dispatch has already started.");

            // Keep scheduling serialized with Enqueue so a launch arriving
            // during initialization cannot overtake previously buffered clicks.
            while (_pending.TryDequeue(out var arguments))
                dispatch(arguments);
            _dispatch = dispatch;
        }
    }
}
