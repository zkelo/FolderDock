namespace FolderDock.Core;

internal static class NewYearSeason
{
    public static bool Contains(DateOnly localDate)
    {
        var seasonEnd = new DateOnly(localDate.Year, month: 1, day: 14);
        var seasonStart = new DateOnly(localDate.Year, month: 12, day: 1);
        return localDate <= seasonEnd || localDate >= seasonStart;
    }
}
