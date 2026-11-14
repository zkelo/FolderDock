using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class FolderChangeSourceTests : IDisposable
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(10);
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "FolderDock-watch-tests-" + Guid.NewGuid().ToString("N"));
    private readonly IFolderChangeSource _source = new FileSystemFolderChangeSource();

    public FolderChangeSourceTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservesFilesRemovedAfterTheFileOperationReturns(bool recursive)
    {
        var folder = recursive
            ? Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName
            : _root;
        var path = Path.Combine(folder, "moved.txt");
        File.WriteAllText(path, "source");
        var changed = NewNotification();
        using var subscription = _source.Watch(_root, recursive, () => changed.TrySetResult());

        File.Delete(path);

        await changed.Task.WaitAsync(NotificationTimeout);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservesMovedDirectoriesIncludingNestedSearchResults(bool recursive)
    {
        var folder = recursive
            ? Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName
            : _root;
        var path = Directory.CreateDirectory(Path.Combine(folder, "moved-folder")).FullName;
        var changed = NewNotification();
        using var subscription = _source.Watch(_root, recursive, () => changed.TrySetResult());

        Directory.Delete(path);

        await changed.Task.WaitAsync(NotificationTimeout);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task ObservesRenamedEntries()
    {
        var original = Path.Combine(_root, "old.txt");
        var renamed = Path.Combine(_root, "new.txt");
        File.WriteAllText(original, "source");
        var changed = NewNotification();
        using var subscription = _source.Watch(_root, false, () => changed.TrySetResult());

        File.Move(original, renamed);

        await changed.Task.WaitAsync(NotificationTimeout);
        Assert.Equal("source", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task ObservesNewEntries()
    {
        var changed = NewNotification();
        using var subscription = _source.Watch(_root, false, () => changed.TrySetResult());
        var path = Path.Combine(_root, "new.txt");

        File.WriteAllText(path, "new");

        await changed.Task.WaitAsync(NotificationTimeout);
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public async Task SubscriptionReportsActiveStateUntilDisposalAndIgnoresLaterChanges()
    {
        var changed = NewNotification();
        var subscription = _source.Watch(_root, true, () => changed.TrySetResult());
        Assert.True(subscription.IsWatching);

        subscription.Dispose();
        subscription.Dispose();

        Assert.False(subscription.IsWatching);

        File.WriteAllText(Path.Combine(_root, "after-disposal.txt"), "new");

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(changed.Task.IsCompleted);
    }

    [Fact]
    public async Task FailedWatchDoesNotPreventObservingAnotherFolder()
    {
        var exception = Assert.Throws<FolderWatchException>(() =>
            _source.Watch(Path.Combine(_root, "missing"), false, () => { }));
        Assert.NotNull(exception.InnerException);
        var changed = NewNotification();
        using var subscription = _source.Watch(_root, false, () => changed.TrySetResult());

        Directory.CreateDirectory(Path.Combine(_root, "new-folder"));

        await changed.Task.WaitAsync(NotificationTimeout);
    }

    [Fact]
    public void FileInsteadOfFolderReportsTypedFailure()
    {
        var path = Path.Combine(_root, "file.txt");
        File.WriteAllText(path, "source");

        Assert.Throws<FolderWatchException>(() => _source.Watch(path, false, () => { }));
    }

    private static TaskCompletionSource NewNotification() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
