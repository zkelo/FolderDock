using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FolderDock;

public static class ShortcutFactory
{
    /// imageres.dll, индекс 3 — стандартная жёлтая папка Windows.
    private const int FolderIconIndex = 3;

    /// Максимальная читаемая длина строки аргументов ярлыка.
    private const int MaxArgumentsLength = 1024;

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
        link.SetIconLocation(SystemLibrary("imageres.dll"), FolderIconIndex);

        WriteAumid(link, FolderAumid.For(folderPath));
        ((IPersistFile)link).Save(lnkPath, true);
        return lnkPath;
    }

    /// Читает путь папки из аргументов существующего ярлыка (--folder "...").
    /// null — ярлык не читается или не содержит --folder.
    public static string? ReadFolderFromShortcut(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new CShellLink();
            ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);
            var args = new StringBuilder(MaxArgumentsLength);
            link.GetArguments(args, args.Capacity);
            return CommandLine.FolderFrom(args.ToString());
        }
        catch
        {
            return null;
        }
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
}
