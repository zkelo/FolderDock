using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderLoadSessionTests
{
    [Fact]
    public async Task ReopeningTheSamePathRejectsTheOlderRead()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var first = session.LoadAsync("photos");
        var second = session.LoadAsync("photos");
        var expected = Snapshot("new.jpg");

        reader.Requests[1].Complete(expected);
        var latest = await second;
        reader.Requests[0].Complete(Snapshot("old.jpg"));

        Assert.Null(await first);
        Assert.NotNull(latest);
        Assert.Same(expected, latest.Snapshot);
        Assert.True(session.IsCurrent(latest));
    }

    [Fact]
    public async Task NavigatingCancelsThePreviousReadAndAcceptsTheNewFolder()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var parent = session.LoadAsync("photos");
        var child = session.LoadAsync("photos/trip");

        Assert.True(reader.Requests[0].Token.IsCancellationRequested);
        Assert.Equal("photos/trip", reader.Requests[1].Folder);
        reader.Requests[0].Complete(Snapshot("parent.jpg"));
        Assert.Null(await parent);
        Assert.True(session.IsLoading);

        reader.Requests[1].Complete(Snapshot("trip.jpg"));
        Assert.NotNull(await child);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task ClosingDiscardsAReaderThatDoesNotHonorCancellation()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("photos");

        session.Cancel();

        Assert.False(session.IsLoading);
        Assert.True(reader.Requests[0].Token.IsCancellationRequested);
        reader.Requests[0].Complete(Snapshot("late.jpg"));
        Assert.Null(await loading);
    }

    [Fact]
    public async Task CancellationAfterReadCompletionStillInvalidatesTheUiReceipt()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("photos");
        reader.Requests[0].Complete(Snapshot("photo.jpg"));
        var completion = await loading;
        Assert.NotNull(completion);

        session.Cancel();

        Assert.False(session.IsCurrent(completion));
    }

    [Fact]
    public async Task AccessFailureIsAnActionableCompletionNotAStaleOrEmptySuccess()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("offline-share");
        reader.Requests[0].Complete(FolderSnapshot.Unavailable);

        var completion = await loading;

        Assert.NotNull(completion);
        Assert.True(completion.Snapshot.Failed);
        Assert.True(session.IsCurrent(completion));
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task ExpectedReaderCancellationDoesNotBecomeAnAccessError()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("photos");
        session.Cancel();
        reader.Requests[0].CompleteCancellation();

        Assert.Null(await loading);
    }

    [Fact]
    public async Task CancellationCallbackCanWaitForTheReadToFinishItsCleanup()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("photos");
        using var registration = reader.Requests[0].Token.Register(() =>
        {
            reader.Requests[0].CompleteCancellation();
            // A callback must not hold a session/request lock required by the
            // completion's finally block. This used to deadlock such readers.
            Assert.True(loading.Wait(TimeSpan.FromSeconds(5)));
        });

        await Task.Run(session.Cancel);

        Assert.Null(await loading);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task UnexpectedReaderFaultRemainsObservableAndReleasesLoadingState()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var loading = session.LoadAsync("photos");
        reader.Requests[0].Fail(new InvalidOperationException("reader bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => loading);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task ThrowingCancellationCallbackCannotStrandTheNextLoad()
    {
        var reader = new ControlledReader();
        var session = new FolderLoadSession(reader);
        var first = session.LoadAsync("photos");
        using var registration = reader.Requests[0].Token.Register(() =>
            throw new InvalidOperationException("callback bug"));

        await Assert.ThrowsAsync<AggregateException>(() => session.LoadAsync("next"));

        Assert.False(session.IsLoading);
        Assert.Single(reader.Requests);
        reader.Requests[0].Complete(Snapshot("old.jpg"));
        Assert.Null(await first);
    }

    private static FolderSnapshot Snapshot(string name) => new(
        new[] { new FolderSnapshotEntry(name, name, IsDirectory: false) }, Failed: false);

    private sealed class ControlledReader : IFolderSnapshotReader
    {
        public List<PendingRead> Requests { get; } = new();

        public Task<FolderSnapshot> ReadAsync(string folder, CancellationToken cancellationToken)
        {
            var request = new PendingRead(folder, cancellationToken);
            Requests.Add(request);
            return request.Task;
        }
    }

    private sealed class PendingRead(string folder, CancellationToken token)
    {
        private readonly TaskCompletionSource<FolderSnapshot> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Folder { get; } = folder;
        public CancellationToken Token { get; } = token;
        public Task<FolderSnapshot> Task => _completion.Task;
        public void Complete(FolderSnapshot snapshot) => _completion.SetResult(snapshot);
        public void CompleteCancellation() => _completion.SetCanceled(Token);
        public void Fail(Exception exception) => _completion.SetException(exception);
    }
}
