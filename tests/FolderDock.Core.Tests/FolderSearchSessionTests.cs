using System.Collections.Concurrent;
using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderSearchSessionTests
{
    [Fact]
    public async Task NewQueryCancelsThePreviousSearchAndRejectsItsLateResult()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var first = session.SearchAsync("photos", "summer");
        var second = session.SearchAsync("photos", "winter");
        var requests = search.Requests.ToArray();
        var expected = Result("winter.jpg");

        Assert.True(requests[0].Token.IsCancellationRequested);
        requests[1].Complete(expected);
        var latest = await second;
        requests[0].Complete(Result("summer.jpg"));

        Assert.Null(await first);
        Assert.NotNull(latest);
        Assert.Same(expected, latest.Result);
        Assert.True(session.IsCurrent(latest));
        Assert.False(session.IsSearching);
    }

    [Fact]
    public async Task SearchingAnotherFolderSupersedesTheSameQuery()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var first = session.SearchAsync("photos", "sunset");
        var second = session.SearchAsync("photos/trip", "sunset");
        var requests = search.Requests.ToArray();

        Assert.Equal("photos/trip", requests[1].Folder);
        Assert.True(requests[0].Token.IsCancellationRequested);
        requests[0].Complete(Result("old.jpg"));
        Assert.Null(await first);
        Assert.True(session.IsSearching);
        requests[1].Complete(Result("new.jpg"));
        Assert.NotNull(await second);
    }

    [Fact]
    public async Task DismissalDiscardsAnAdapterThatIgnoresCancellation()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var searching = session.SearchAsync("photos", "sunset");

        session.Cancel();

        Assert.False(session.IsSearching);
        var request = Assert.Single(search.Requests);
        Assert.True(request.Token.IsCancellationRequested);
        request.Complete(Result("late.jpg"));
        Assert.Null(await searching);
    }

    [Fact]
    public async Task CancellationAfterCompletionInvalidatesTheUiReceipt()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var searching = session.SearchAsync("photos", "sunset");
        Assert.Single(search.Requests).Complete(Result("sunset.jpg"));
        var completion = await searching;
        Assert.NotNull(completion);

        session.Cancel();

        Assert.False(session.IsCurrent(completion));
    }

    [Fact]
    public async Task AdapterCancellationDoesNotBecomeAnError()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var searching = session.SearchAsync("photos", "sunset");
        session.Cancel();
        Assert.Single(search.Requests).CompleteCancellation();

        Assert.Null(await searching);
        Assert.False(session.IsSearching);
    }

    [Fact]
    public async Task UnexpectedAdapterFailuresRemainObservableAndReleaseTheSession()
    {
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, TimeSpan.Zero);
        var searching = session.SearchAsync("photos", "sunset");
        Assert.Single(search.Requests).Fail(new InvalidOperationException("adapter bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => searching);

        Assert.False(session.IsSearching);
    }

    [Fact]
    public async Task DebounceWaits180MillisecondsAndStartsOnlyTheLatestQuery()
    {
        var clock = new DebounceClock();
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, timeProvider: clock);
        var first = session.SearchAsync("photos", "sun");
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var second = session.SearchAsync("photos", "sunset");

        Assert.Null(await first);
        clock.Advance(TimeSpan.FromMilliseconds(179));
        Assert.Empty(search.Requests);
        Assert.True(session.IsSearching);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var request = await search.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("sunset", request.Query);
        request.Complete(Result("sunset.jpg"));

        Assert.NotNull(await second);
        Assert.Single(search.Requests);
    }

    [Fact]
    public async Task DismissalDuringDebounceDoesNotReadAnyFolder()
    {
        var clock = new DebounceClock();
        var search = new ControlledSearch();
        var session = new FolderSearchSession(search, timeProvider: clock);
        var searching = session.SearchAsync("photos", "sunset");

        session.Cancel();
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(await searching);
        Assert.Empty(search.Requests);
    }

    private static RecursiveSearchResult Result(string name) => new(
        new[] { new RecursiveSearchEntry(name, name, IsDirectory: false) },
        Failed: false, Incomplete: false, LimitReached: false);

    private sealed class ControlledSearch : IFolderSearch
    {
        public ConcurrentQueue<PendingSearch> Requests { get; } = new();
        public TaskCompletionSource<PendingSearch> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<RecursiveSearchResult> SearchAsync(string folder, string? query,
            CancellationToken cancellationToken = default)
        {
            var request = new PendingSearch(folder, query, cancellationToken);
            Requests.Enqueue(request);
            Started.TrySetResult(request);
            return request.Task;
        }
    }

    private sealed class PendingSearch(string folder, string? query, CancellationToken token)
    {
        private readonly TaskCompletionSource<RecursiveSearchResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Folder { get; } = folder;
        public string? Query { get; } = query;
        public CancellationToken Token { get; } = token;
        public Task<RecursiveSearchResult> Task => _completion.Task;
        public void Complete(RecursiveSearchResult result) => _completion.SetResult(result);
        public void CompleteCancellation() => _completion.SetCanceled(Token);
        public void Fail(Exception exception) => _completion.SetException(exception);
    }

    private sealed class DebounceClock : TimeProvider
    {
        private readonly List<OneShotTimer> _timers = new();
        private TimeSpan _elapsed;

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            var timer = new OneShotTimer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            _elapsed += elapsed;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
        }

        private sealed class OneShotTimer(DebounceClock clock, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan _dueAt;
            private volatile bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _dueAt = clock._elapsed + dueTime;
                return true;
            }

            public void FireIfDue()
            {
                if (_disposed || _dueAt > clock._elapsed) return;
                _disposed = true;
                callback(state);
            }

            public void Dispose() => _disposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
