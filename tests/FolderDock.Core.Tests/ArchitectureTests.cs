using FolderDock.Core;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreDoesNotReferenceApplicationOrWindowsFrameworks()
    {
        var references = typeof(PopupSession).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            reference.Name is "FolderDock" or "FolderDock.Infrastructure" ||
            reference.Name?.StartsWith("Microsoft.UI", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Microsoft.WinUI", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Microsoft.Windows", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("Windows", StringComparison.Ordinal) == true ||
            reference.Name?.StartsWith("WinRT", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CoreDoesNotCallFilesystemOrRegistryAdapters()
    {
        using var assembly = File.OpenRead(typeof(PopupSession).Assembly.Location);
        using var reader = new PEReader(assembly);
        var metadata = reader.GetMetadataReader();
        var types = metadata.TypeReferences.Select(handle =>
        {
            var type = metadata.GetTypeReference(handle);
            return (Namespace: metadata.GetString(type.Namespace), Name: metadata.GetString(type.Name));
        });

        Assert.DoesNotContain(types, type =>
            type.Namespace.StartsWith("Microsoft.Win32", StringComparison.Ordinal) ||
            (type.Namespace == "System.IO" && type.Name is
                "File" or "Directory" or "FileInfo" or "DirectoryInfo" or
                "FileSystemInfo" or "FileSystemWatcher" or "FileStream" or "EnumerationOptions"));
    }
}
