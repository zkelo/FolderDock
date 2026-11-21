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
}
