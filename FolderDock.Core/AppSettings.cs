namespace FolderDock.Core;

public sealed record AppSettings(bool AutoCheckUpdates = true, bool NewYearThemeEnabled = false)
{
    public NewYearThemeState GetNewYearThemeState(DateOnly localDate)
    {
        if (!IsNewYearSeason(localDate)) return NewYearThemeState.Hidden;
        return NewYearThemeEnabled ? NewYearThemeState.Enabled : NewYearThemeState.Available;
    }

    internal AppSettings ToggleNewYearTheme(DateOnly localDate) =>
        IsNewYearSeason(localDate) ? this with { NewYearThemeEnabled = !NewYearThemeEnabled } : this;

    private static bool IsNewYearSeason(DateOnly localDate) =>
        localDate.Month == 12 || (localDate.Month == 1 && localDate.Day <= 14);
}
