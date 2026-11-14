using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.ApplicationModel.DataTransfer;
using WinRT;

namespace FolderDock;

/// <summary>Bridges native Shell formats into the Windows Runtime drag package.</summary>
internal static class ShellDataPackage
{
    internal static void SetFile(DataPackage package, string path)
    {
        ArgumentNullException.ThrowIfNull(package);
        var data = ShellFileDataObject.Create(path);
        try
        {
            // The DataPackage retains its own COM reference to the native data
            // object, including when a destination reads it asynchronously.
            package.As<IDataObjectProvider>().SetDataObject(data);
        }
        finally
        {
            Marshal.ReleaseComObject(data);
        }
    }

    [ComImport, Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)] out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)] IDataObject data);
    }
}
