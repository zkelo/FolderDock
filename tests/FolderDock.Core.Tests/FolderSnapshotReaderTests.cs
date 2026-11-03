using System.Globalization;
using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderSnapshotReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "FolderDock-snapshot-tests-" + Guid.NewGuid().ToString("N"));

    public FolderSnapshotReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ReadsOnlyDirectChildrenWithDirectoriesFirstAndCultureAwareNames()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var lastDirectory = CreateDirectory("z folder");
            var firstDirectory = CreateDirectory("A folder");
            var lastFile = CreateFile("z.txt");
            var firstFile = CreateFile("a.txt");
            File.WriteAllText(Path.Combine(firstDirectory, "not-a-direct-child.txt"), "");

            var result = FolderSnapshotReader.Read(_root);

            Assert.False(result.Failed);
            Assert.Equal(new[] { firstDirectory, lastDirectory, firstFile, lastFile },
                result.Entries.Select(entry => entry.FullPath));
            Assert.Equal(new[] { true, true, false, false }, result.Entries.Select(entry => entry.IsDirectory));
            Assert.Equal(new[] { "A folder", "z folder", "a.txt", "z.txt" },
                result.Entries.Select(entry => entry.Name));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void EmptyFolderIsSuccessful()
    {
        var result = FolderSnapshotReader.Read(_root);

        Assert.Empty(result.Entries);
        Assert.False(result.Failed);
    }

    [Fact]
    public void MissingFolderReportsFailure()
    {
        var result = FolderSnapshotReader.Read(Path.Combine(_root, "missing"));

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Fact]
    public void FileUsedAsFolderReportsFailure()
    {
        var result = FolderSnapshotReader.Read(CreateFile("file.txt"));

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public void InvalidPathReportsFailure(string path)
    {
        var result = FolderSnapshotReader.Read(path);

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Fact]
    public void CancelledReadThrowsInsteadOfReportingAnAccessError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            FolderSnapshotReader.Read(Path.Combine(_root, "missing"), cancellation.Token));
    }

    [Fact]
    public void ReadsAllEntriesBeforeTheUiAppliesItsDisplayLimit()
    {
        for (var index = FolderSearch.MaxResults + 10; index >= 0; index--)
            CreateFile($"item {index:D4}.txt");

        var result = FolderSnapshotReader.Read(_root);

        Assert.False(result.Failed);
        Assert.Equal(FolderSearch.MaxResults + 11, result.Entries.Count);
        Assert.Equal("item 0000.txt", result.Entries[0].Name);
        Assert.Equal($"item {FolderSearch.MaxResults + 10:D4}.txt", result.Entries[^1].Name);
    }

    [Fact]
    public void WindowsHiddenEntriesAreExcludedButSystemEntriesRemainVisible()
    {
        if (!OperatingSystem.IsWindows()) return;
        var hiddenFile = CreateFile("hidden.txt");
        var hiddenDirectory = CreateDirectory("hidden folder");
        var systemFile = CreateFile("system.txt");
        var visibleFile = CreateFile("visible.txt");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);
        File.SetAttributes(hiddenDirectory, FileAttributes.Directory | FileAttributes.Hidden);
        File.SetAttributes(systemFile, FileAttributes.System);

        var result = FolderSnapshotReader.Read(_root);

        Assert.False(result.Failed);
        Assert.Equal(new[] { systemFile, visibleFile }, result.Entries.Select(entry => entry.FullPath));
    }

    [Fact]
    public void DirectoryLinkIsListedWithoutReadingItsTargetContents()
    {
        var target = CreateDirectory("target");
        File.WriteAllText(Path.Combine(target, "nested.txt"), "");
        var link = Path.Combine(_root, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                         or PlatformNotSupportedException)
        {
            return;
        }

        var result = FolderSnapshotReader.Read(_root);

        Assert.False(result.Failed);
        Assert.Equal(new[] { link, target }, result.Entries.Select(entry => entry.FullPath));
        Assert.All(result.Entries, entry => Assert.True(entry.IsDirectory));
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "");
        return path;
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*",
                     new EnumerationOptions { AttributesToSkip = 0 }))
            File.SetAttributes(path, File.GetAttributes(path) & ~(FileAttributes.Hidden | FileAttributes.System));
        Directory.Delete(_root, recursive: true);
    }
}
