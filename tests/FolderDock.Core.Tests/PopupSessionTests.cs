using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class PopupSessionTests
{
    private static readonly PopupDismissalContext InactiveVisiblePopup = new(
        IsActive: false,
        IsVisible: true,
        IsShareInProgress: false,
        IsShellMenuOpen: false,
        TimeSinceShown: TimeSpan.FromSeconds(1));

    [Fact]
    public void NewSessionStartsUnpinned()
    {
        var session = new PopupSession();

        Assert.False(session.IsPinned);
    }

    [Fact]
    public void PinCanBeToggledOnAndOffWithinSession()
    {
        var session = new PopupSession();

        session.TogglePin();
        Assert.True(session.IsPinned);

        session.TogglePin();
        Assert.False(session.IsPinned);
    }

    [Fact]
    public void ReopeningStartsUnpinnedEvenWhenPreviousSessionWasPinned()
    {
        var session = new PopupSession();
        session.Begin();
        session.TogglePin();

        session.Begin();

        Assert.False(session.IsPinned);
        Assert.True(session.ShouldDismiss(
            PopupDismissalReason.Deactivated, InactiveVisiblePopup));
    }

    [Theory]
    [InlineData(PopupDismissalReason.Deactivated)]
    [InlineData(PopupDismissalReason.ShareCompleted)]
    [InlineData(PopupDismissalReason.ExternalLaunch)]
    [InlineData(PopupDismissalReason.DragCompleted)]
    public void PinKeepsPopupOpenForEveryAutomaticDismissalReason(
        PopupDismissalReason reason)
    {
        var session = new PopupSession();
        session.TogglePin();

        Assert.False(session.ShouldDismiss(reason, InactiveVisiblePopup));
        Assert.True(session.IsPinned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitDismissalWorksDespitePinAndAutomaticDismissalBlockers(
        bool pinned)
    {
        var session = new PopupSession();
        if (pinned)
            session.TogglePin();
        var context = new PopupDismissalContext(
            IsActive: true,
            IsVisible: false,
            IsShareInProgress: true,
            IsShellMenuOpen: true,
            TimeSinceShown: TimeSpan.FromMilliseconds(-1));

        Assert.True(session.ShouldDismiss(PopupDismissalReason.Explicit, context));
    }

    [Theory]
    [InlineData(false, false, false, 301, true)]
    [InlineData(true, false, false, 301, false)]
    [InlineData(false, true, false, 301, false)]
    [InlineData(false, false, true, 301, false)]
    [InlineData(false, true, true, 301, false)]
    [InlineData(false, false, false, 299, false)]
    [InlineData(false, false, false, 0, false)]
    public void DeactivationRespectsActivationAndTransientInteractionState(
        bool isActive,
        bool isShareInProgress,
        bool isShellMenuOpen,
        int elapsedMilliseconds,
        bool expectedDismissal)
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            IsActive = isActive,
            IsShareInProgress = isShareInProgress,
            IsShellMenuOpen = isShellMenuOpen,
            TimeSinceShown = TimeSpan.FromMilliseconds(elapsedMilliseconds)
        };

        Assert.Equal(expectedDismissal,
            session.ShouldDismiss(PopupDismissalReason.Deactivated, context));
    }

    [Fact]
    public void DeactivationAtExactlyThreeHundredMillisecondsKeepsPopupOpen()
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            TimeSinceShown = TimeSpan.FromMilliseconds(300)
        };

        Assert.False(session.ShouldDismiss(PopupDismissalReason.Deactivated, context));
    }

    [Fact]
    public void NegativeElapsedTimeDoesNotCauseImmediateDeactivationDismissal()
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            TimeSinceShown = TimeSpan.FromSeconds(-1)
        };

        Assert.False(session.ShouldDismiss(PopupDismissalReason.Deactivated, context));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void ShareCompletionClosesOnlyAnInactiveVisiblePopup(
        bool isActive,
        bool isVisible,
        bool expectedDismissal)
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            IsActive = isActive,
            IsVisible = isVisible,
            TimeSinceShown = TimeSpan.Zero
        };

        Assert.Equal(expectedDismissal,
            session.ShouldDismiss(PopupDismissalReason.ShareCompleted, context));
    }

    [Fact]
    public void OpeningExternalContentDismissesUnpinnedPopupDuringActivationGracePeriod()
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            IsActive = true,
            TimeSinceShown = TimeSpan.Zero
        };

        Assert.True(session.ShouldDismiss(PopupDismissalReason.ExternalLaunch, context));
    }

    [Theory]
    [InlineData(PopupDismissalReason.Deactivated)]
    [InlineData(PopupDismissalReason.ShareCompleted)]
    [InlineData(PopupDismissalReason.ExternalLaunch)]
    [InlineData(PopupDismissalReason.DragCompleted)]
    public void ActiveDragBlocksEveryAutomaticDismissal(PopupDismissalReason reason)
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with { IsDragInProgress = true };

        Assert.False(session.ShouldDismiss(reason, context));
    }

    [Fact]
    public void ExplicitDismissalStillWorksDuringDrag()
    {
        var session = new PopupSession();
        session.TogglePin();

        Assert.True(session.ShouldDismiss(PopupDismissalReason.Explicit,
            InactiveVisiblePopup with { IsDragInProgress = true }));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void DragCompletionReevaluatesCurrentFocusAndVisibility(
        bool isActive, bool isVisible, bool expectedDismissal)
    {
        var session = new PopupSession();
        var context = InactiveVisiblePopup with
        {
            IsActive = isActive,
            IsVisible = isVisible,
            TimeSinceShown = TimeSpan.Zero
        };

        Assert.Equal(expectedDismissal,
            session.ShouldDismiss(PopupDismissalReason.DragCompleted, context));
    }

    [Fact]
    public void CancelledDragAllowsSubsequentNormalDeactivation()
    {
        var session = new PopupSession();
        Assert.False(session.ShouldDismiss(PopupDismissalReason.Deactivated,
            InactiveVisiblePopup with { IsDragInProgress = true }));

        Assert.False(session.ShouldDismiss(PopupDismissalReason.DragCompleted,
            InactiveVisiblePopup with { IsActive = true }));
        Assert.True(session.ShouldDismiss(PopupDismissalReason.Deactivated,
            InactiveVisiblePopup));
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue)]
    public void UnknownDismissalReasonsAreRejectedEvenWhenPinned(
        bool pinned,
        int invalidReason)
    {
        var session = new PopupSession();
        if (pinned)
            session.TogglePin();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.ShouldDismiss((PopupDismissalReason)invalidReason, InactiveVisiblePopup));
    }
}
