using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderDock;

/// <summary>Offers an existing Shell item without resolving shortcuts or copying its contents.</summary>
internal static class ShellFileDataObject
{
    private static readonly Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid DataObjectId = new("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid DataObjectHandler = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");

    // The caller owns the returned COM reference. This remains independent of
    // WinUI so native tests can inspect both CF_HDROP and Shell IDList Array.
    internal static IDataObject Create(string path)
    {
        var itemId = ShellItemId;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(
            path, IntPtr.Zero, ref itemId, out var item));
        try
        {
            var handler = DataObjectHandler;
            var dataId = DataObjectId;
            item.BindToHandler(IntPtr.Zero, ref handler, ref dataId, out var data);
            return data;
        }
        finally
        {
            Marshal.ReleaseComObject(item);
        }
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        // Only the first IShellItem vtable slot is used.
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IDataObject data);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
}
