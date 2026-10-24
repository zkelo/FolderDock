using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderDock;

/// <summary>Transfers the actual Shell item, including shortcuts, without resolving its target.</summary>
internal static class ShellDragDrop
{
    private const uint CopyMoveLink = 1 | 2 | 4;
    private const int DragDropSucceeded = 0x00040100;
    private static readonly Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid DataObjectId = new("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid DataObjectHandler = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");

    /// <remarks>
    /// Call on the UI STA after WinUI has cancelled its own drag. The Shell owns
    /// modifier keys, drag feedback, cancellation and file operations. In particular,
    /// never delete a source ourselves: optimized moves can report a zero effect.
    /// </remarks>
    public static bool Start(IntPtr owner, string path)
    {
        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        try
        {
            var data = CreateDataObject(path);
            try
            {
                // A null IDropSource asks Windows to supply its standard implementation.
                // All three operations stay available throughout the gesture, so Ctrl,
                // Shift and Ctrl+Shift work even when pressed after dragging starts.
                var result = SHDoDragDrop(owner, data, IntPtr.Zero, CopyMoveLink, out _);
                Marshal.ThrowExceptionForHR(result);
                return result == DragDropSucceeded;
            }
            finally
            {
                Marshal.ReleaseComObject(data);
            }
        }
        finally
        {
            // S_FALSE from OleInitialize also needs a matching OleUninitialize.
            OleUninitialize();
        }
    }

    // Kept separate so Windows integration tests can inspect real Shell formats
    // without starting an interactive drag or changing any user files.
    internal static IDataObject CreateDataObject(string path)
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
        // Only the first vtable slot is used; the remaining IShellItem methods
        // are deliberately omitted, as they are not needed for a file drag.
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IDataObject data);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHDoDragDrop(IntPtr owner, IDataObject data,
        IntPtr source, uint allowedEffects, out uint performedEffect);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void OleUninitialize();
}
