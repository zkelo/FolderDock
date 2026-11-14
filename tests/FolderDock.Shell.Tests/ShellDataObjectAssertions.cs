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

    public static void SingleFile(IDataObject data, string expectedPath)
    {
        Assert.Equal(expectedPath, ReadFileDrop(data), ignoreCase: true);
        Assert.Equal(expectedPath, ReadShellItem(data), ignoreCase: true);
    }

    private static string ReadFileDrop(IDataObject data)
    {
        var medium = GetMedium(data, FileDropFormat);
        try
        {
            Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
            Assert.Equal(1u, DragQueryFileW(medium.unionmember, uint.MaxValue, null, 0));
            var length = DragQueryFileW(medium.unionmember, 0, null, 0);
            var path = new StringBuilder(checked((int)length + 1));
            Assert.Equal(length, DragQueryFileW(medium.unionmember, 0, path, (uint)path.Capacity));
            return path.ToString();
        }
        finally { ReleaseStgMedium(ref medium); }
    }

    private static string ReadShellItem(IDataObject data)
    {
        var format = RegisterClipboardFormatW("Shell IDList Array");
        Assert.NotEqual(0u, format);
        var medium = GetMedium(data, unchecked((short)format));
        try
        {
            Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
            var memory = GlobalLock(medium.unionmember);
            Assert.NotEqual(IntPtr.Zero, memory);
            try { return ReadSingleItemPath(memory, checked((int)GlobalSize(medium.unionmember))); }
            finally { GlobalUnlock(medium.unionmember); }
        }
        finally { ReleaseStgMedium(ref medium); }
    }

    private static string ReadSingleItemPath(IntPtr memory, int size)
    {
        // CIDA contains the item count, an absolute parent PIDL offset and one
        // relative child PIDL offset. Resolve their combination, not the parent.
        Assert.True(size >= 12, "The Shell IDList Array must contain its header and two offsets.");
        Assert.Equal(1, Marshal.ReadInt32(memory));
        var parentOffset = Marshal.ReadInt32(memory, 4);
        var childOffset = Marshal.ReadInt32(memory, 8);
        Assert.InRange(parentOffset, 12, size - 2);
        Assert.InRange(childOffset, 12, size - 2);
        var item = ILCombine(IntPtr.Add(memory, parentOffset), IntPtr.Add(memory, childOffset));
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
