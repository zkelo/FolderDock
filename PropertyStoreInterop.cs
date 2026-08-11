using System;
using System.Runtime.InteropServices;

namespace FolderDock;

// Общие COM-типы для записи свойств shell property store
// (используются ShortcutFactory для .lnk и WindowAumid для HWND).

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid fmtid;
    public uint pid;

    public static PropertyKey AppUserModelId => new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };
}

[StructLayout(LayoutKind.Explicit)]
internal sealed class PropVariant : IDisposable
{
    [FieldOffset(0)] private ushort vt;
    [FieldOffset(8)] private IntPtr ptr;

    public static PropVariant FromString(string value) => new()
    {
        vt = 31, // VT_LPWSTR
        ptr = Marshal.StringToCoTaskMemUni(value)
    };

    public void Dispose()
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(ptr);
            ptr = IntPtr.Zero;
        }
        vt = 0;
    }
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
internal interface IPropertyStore
{
    void GetCount(out uint cProps);
    void GetAt(uint iProp, out PropertyKey pkey);
    void GetValue(ref PropertyKey key, [Out] PropVariant pv);
    void SetValue(ref PropertyKey key, [In] PropVariant pv);
    void Commit();
}
