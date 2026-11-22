using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Xunit;

namespace FolderDock.Shell.Tests;

/// <summary>Consumes the real Shell payloads used by Explorer and file-drop applications.</summary>
internal static class ShellDataObjectAssertions
{
    private const short FileDropFormat = 15;
    private const uint FileSystemPathName = 0x80058000;

    public static void Files(IDataObject data, params string[] expectedPaths)
    {
        Assert.Equal(expectedPaths, ReadFileDrop(data), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedPaths, ReadShellItems(data), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ReadFileDrop(IDataObject data)
    {
        var medium = GetMedium(data, FileDropFormat);
        try
        {
            Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
            var paths = new string[DragQueryFileW(medium.unionmember, uint.MaxValue, null, 0)];
            for (uint index = 0; index < paths.Length; index++)
            {
                var length = DragQueryFileW(medium.unionmember, index, null, 0);
                var path = new StringBuilder(checked((int)length + 1));
                Assert.Equal(length, DragQueryFileW(medium.unionmember, index, path, (uint)path.Capacity));
                paths[index] = path.ToString();
            }
            return paths;
        }
        finally { ReleaseStgMedium(ref medium); }
    }

    private static string[] ReadShellItems(IDataObject data)
    {
        var format = RegisterClipboardFormatW("Shell IDList Array");
        Assert.NotEqual(0u, format);
        var medium = GetMedium(data, unchecked((short)format));
        try
        {
            Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
            var memory = GlobalLock(medium.unionmember);
            Assert.NotEqual(IntPtr.Zero, memory);
            try { return ReadItemPaths(memory, checked((int)GlobalSize(medium.unionmember))); }
            finally { GlobalUnlock(medium.unionmember); }
        }
        finally { ReleaseStgMedium(ref medium); }
    }

    private static string[] ReadItemPaths(IntPtr memory, int size)
    {
        // Resolve each relative child PIDL against the parent in the CIDA header.
        Assert.True(size >= 12, "The Shell IDList Array must contain a parent and at least one child.");
        var count = Marshal.ReadInt32(memory);
        Assert.InRange(count, 1, (size / sizeof(int)) - 2);
        var headerSize = (count + 2) * sizeof(int);
        var parentOffset = Marshal.ReadInt32(memory, 4);
        Assert.InRange(parentOffset, headerSize, size - 2);
        var parent = IntPtr.Add(memory, parentOffset);
        var paths = new string[count];
        for (var index = 0; index < count; index++)
        {
            var childOffset = Marshal.ReadInt32(memory, (index + 2) * sizeof(int));
            Assert.InRange(childOffset, headerSize, size - 2);
            paths[index] = ReadPath(parent, IntPtr.Add(memory, childOffset));
        }
        return paths;
    }

    private static string ReadPath(IntPtr parent, IntPtr child)
    {
        var item = ILCombine(parent, child);
        Assert.NotEqual(IntPtr.Zero, item);
        try
        {
            Marshal.ThrowExceptionForHR(SHGetNameFromIDList(item, FileSystemPathName, out var name));
            try { return Marshal.PtrToStringUni(name) ?? string.Empty; }
            finally { Marshal.FreeCoTaskMem(name); }
        }
        finally { Marshal.FreeCoTaskMem(item); }
    }

    private static STGMEDIUM GetMedium(IDataObject data, short id)
    {
        var format = new FORMATETC
        {
            cfFormat = id,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };
        Assert.Equal(0, data.QueryGetData(ref format));
        data.GetData(ref format, out var medium);
        return medium;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint RegisterClipboardFormatW(string name);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint DragQueryFileW(IntPtr drop, uint index, StringBuilder? path, uint length);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern IntPtr ILCombine(IntPtr parent, IntPtr child);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetNameFromIDList(IntPtr item, uint nameType, out IntPtr name);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nuint GlobalSize(IntPtr memory);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
