namespace FolderDock.Core;

public readonly record struct PopupDismissalContext(
    bool IsActive,
    bool IsVisible,
    bool IsShareInProgress,
    bool IsShellMenuOpen,
    TimeSpan TimeSinceShown,
    bool IsDragInProgress = false);
