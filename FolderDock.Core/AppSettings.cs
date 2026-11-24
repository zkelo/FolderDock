namespace FolderDock.Core;

public sealed record AppSettings(bool AutoCheckUpdates = true, bool NewYearThemeEnabled = false)
{
    public NewYearThemeState GetNewYearThemeState(DateOnly localDate)
    {
        if (!NewYearSeason.Contains(localDate)) return NewYearThemeState.Hidden;
        return NewYearThemeEnabled ? NewYearThemeState.Enabled : NewYearThemeState.Available;
    }

    internal AppSettings ToggleNewYearTheme(DateOnly localDate) =>
        NewYearSeason.Contains(localDate) ? this with { NewYearThemeEnabled = !NewYearThemeEnabled } : this;
}
