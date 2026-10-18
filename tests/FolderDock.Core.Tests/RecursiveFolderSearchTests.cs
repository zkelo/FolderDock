using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class RecursiveFolderSearchTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(), "FolderDock-search-tests-" + Guid.NewGuid().ToString("N"));

    public RecursiveFolderSearchTests() => Directory.CreateDirectory(_temporaryDirectory);

    [Fact]
    public void FindsFilesAndDirectoriesAtEveryDepth()
    {
        var root = CreateDirectory("root");
        var rootFile = CreateFile("root", "TARGET root.txt");
        var matchingDirectory = CreateDirectory("root", "target folder");
        var matchingNestedFile = CreateFile("root", "target folder", "Target child.txt");
        var deepestDirectory = CreateDirectory("root", "other", "nested", "TARGET deep");
        var deepestFile = CreateFile("root", "other", "nested", "TARGET deep", "target deepest.txt");

        var result = RecursiveFolderSearch.Search(root, "  tArGeT  ");

        Assert.Equal(new[] { matchingDirectory, deepestDirectory, matchingNestedFile, deepestFile, rootFile }
                .OrderBy(path => !Directory.Exists(path))
                .ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(path => path, StringComparer.Ordinal),
            result.Entries.Select(entry => entry.FullPath));
        Assert.Equal(2, result.Entries.Count(entry => entry.IsDirectory));
        Assert.False(result.Failed);
        Assert.False(result.Incomplete);
        Assert.False(result.LimitReached);
    }

    [Fact]
    public void ExtensionQueryMatchesRootAndDescendantsIgnoringCase()
    {
        var root = CreateDirectory("root");
        var first = CreateFile("root", "AnnualReport.TXT");
        var second = CreateFile("root", "subfolder", "Отчёт за октябрь.txt");
        CreateFile("root", "subfolder", "Photo.jpeg");

        var result = RecursiveFolderSearch.Search(root, ".tXt");

        Assert.Equal(new[] { first, second }.OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase),
            result.Entries.Select(entry => entry.FullPath));
        Assert.All(result.Entries, entry => Assert.False(entry.IsDirectory));
    }

    [Fact]
    public void MatchesOnlyEntryNamesAndStaysInsideSelectedDirectory()
    {
        var root = CreateDirectory("target root");
        CreateFile("target root", "unrelated.txt");
        CreateFile("sibling", "target outside.txt");
        var expected = CreateFile("target root", "nested", "target inside.txt");

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(expected, Assert.Single(result.Entries).FullPath);
    }

    [Fact]
    public void SortsDirectoriesFirstAndUsesFullPathToBreakEqualNameTies()
    {
        var root = CreateDirectory("root");
        var directory = CreateDirectory("root", "z target folder");
        var second = CreateFile("root", "b", "a target.txt");
        var first = CreateFile("root", "a", "a target.txt");
        var last = CreateFile("root", "z target.txt");

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(new[] { directory, first, second, last }, result.Entries.Select(entry => entry.FullPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void EmptyQueryReturnsEmptyWithoutOpeningDirectory(string? query)
    {
        var result = RecursiveFolderSearch.Search(Path.Combine(_temporaryDirectory, "missing"), query);

        Assert.Empty(result.Entries);
        Assert.False(result.Failed);
        Assert.False(result.Incomplete);
        Assert.False(result.LimitReached);
    }

    [Fact]
    public void MissingRootReportsFailure()
    {
        var result = RecursiveFolderSearch.Search(Path.Combine(_temporaryDirectory, "missing"), "target");

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
        Assert.False(result.Incomplete);
        Assert.False(result.LimitReached);
    }

    [Fact]
    public void FileUsedAsRootReportsFailure()
    {
        var file = CreateFile("file.txt");

        var result = RecursiveFolderSearch.Search(file, "target");

        Assert.Empty(result.Entries);
        Assert.True(result.Failed);
    }

    [Fact]
    public void LimitCountsMatchesAcrossDescendantsAfterNonmatchingEntries()
    {
        var root = CreateDirectory("root");
        for (var index = 0; index < FolderSearch.MaxResults + 20; index++)
            CreateFile("root", $"unrelated {index}.txt");
        for (var index = 0; index < FolderSearch.MaxResults + 20; index++)
            CreateFile("root", "nested", $"target {index}.txt");

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(FolderSearch.MaxResults, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Contains("target", entry.Name));
        Assert.True(result.LimitReached);
        Assert.False(result.Failed);
    }

    [Fact]
    public void ExactlyFiveHundredMatchesReportsLimitReached()
    {
        var root = CreateDirectory("root");
        for (var index = 0; index < FolderSearch.MaxResults; index++)
            CreateFile("root", $"target {index}.txt");

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(FolderSearch.MaxResults, result.Entries.Count);
        Assert.True(result.LimitReached);
    }

    [Fact]
    public void PreCancelledSearchThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => RecursiveFolderSearch.Search(
            _temporaryDirectory, "target", cancellation.Token));
    }

    [Fact]
    public void HiddenFilesAndDirectoryContentsAreSkippedOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateDirectory("root");
        var hiddenFile = CreateFile("root", "target hidden.txt");
        var hiddenDirectory = CreateDirectory("root", "target hidden folder");
        CreateFile("root", "target hidden folder", "target nested.txt");
        var visibleFile = CreateFile("root", "target visible.txt");
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
        File.SetAttributes(hiddenDirectory, File.GetAttributes(hiddenDirectory) | FileAttributes.Hidden);

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(visibleFile, Assert.Single(result.Entries).FullPath);
        Assert.False(result.Incomplete);
    }

    [Fact]
    public void DirectoryLinksAreReturnedWithoutFollowingOutsideOrLoops()
    {
        var root = CreateDirectory("root");
        var outside = CreateDirectory("outside");
        CreateFile("outside", "target outside.txt");
        var outsideLink = Path.Combine(root, "target link");
        var loopLink = Path.Combine(root, "target loop");
        try
        {
            Directory.CreateSymbolicLink(outsideLink, outside);
            Directory.CreateSymbolicLink(loopLink, root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                         or PlatformNotSupportedException)
        {
            // Windows may require Developer Mode or an elevated process to create links.
            return;
        }

        var result = RecursiveFolderSearch.Search(root, "target");

        Assert.Equal(new[] { outsideLink, loopLink }, result.Entries.Select(entry => entry.FullPath));
        Assert.All(result.Entries, entry => Assert.True(entry.IsDirectory));
        Assert.False(result.LimitReached);
        Assert.False(result.Incomplete);
    }

    private string CreateDirectory(params string[] parts)
    {
        var path = Path.Combine([_temporaryDirectory, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateFile(params string[] parts)
    {
        var path = Path.Combine([_temporaryDirectory, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    public void Dispose()
    {
        // Attributes can otherwise prevent cleanup on Windows.
        foreach (var path in Directory.EnumerateFileSystemEntries(_temporaryDirectory, "*",
                     new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.Hidden);
        Directory.Delete(_temporaryDirectory, true);
    }
}
