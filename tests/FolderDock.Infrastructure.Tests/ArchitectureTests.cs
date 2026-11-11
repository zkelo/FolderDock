using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

/// Infrastructure implements Core ports. The WinUI application composes those
/// adapters; neither the application nor UI frameworks may flow back into them.
public sealed class ArchitectureTests
{
    [Fact]
    public void InfrastructureDependsOnCoreWithoutReferencingPresentation()
    {
        var references = typeof(FileSystemFolderSnapshotReader).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("FolderDock.Core", references);
        Assert.DoesNotContain(references, name =>
            name == "FolderDock" ||
            (name.StartsWith("FolderDock.", StringComparison.Ordinal) && name != "FolderDock.Core") ||
            name.StartsWith("Microsoft.UI", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.WinUI", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.Windows", StringComparison.Ordinal) ||
            name.StartsWith("Windows", StringComparison.Ordinal) ||
            name.StartsWith("WinRT", StringComparison.Ordinal));
    }
}
