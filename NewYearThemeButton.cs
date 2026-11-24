using FolderDock.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace FolderDock;

public sealed partial class NewYearThemeButton : ToggleButton
{
    private const string Snowflake = "\u2744";
    private const string UndoArrow = "\u21B6";

    private readonly FontIcon _icon = new()
    {
        FontFamily = new FontFamily("Segoe UI Symbol"),
        FontSize = 16
    };

    public NewYearThemeButton()
    {
        Visibility = Visibility.Collapsed;
        Content = _icon;
    }

    internal void Render(NewYearThemeState state)
    {
        bool enabled = state == NewYearThemeState.Enabled;
        IsChecked = enabled;
        Visibility = state == NewYearThemeState.Hidden ? Visibility.Collapsed : Visibility.Visible;
        if (state == NewYearThemeState.Hidden) return;

        _icon.Glyph = enabled ? UndoArrow : Snowflake;
        var action = Loc.Get(enabled ? "Tooltip_DisableNewYearTheme" : "Tooltip_EnableNewYearTheme");
        ToolTipService.SetToolTip(this, action);
        AutomationProperties.SetName(this, action);
    }
}
