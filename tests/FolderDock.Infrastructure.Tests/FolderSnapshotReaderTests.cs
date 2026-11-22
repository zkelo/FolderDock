using System.Globalization;
using FolderDock.Core;
using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class FolderSnapshotReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "FolderDock-snapshot-tests-" + Guid.NewGuid().ToString("N"));

    public FolderSnapshotReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadsOnlyDirectChildrenWithDirectoriesFirstAndCultureAwareNames()
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

            var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root);

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
    public async Task DateAddedOrdersFoldersThenFilesByCreationTimeWithinEachGroup()
    {
        if (!OperatingSystem.IsWindows()) return;
        var olderFolder = CreateDirectory("a folder");
        var newerFolder = CreateDirectory("z folder");
        var olderFile = CreateFile("a.txt");
        var newerFile = CreateFile("z.txt");
        var day = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Directory.SetCreationTimeUtc(olderFolder, day);
        Directory.SetCreationTimeUtc(newerFolder, day.AddDays(1));
        File.SetCreationTimeUtc(olderFile, day.AddDays(2));
        File.SetCreationTimeUtc(newerFile, day.AddDays(3));
        Directory.SetLastWriteTimeUtc(olderFolder, day.AddDays(1));
        Directory.SetLastWriteTimeUtc(newerFolder, day);
        File.SetLastWriteTimeUtc(olderFile, day.AddDays(1));
        File.SetLastWriteTimeUtc(newerFile, day);

        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root, FolderSortOrder.DateAdded);

        Assert.Equal(new[] { newerFolder, olderFolder, newerFile, olderFile },
            result.Entries.Select(entry => entry.FullPath));
    }

    [Fact]
    public async Task DateModifiedOrdersFoldersThenFilesBeforeTheDisplayLimitAndBreaksTiesByName()
    {
        var oldDate = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var olderFolder = CreateDirectory("a folder");
        var newerFolder = CreateDirectory("z folder");
        Directory.SetLastWriteTimeUtc(olderFolder, oldDate.AddDays(-2));
        Directory.SetLastWriteTimeUtc(newerFolder, oldDate);
        var tiedFileB = CreateFile("b.txt");
        var tiedFileA = CreateFile("a.txt");
        File.SetLastWriteTimeUtc(tiedFileB, oldDate);
        File.SetLastWriteTimeUtc(tiedFileA, oldDate);
        for (var index = 0; index < FolderSearch.MaxResults; index++)
            File.SetLastWriteTimeUtc(CreateFile($"item {index:D4}.txt"), oldDate.AddDays(-1));
        var newest = CreateFile("z last by name.txt");
        File.SetLastWriteTimeUtc(newest, oldDate.AddDays(1));

        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root, FolderSortOrder.DateModified);

        var expected = new[] { newerFolder, olderFolder, newest, tiedFileA, tiedFileB };
        Assert.Equal(expected, result.Entries.Take(expected.Length).Select(entry => entry.FullPath));
        Assert.Equal(FolderSearch.MaxResults + expected.Length, result.Entries.Count);
    }

    [Fact]
    public async Task EmptyFolderIsSuccessful()
    {
        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root);

        Assert.Empty(result.Entries);
        Assert.False(result.Failed);
    }

    [Fact]
    public async Task MissingFolderReportsFailure()
    {
        var result = await new FileSystemFolderSnapshotReader().ReadAsync(Path.Combine(_root, "missing"));

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Fact]
    public async Task FileUsedAsFolderReportsFailure()
    {
        var result = await new FileSystemFolderSnapshotReader().ReadAsync(CreateFile("file.txt"));

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public async Task InvalidPathReportsFailure(string path)
    {
        var result = await new FileSystemFolderSnapshotReader().ReadAsync(path);

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Fact]
    public async Task CancelledReadThrowsInsteadOfReportingAnAccessError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileSystemFolderSnapshotReader().ReadAsync(Path.Combine(_root, "missing"),
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ReadsAllEntriesBeforeTheUiAppliesItsDisplayLimit()
    {
        for (var index = FolderSearch.MaxResults + 10; index >= 0; index--)
            CreateFile($"item {index:D4}.txt");

        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root);

        Assert.False(result.Failed);
        Assert.Equal(FolderSearch.MaxResults + 11, result.Entries.Count);
        Assert.Equal("item 0000.txt", result.Entries[0].Name);
        Assert.Equal($"item {FolderSearch.MaxResults + 10:D4}.txt", result.Entries[^1].Name);
    }

    [Fact]
    public async Task WindowsHiddenEntriesAreExcludedButSystemEntriesRemainVisible()
    {
        if (!OperatingSystem.IsWindows()) return;
        var hiddenFile = CreateFile("hidden.txt");
        var hiddenDirectory = CreateDirectory("hidden folder");
        var systemFile = CreateFile("system.txt");
        var visibleFile = CreateFile("visible.txt");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);
        File.SetAttributes(hiddenDirectory, FileAttributes.Directory | FileAttributes.Hidden);
        File.SetAttributes(systemFile, FileAttributes.System);

        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root);

        Assert.False(result.Failed);
        Assert.Equal(new[] { systemFile, visibleFile }, result.Entries.Select(entry => entry.FullPath));
    }

    [Fact]
    public async Task DirectoryLinkIsListedWithoutReadingItsTargetContents()
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

        var result = await new FileSystemFolderSnapshotReader().ReadAsync(_root);

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
