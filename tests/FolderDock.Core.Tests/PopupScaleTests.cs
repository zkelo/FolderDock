using System.Drawing;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class PopupScaleTests
{
    [Theory]
    [InlineData(32, 1, 2.0 / 3)]
    [InlineData(48, 1, 1)]
    [InlineData(48, 1.5, 1)]
    [InlineData(72, 1.5, 1.5)]
    [InlineData(72, 1, 1.5)]
    [InlineData(96, 1, 2)]
    public void MeasuredThicknessIncludesDisplayScaling(int thickness, double displayScale, double expected)
    {
        Assert.Equal(expected, PopupScale.PixelsPerLayoutUnit(thickness, displayScale));
    }

    [Theory]
    [InlineData(null, 1.25, 1.25)]
    [InlineData(0, 1.5, 1.5)]
    [InlineData(-1, 2, 2)]
    [InlineData(null, 0, 1)]
    [InlineData(null, double.NaN, 1)]
    public void UnavailableThicknessUsesDisplayScale(int? thickness, double displayScale, double expected)
    {
        Assert.Equal(expected, PopupScale.PixelsPerLayoutUnit(thickness, displayScale));
    }

    [Theory]
    [InlineData(6.25, 850, 800, 2.5)]
    [InlineData(6.25, 1280, 450, 2.25)]
    [InlineData(1.5, 1280, 800, 1.5)]
    [InlineData(2.0 / 3, 1280, 800, 2.0 / 3)]
    public void LimitsScaleOnlyWhenMinimumContentWouldOverflow(double scale, int width, int height, double expected)
    {
        var fitted = PopupScale.FitToViewport(scale, new Size(width, height), new Size(340, 200));

        Assert.Equal(expected, fitted);
    }
}
