using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderDock.Shell.Tests;

/// <summary>Exercises the filesystem drop target used by Explorer, without injecting mouse input.</summary>
internal sealed class NativeFolderDrop : IDisposable
{
    public const uint Copy = 1;
    public const uint Move = 2;
    public const uint Link = 4;
    private const uint AllowedEffects = Copy | Move | Link;
    private const uint LeftButton = 1;
    private readonly IDataObject _data;
    private readonly IDropTarget _target;
    private bool _entered;

    public NativeFolderDrop(string source, string destination)
    {
        _target = BindTarget(destination);
        try { _data = ShellDragDrop.CreateDataObject(source); }
        catch
        {
            Marshal.ReleaseComObject(_target);
            throw;
        }
    }

    public uint Enter(uint modifiers, uint allowedEffects = AllowedEffects)
    {
        var effect = allowedEffects;
        Marshal.ThrowExceptionForHR(_target.DragEnter(_data, modifiers | LeftButton, default, ref effect));
        _entered = true;
        return effect;
    }

    public uint Drop(uint modifiers)
    {
        var effect = AllowedEffects;
        Marshal.ThrowExceptionForHR(_target.Drop(_data, modifiers, default, ref effect));
        _entered = false;
        return effect;
    }

    public void Leave()
    {
        if (!_entered) return;
        _entered = false;
        Marshal.ThrowExceptionForHR(_target.DragLeave());
    }

    public void Dispose()
    {
        try { Leave(); }
        finally
        {
            Marshal.ReleaseComObject(_data);
            Marshal.ReleaseComObject(_target);
        }
    }

    private static IDropTarget BindTarget(string path)
    {
        var itemId = typeof(IShellItem).GUID;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out var item));
        try
        {
            // BHID_SFUIObject delegates to the folder's GetUIObjectOf implementation.
            var handler = new Guid("3981E225-F559-11D3-8E3A-00C04F6837D5");
            var targetId = typeof(IDropTarget).GUID;
            item.BindToHandler(IntPtr.Zero, ref handler, ref targetId, out var target);
            return target;
        }
        finally { Marshal.ReleaseComObject(item); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IDropTarget target);
    }

    [ComImport, Guid("00000122-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDropTarget
    {
        [PreserveSig] int DragEnter(IDataObject data, uint keys, Point point, ref uint effect);
        [PreserveSig] int DragOver(uint keys, Point point, ref uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop(IDataObject data, uint keys, Point point, ref uint effect);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context,
        ref Guid iid, out IShellItem item);
}
