using System;
using FolderDock.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FolderDock;

internal sealed class NewYearThemeController
{
    private static readonly DateOnly PreviewDate = new(2026, 12, 1);
    private readonly SettingsService _settings;
    private readonly DispatcherQueueTimer _midnightTimer;
    private NewYearThemeState _state;
    private event Action<NewYearThemeState>? StateChanged;

    public NewYearThemeController(SettingsService settings, DispatcherQueue queue)
    {
        _settings = settings;
        _midnightTimer = queue.CreateTimer();
        _midnightTimer.IsRepeating = false;
        _midnightTimer.Tick += (_, _) => Refresh();
    }

    public void Refresh()
    {
        var now = DateTimeOffset.Now;
        var nextMidnight = now.Date.AddDays(1).ToUniversalTime();
        _midnightTimer.Stop();
        _midnightTimer.Interval = nextMidnight - now.UtcDateTime;
        _midnightTimer.Start();

        var state = _settings.Current.GetNewYearThemeState(PreviewDate);
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(state);
    }

    public void Attach(Window window, NewYearThemeButton button, UIElement decoration,
        Action? buttonVisibilityChanged = null)
    {
        void Render(NewYearThemeState state)
        {
            var previousVisibility = button.Visibility;
            button.Render(state);
            decoration.Visibility = state == NewYearThemeState.Enabled ? Visibility.Visible : Visibility.Collapsed;
            if (button.Visibility != previousVisibility) buttonVisibilityChanged?.Invoke();
        }

        void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated) Refresh();
        }

        void OnClosed(object sender, WindowEventArgs args)
        {
            StateChanged -= Render;
            button.Click -= OnToggle;
            window.Activated -= OnActivated;
            window.Closed -= OnClosed;
        }

        Render(_state);
        StateChanged += Render;
        button.Click += OnToggle;
        window.Activated += OnActivated;
        window.Closed += OnClosed;
    }

    private void OnToggle(object sender, RoutedEventArgs args)
    {
        App.ReportSettingsError(_settings.ToggleNewYearTheme(PreviewDate).Error);
        Refresh();
    }
}
