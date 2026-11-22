using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderDock;

/// <summary>Obtains the native Shell object for a group of filesystem items.</summary>
internal static class ShellSelection
{
    internal static IDataObject CreateDataObject(IReadOnlyList<string> paths) =>
        GetForPaths(paths, 0, CreateDesktopDataObject);

    internal static T CreateContextMenu<T>(IReadOnlyList<string> paths, nint owner) where T : class =>
        GetForPaths(paths, owner, ids => CreateMixedMenu<T>(ids, owner));

    // The caller owns the returned COM reference. Resolve every path before
    // binding so a missing item cannot silently turn into a partial action.
    private static T GetForPaths<T>(IReadOnlyList<string> paths, nint owner, Func<nint[], T> forMixedParents)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0) throw new ArgumentException("Select at least one item.", nameof(paths));

        var absoluteIds = new nint[paths.Count];
        try
        {
            for (var i = 0; i < paths.Count; i++)
                Marshal.ThrowExceptionForHR(SHParseDisplayName(paths[i], 0, out absoluteIds[i], 0, out _));

            var parentPath = Path.GetDirectoryName(paths[0]);
            var siblings = paths.All(path => Path.GetDirectoryName(path) == parentPath);
            return siblings ? GetSiblingObject<T>(absoluteIds, owner) : forMixedParents(absoluteIds);
        }
        finally
        {
            foreach (var id in absoluteIds) Marshal.FreeCoTaskMem(id);
        }
    }

    private static T GetSiblingObject<T>(nint[] ids, nint owner) where T : class
    {
        var folderId = typeof(IShellFolder).GUID;
        Marshal.ThrowExceptionForHR(SHBindToParent(ids[0], ref folderId, out var folder, out _));
        try
        {
            var children = Array.ConvertAll(ids, ILFindLastID);
            var iid = typeof(T).GUID;
            folder.GetUIObjectOf(owner, (uint)children.Length, children, ref iid, 0, out var value);
            return (T)value;
        }
        finally { Marshal.ReleaseComObject(folder); }
    }

    private static IDataObject CreateDesktopDataObject(nint[] ids)
    {
        // An empty desktop PIDL makes each fully qualified PIDL relative to the namespace root.
        ushort desktop = 0;
        Marshal.ThrowExceptionForHR(CIDLData_CreateFromIDArray(ref desktop, (uint)ids.Length, ids, out var data));
        return data;
    }

    private static T CreateMixedMenu<T>(nint[] ids, nint owner) where T : class
    {
        Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)ids.Length, ids, out var selection));
        try
        {
            // GetUIObjectOf accepts siblings. Initialize the menu with one
            // filesystem item, then give it the entire cross-folder selection.
            var menu = GetSiblingObject<T>([ids[0]], owner);
            try
            {
                ((IObjectWithSelection)menu).SetSelection(selection);
                return menu;
            }
            catch
            {
                Marshal.ReleaseComObject(menu);
                throw;
            }
        }
        finally { Marshal.ReleaseComObject(selection); }
    }

    [ComImport, Guid("1C9CD5BB-98E9-4491-A60F-31AACC72B83C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectWithSelection
    {
        void SetSelection(IShellItemArray selection);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray { }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        // Reserve the preceding vtable slots. Only GetUIObjectOf is invoked.
        void ParseDisplayName();
        void EnumObjects();
        void BindToObject();
        void BindToStorage();
        void CompareIDs();
        void CreateViewObject();
        void GetAttributesOf();
        void GetUIObjectOf(nint owner, uint count,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] children,
            ref Guid iid, nint reserved, [MarshalAs(UnmanagedType.Interface)] out object value);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string path, nint bindContext, out nint id,
        uint attributes, out uint resultAttributes);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHBindToParent(nint id, ref Guid iid, out IShellFolder folder, out nint child);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int CIDLData_CreateFromIDArray(ref ushort desktop, uint count,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] ids, out IDataObject data);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHCreateShellItemArrayFromIDLists(uint count,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] ids, out IShellItemArray selection);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern nint ILFindLastID(nint id);
}
