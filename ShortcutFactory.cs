using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FolderDock;

public static class ShortcutFactory
{
    public static string CreateFolderShortcut(string folderPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var argumentPath = folderPath.TrimEnd(Path.DirectorySeparatorChar);
        if (argumentPath.Length == 0) argumentPath = folderPath;

        var lnkPath = UniqueShortcutPath(folderPath, argumentPath, outputDir);
        var exe = Environment.ProcessPath
                  ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу");

        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exe);
        link.SetArguments($"--folder \"{argumentPath}\"");
        link.SetWorkingDirectory(Path.GetDirectoryName(exe)!);
        link.SetDescription($"FolderDock: {folderPath}");
        link.SetIconLocation(SystemLibrary("imageres.dll"), 3);

        WriteAumid(link, FolderAumid.For(folderPath));
        ((IPersistFile)link).Save(lnkPath, true);
        return lnkPath;
    }

    private static string UniqueShortcutPath(
        string folderPath, string argumentPath, string outputDir)
    {
        var name = Path.GetFileName(argumentPath);
        if (string.IsNullOrEmpty(name))
            name = "Диск " + argumentPath.Replace(":", "");

        var path = Path.Combine(outputDir, name + ".lnk");
        if (!File.Exists(path)) return path;

        return Path.Combine(outputDir,
            $"{name} ({FolderAumid.ShortSuffix(folderPath)}).lnk");
    }

    private static string SystemLibrary(string fileName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName);

    private static void WriteAumid(IShellLinkW link, string aumid)
    {
        var store = (IPropertyStore)link;
        var key = PropertyKey.AppUserModelId;
        using var value = PropVariant.FromString(aumid);
        store.SetValue(ref key, value);
        store.Commit();
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
            int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
            int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
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
    private sealed class PropVariant : IDisposable
    {
        [FieldOffset(0)] private ushort vt;
        [FieldOffset(8)] private IntPtr ptr;

        public static PropVariant FromString(string value) => new()
        {
            vt = 31,
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
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, [Out] PropVariant pv);
        void SetValue(ref PropertyKey key, [In] PropVariant pv);
        void Commit();
    }
}
