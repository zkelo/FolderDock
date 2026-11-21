using System.Drawing;

namespace FolderDock.Core;

public static class PopupScale
{
    private const double StandardTaskbarThicknessInDips = 48;

    public static double PixelsPerLayoutUnit(int? taskbarThickness, double displayScale)
    {
        // A thickness measured in physical pixels already includes display scaling.
        if (taskbarThickness is > 0)
            return taskbarThickness.Value / StandardTaskbarThicknessInDips;

        return double.IsFinite(displayScale) && displayScale > 0 ? displayScale : 1;
    }

    public static double FitToViewport(double scale, Size availablePixels, Size minimumLayoutSize)
    {
        double widthLimit = Math.Max(1, availablePixels.Width) / (double)Math.Max(1, minimumLayoutSize.Width);
        double heightLimit = Math.Max(1, availablePixels.Height) / (double)Math.Max(1, minimumLayoutSize.Height);
        return Math.Min(scale, Math.Min(widthLimit, heightLimit));
    }
}
