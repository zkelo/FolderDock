using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreDoesNotReferenceApplicationOrWindowsFrameworks()
    {
        var references = typeof(PopupSession).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            reference.Name == "FolderDock" ||
            reference.Name?.StartsWith("Microsoft.UI", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Microsoft.WinUI", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Microsoft.Windows", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Windows", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("WinRT", StringComparison.Ordinal) == true);
    }
}
