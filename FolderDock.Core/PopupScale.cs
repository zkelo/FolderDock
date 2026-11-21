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
}
