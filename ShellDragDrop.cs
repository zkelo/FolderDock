using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderDock;

/// <summary>Transfers Shell items without resolving shortcuts or managing file operations.</summary>
internal sealed class ShellDragDrop : IFileDragDrop
{
    private const uint CopyMoveLink = 1 | 2 | 4;
    private const int DragDropSucceeded = 0x00040100;
    private const int LeftMouseButton = 0x01;
    private const int RightMouseButton = 0x02;
    private const int SwappedMouseButtons = 23;
    private static readonly Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid DataObjectId = new("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid DataObjectHandler = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");

    /// <remarks>
    /// Runs on the UI STA after the pointer gesture releases its capture.
    /// Windows owns modifier keys, feedback, cancellation and file operations.
    /// Never delete a source here: optimized moves can report a zero effect.
    /// </remarks>
    public bool Start(nint owner, string path)
    {
        if (!IsPrimaryButtonDown()) return false;

        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        try
        {
            return DragShellItem(owner, path);
        }
        finally
        {
            // S_FALSE from OleInitialize also requires OleUninitialize.
            OleUninitialize();
        }
    }

    private static bool DragShellItem(nint owner, string path)
    {
        var data = CreateDataObject(path);
        try
        {
            // Resolving a network path may take time. A released gesture must
            // not turn into an immediate drop at the pointer's new position.
            if (!IsPrimaryButtonDown()) return false;

            // Null asks Windows for its standard drop source and drag image.
            // Keep every operation available as modifiers change during the drag.
            var result = SHDoDragDrop(owner, data, IntPtr.Zero, CopyMoveLink, out _);
            Marshal.ThrowExceptionForHR(result);
            return result == DragDropSucceeded;
        }
        finally
        {
            Marshal.ReleaseComObject(data);
        }
    }

    private static bool IsPrimaryButtonDown()
    {
        // GetAsyncKeyState reports physical buttons, including left-handed setups.
        var button = GetSystemMetrics(SwappedMouseButtons) == 0
            ? LeftMouseButton
            : RightMouseButton;
        return GetAsyncKeyState(button) < 0;
    }

    // Separate from the modal drag loop so Windows tests can inspect the actual
    // Shell formats without moving files or generating global mouse input.
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
        // Only the first IShellItem vtable slot is used.
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

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetrics(int index);
}
