using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class FolderSearchTests
{
    [Fact]
    public void ResultLimitIsAppliedAfterFilteringAllEntries()
    {
        var entries = Enumerable.Range(0, 600)
            .Select(index => new Entry($"Other {index}.txt"))
            .Concat(Enumerable.Range(0, FolderSearch.MaxResults + 10)
                .Select(index => new Entry($"Target {index}.txt")))
            .ToArray();

        var results = FolderSearch.Filter(entries, "Target", entry => entry.Name);

        Assert.Equal(FolderSearch.MaxResults, results.Count);
        Assert.Same(entries[600], results[0]);
        Assert.Same(entries[600 + FolderSearch.MaxResults - 1], results[^1]);
    }

    [Fact]
    public void MatchingEntriesRetainSourceOrderAndObjectIdentity()
    {
        var first = new Entry("Zebra report.txt");
        var second = new Entry("Alpha report.txt");
        var entries = new[] { first, new Entry("Unrelated.txt"), second, first };

        var results = FolderSearch.Filter(entries, "report", entry => entry.Name);

        Assert.Collection(results,
            entry => Assert.Same(first, entry),
            entry => Assert.Same(second, entry),
            entry => Assert.Same(first, entry));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\r\n ")]
    public void ClearingQueryRestoresFirstFiveHundredEntries(string? query)
    {
        var entries = Enumerable.Range(0, FolderSearch.MaxResults + 10)
            .Select(index => new Entry($"File {index}.txt"))
            .ToArray();
        Assert.Single(FolderSearch.Filter(entries, "File 509.", entry => entry.Name));

        var results = FolderSearch.Filter(entries, query, entry => entry.Name);

        Assert.Equal(FolderSearch.MaxResults, results.Count);
        for (var index = 0; index < results.Count; index++)
            Assert.Same(entries[index], results[index]);
    }

    [Theory]
    [InlineData("AnnualReport.TXT", "rEp")]
    [InlineData("Отчёт за октябрь.txt", "оТчЁ")]
    [InlineData("Документы", "кУмЕн")]
    [InlineData("Photo.JPEG", ".jPe")]
    [InlineData("AnnualReport.TXT", "  report  ")]
    public void QueryMatchesPartialNamesAndExtensionsIgnoringCase(
        string name, string query)
    {
        var expected = new Entry(name);
        var entries = new[] { new Entry("Unrelated.bin"), expected };

        var results = FolderSearch.Filter(entries, query, entry => entry.Name);

        Assert.Same(expected, Assert.Single(results));
    }

    [Fact]
    public void ExtensionQueryMatchesLatinAndCyrillicFilenames()
    {
        var entries = new[]
        {
            new Entry("AnnualReport.TXT"),
            new Entry("Отчёт за октябрь.txt"),
            new Entry("Photo.jpeg")
        };

        var results = FolderSearch.Filter(entries, ".tXt", entry => entry.Name);

        Assert.Collection(results,
            entry => Assert.Same(entries[0], entry),
            entry => Assert.Same(entries[1], entry));
    }

    [Fact]
    public void UnmatchedQueryReturnsNoEntries()
    {
        var entries = new[] { new Entry("Report.txt"), new Entry("Документы") };

        var results = FolderSearch.Filter(entries, "missing", entry => entry.Name);

        Assert.Empty(results);
    }

    private sealed record Entry(string Name);
}
