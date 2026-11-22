using System.Runtime.InteropServices.ComTypes;
using Windows.ApplicationModel.DataTransfer;
using WinRT;

namespace FolderDock.Shell.Tests;

/// <summary>Consumes the production WinRT package as an external native drop target would.</summary>
internal static class ShellTestDataObject
{
    static ShellTestDataObject() => ComWrappersSupport.InitializeComWrappers();

    // The caller owns the returned COM reference.
    internal static IDataObject Create(params string[] paths)
    {
        var package = new DataPackage();
        ShellDataPackage.SetFiles(package, paths);

        // SetFiles has released its local Shell RCW. A delayed drop target must
        // still be able to read the package and perform the requested operation.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        package.As<ShellDataPackage.IDataObjectProvider>().GetDataObject(out var data);
        GC.KeepAlive(package);
        return data;
    }
}
