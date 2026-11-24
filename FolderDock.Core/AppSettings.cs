namespace FolderDock.Core;

public sealed record AppSettings(bool AutoCheckUpdates = true, int? NewYearThemeSeason = null)
{
    public NewYearThemeState GetNewYearThemeState(DateOnly localDate)
    {
        if (GetSeason(localDate) is not int season) return NewYearThemeState.Hidden;
        return NewYearThemeSeason == season ? NewYearThemeState.Enabled : NewYearThemeState.Available;
    }

    internal AppSettings ToggleNewYearTheme(DateOnly localDate)
    {
        if (GetSeason(localDate) is not int season) return this;
        return this with { NewYearThemeSeason = NewYearThemeSeason == season ? null : season };
    }

    private static int? GetSeason(DateOnly localDate) => localDate.Month switch
    {
        12 => localDate.Year,
        1 when localDate.Day <= 14 => localDate.Year - 1,
        _ => null
    };
}
