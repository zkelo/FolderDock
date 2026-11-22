namespace FolderDock.Core;

public readonly record struct SelectionBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;

    public static SelectionBounds Between(double startX, double startY, double endX, double endY) =>
        new(Math.Min(startX, endX), Math.Min(startY, endY),
            Math.Max(startX, endX), Math.Max(startY, endY));

    public bool Intersects(SelectionBounds other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
