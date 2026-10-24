using Xunit;

namespace FolderDock.Shell.Tests;

public sealed class FolderChangeMonitorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "FolderDock-monitor-tests-" + Guid.NewGuid().ToString("N"));

    public FolderChangeMonitorTests() => Directory.CreateDirectory(_folder);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoticesSourcesRemovedAfterTheDragHasReturned(bool recursive)
    {
        var sourceFolder = recursive
            ? Directory.CreateDirectory(Path.Combine(_folder, "nested")).FullName
            : _folder;
        var path = Path.Combine(sourceFolder, "moved.txt");
        File.WriteAllText(path, "source");
        using var changed = new ManualResetEventSlim();
        using var monitor = new FolderChangeMonitor(_folder, recursive, changed.Set);

        File.Delete(path);

        Assert.True(changed.Wait(TimeSpan.FromSeconds(10)), "Source deletion was not observed.");
    }

    [Fact]
    public void NoticesRenamedItems()
    {
        var path = Path.Combine(_folder, "old.txt");
        File.WriteAllText(path, "source");
        using var changed = new ManualResetEventSlim();
        using var monitor = new FolderChangeMonitor(_folder, false, changed.Set);

        File.Move(path, Path.Combine(_folder, "new.txt"));

        Assert.True(changed.Wait(TimeSpan.FromSeconds(10)), "Rename was not observed.");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
