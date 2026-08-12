using System;

namespace FolderDock;

/// Чистая математика размеров попапа (без зависимостей от UI).
/// Константы связаны с шаблонами PopupWindow.xaml:
/// плитка сетки — StackPanel Width="92" + отступы GridView (≈ 100×96),
/// строка списка — Image 20px + Padding (≈ 36 по высоте).
/// Меняя шаблон в XAML, обнови соответствующую константу здесь.
public static class PopupLayout
{
    private const int GridCellWidth = 100;
    private const int GridCellHeight = 96;
    private const int GridChromeWidth = 40; // Padding GridView + рамка окна
    private const int HeaderHeight = 60;    // строка заголовка с кнопками
    private const int MinColumns = 3;
    private const int MaxColumns = 6;
    private const int MaxRows = 5;

    private const int ListWidth = 340;
    private const int ListRowHeight = 36;
    private const int MaxListRows = 14;

    public static (int Width, int Height) Measure(int itemCount, bool gridMode) =>
        gridMode ? Grid(itemCount) : List(itemCount);

    private static (int Width, int Height) Grid(int count)
    {
        var columns = Math.Clamp(
            (int)Math.Ceiling(Math.Sqrt(Math.Max(count, 1))), MinColumns, MaxColumns);
        var rows = Math.Clamp((int)Math.Ceiling(count / (double)columns), 1, MaxRows);
        return (columns * GridCellWidth + GridChromeWidth,
                rows * GridCellHeight + HeaderHeight);
    }

    private static (int Width, int Height) List(int count) =>
        (ListWidth, Math.Clamp(count, 1, MaxListRows) * ListRowHeight + HeaderHeight);
}
