using FolderDock.Core;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace FolderDock.Core.Tests;

/// Application rules own their ports; operating-system and persistence adapters
/// must depend on Core rather than becoming dependencies of those rules.
public sealed class ArchitectureTests
{
    [Fact]
    public void CoreDoesNotReferenceOuterLayersOrWindowsFrameworks()
    {
        var forbiddenReferences = typeof(PopupSession).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name == "FolderDock" ||
                name.StartsWith("FolderDock.", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.UI", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.WinUI", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.Windows", StringComparison.Ordinal) ||
                name.StartsWith("Windows", StringComparison.Ordinal) ||
                name.StartsWith("WinRT", StringComparison.Ordinal));

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void CoreDoesNotUseMachineStateOrSerializationAdapters()
    {
        using var assembly = File.OpenRead(typeof(PopupSession).Assembly.Location);
        using var reader = new PEReader(assembly);
        var metadata = reader.GetMetadataReader();

        var forbiddenTypes = metadata.TypeReferences.Select(handle =>
        {
            var type = metadata.GetTypeReference(handle);
            return (Namespace: metadata.GetString(type.Namespace), Name: metadata.GetString(type.Name));
        }).Where(type => IsAdapterType(type.Namespace, type.Name))
          .Select(type => $"{type.Namespace}.{type.Name}");

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void CoreDoesNotDeclareNativeOrComImports()
    {
        using var assembly = File.OpenRead(typeof(PopupSession).Assembly.Location);
        using var reader = new PEReader(assembly);
        var metadata = reader.GetMetadataReader();

        var nativeMethods = metadata.MethodDefinitions
            .Select(metadata.GetMethodDefinition)
            .Where(method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            .Select(method => metadata.GetString(method.Name));
        var comTypes = metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Where(type => (type.Attributes & TypeAttributes.Import) != 0)
            .Select(type => metadata.GetString(type.Name));

        Assert.Empty(nativeMethods);
        Assert.Empty(comTypes);
    }

    private static bool IsAdapterType(string typeNamespace, string name) =>
        InNamespace(typeNamespace, "Microsoft.Win32") ||
        InNamespace(typeNamespace, "System.Net") ||
        InNamespace(typeNamespace, "System.Management") ||
        InNamespace(typeNamespace, "System.Text.Json") ||
        InNamespace(typeNamespace, "System.Xml") ||
        InNamespace(typeNamespace, "System.IO.Enumeration") ||
        InNamespace(typeNamespace, "System.IO.Pipes") ||
        InNamespace(typeNamespace, "System.IO.MemoryMappedFiles") ||
        (typeNamespace == "System" && name is "Environment" or "Console" or "AppContext") ||
        (typeNamespace == "System.Runtime.InteropServices" && name is
            "Marshal" or "NativeLibrary" or "ComWrappers" or "RuntimeInformation") ||
        (typeNamespace == "System.Diagnostics" && name is "Process" or "ProcessStartInfo") ||
        (typeNamespace == "System.IO" && name is
            "File" or "Directory" or "FileInfo" or "DirectoryInfo" or "DriveInfo" or
            "FileSystemInfo" or "FileSystemWatcher" or "FileStream" or "EnumerationOptions" or
            "RandomAccess" or "FileSystemAclExtensions");

    private static bool InNamespace(string candidate, string expected) =>
        candidate == expected || candidate.StartsWith(expected + ".", StringComparison.Ordinal);
}
