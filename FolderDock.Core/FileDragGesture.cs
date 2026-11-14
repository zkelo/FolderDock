namespace FolderDock.Core;

/// <summary>Recognizes one drag from a pointer press without depending on an input framework.</summary>
public sealed class FileDragGesture
{
    private Press? _press;

    // Keep suppressing the release-generated click even if the view refreshes
    // or the native drag is cancelled. The adapter completes it after input unwinds.
    public bool SuppressClick { get; private set; }

    public void Begin(uint pointerId, string? path, double x, double y,
        double horizontalThreshold, double verticalThreshold)
    {
        Reset();
        SuppressClick = false;
        if (path is null) return;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(horizontalThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(verticalThreshold);
        _press = new Press(pointerId, path, x, y, horizontalThreshold, verticalThreshold);
    }

    public void BeginKeyboardActivation()
    {
        Reset();
        SuppressClick = false;
    }

    public string? TryStart(uint pointerId, double x, double y, bool isButtonPressed)
    {
        if (_press is not { } press || press.PointerId != pointerId) return null;
        if (!isButtonPressed)
        {
            Reset();
            return null;
        }

        if (Math.Abs(x - press.X) <= press.HorizontalThreshold &&
            Math.Abs(y - press.Y) <= press.VerticalThreshold)
            return null;

        _press = null;
        SuppressClick = true;
        return press.Path;
    }

    public void CompleteDrag() => SuppressClick = false;

    public void Cancel(uint pointerId)
    {
        if (_press?.PointerId == pointerId) Reset();
    }

    public void Reset() => _press = null;

    private sealed record Press(uint PointerId, string Path, double X, double Y,
        double HorizontalThreshold, double VerticalThreshold);
}
