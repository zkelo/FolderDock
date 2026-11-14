namespace FolderDock.Core;

/// <summary>Состояние закрепления и правила закрытия одного сеанса попапа.</summary>
public sealed class PopupSession
{
    // Новый попап может кратковременно потерять активацию во время показа.
    private static readonly TimeSpan DismissGracePeriod = TimeSpan.FromMilliseconds(300);

    public bool IsPinned { get; private set; }

    public void Begin() => IsPinned = false;

    public void TogglePin() => IsPinned = !IsPinned;

    public bool ShouldDismiss(PopupDismissalReason reason, PopupDismissalContext context)
    {
        var allowedByEvent = reason switch
        {
            PopupDismissalReason.Explicit => true,
            PopupDismissalReason.Deactivated =>
                !context.IsActive &&
                !context.IsShareInProgress &&
                !context.IsShellMenuOpen &&
                context.TimeSinceShown > DismissGracePeriod,
            PopupDismissalReason.ShareCompleted => !context.IsActive && context.IsVisible,
            PopupDismissalReason.DragCompleted => !context.IsActive && context.IsVisible,
            PopupDismissalReason.ExternalLaunch => true,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };

        return allowedByEvent && (reason == PopupDismissalReason.Explicit ||
            (!IsPinned && !context.IsDragInProgress));
    }
}
