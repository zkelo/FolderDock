using System;
using System.Runtime.InteropServices;

namespace FolderDock;

/// Настоящее контекстное меню Проводника для файла/папки (IContextMenu).
/// Показ синхронный: TrackPopupMenuEx с TPM_RETURNCMD блокирует до выбора,
/// затем команда передаётся шеллу через InvokeCommand. Подменю
/// «Открыть с помощью»/«Отправить» наполняются лениво — для этого на время
/// меню окно сабклассируется и WM_INITMENUPOPUP/WM_MEASUREITEM/WM_DRAWITEM
/// проксируются в IContextMenu2/IContextMenu3.
/// Вызывать строго из UI-потока (STA) — требование shell-расширений.
internal static class ShellContextMenu
{
    private const uint CmdFirst = 1;
    private const uint CmdLast = 0x7FFF;

    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXTENDEDVERBS = 0x00000100; // Shift: расширенные команды

    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint CMIC_MASK_UNICODE = 0x00004000;
    private const int SW_SHOWNORMAL = 1;
    private const int VK_SHIFT = 0x10;

    private const uint WM_INITMENUPOPUP = 0x0117;
    private const uint WM_DRAWITEM = 0x002B;
    private const uint WM_MEASUREITEM = 0x002C;
    private const uint WM_MENUCHAR = 0x0120;
    private const int GWLP_WNDPROC = -4;

    private static readonly Guid IID_IShellFolder =
        new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IID_IContextMenu =
        new("000214E4-0000-0000-C000-000000000046");

    /// true — пользователь выбрал команду и она передана шеллу.
    /// false — меню закрыто без выбора. Исключения глотать вызывающему:
    /// сломанное shell-расширение не должно ронять попап.
    public static bool Show(IntPtr ownerHwnd, string path)
    {
        var pidl = IntPtr.Zero;
        var contextMenuPtr = IntPtr.Zero;
        var menu = IntPtr.Zero;
        object? folderObj = null;
        object? cmObj = null;

        try
        {
            Marshal.ThrowExceptionForHR(
                SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _));

            var folderIid = IID_IShellFolder;
            Marshal.ThrowExceptionForHR(
                SHBindToParent(pidl, ref folderIid, out var folderPtr, out var childPidl));
            folderObj = Marshal.GetObjectForIUnknown(folderPtr);
            Marshal.Release(folderPtr); // GetObjectForIUnknown сделал свой AddRef
            var folder = (IShellFolder)folderObj;

            // childPidl принадлежит pidl — освобождать отдельно не нужно
            var cmIid = IID_IContextMenu;
            folder.GetUIObjectOf(ownerHwnd, 1, new[] { childPidl },
                ref cmIid, IntPtr.Zero, out contextMenuPtr);
            cmObj = Marshal.GetObjectForIUnknown(contextMenuPtr);
            var contextMenu = (IContextMenu)cmObj;

            menu = CreatePopupMenu();
            var shiftHeld = (Native.GetKeyState(VK_SHIFT) & 0x8000) != 0;
            var queryHr = contextMenu.QueryContextMenu(menu, 0, CmdFirst, CmdLast,
                shiftHeld ? CMF_NORMAL | CMF_EXTENDEDVERBS : CMF_NORMAL);
            if (queryHr < 0) return false;

            Native.GetCursorPos(out var pt);
            // Без форграунда меню не закроется по клику мимо (классика tray-меню)
            Native.SetForegroundWindow(ownerHwnd);

            int selected;
            using (new MenuMessageForwarder(ownerHwnd, cmObj))
            {
                selected = TrackPopupMenuEx(menu,
                    TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_LEFTALIGN,
                    pt.X, pt.Y, ownerHwnd, IntPtr.Zero);
            }
            if (selected < CmdFirst) return false;

            var verb = (IntPtr)(selected - CmdFirst); // MAKEINTRESOURCE
            var info = new CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CMIC_MASK_UNICODE,
                hwnd = ownerHwnd,
                lpVerb = verb,
                lpVerbW = verb,
                nShow = SW_SHOWNORMAL
            };
            Marshal.ThrowExceptionForHR(contextMenu.InvokeCommand(ref info));
            return true;
        }
        finally
        {
            if (menu != IntPtr.Zero) DestroyMenu(menu);
            if (cmObj is not null) Marshal.ReleaseComObject(cmObj);
            if (contextMenuPtr != IntPtr.Zero) Marshal.Release(contextMenuPtr);
            if (folderObj is not null) Marshal.ReleaseComObject(folderObj);
            if (pidl != IntPtr.Zero) ILFree(pidl);
        }
    }

    /// Сабкласс окна на время TrackPopupMenuEx: shell-расширения наполняют
    /// подменю в WM_INITMENUPOPUP и рисуют owner-draw пункты (иконки в
    /// «Отправить») в WM_MEASUREITEM/WM_DRAWITEM — без проксирования этих
    /// сообщений в IContextMenu2/3 подменю остаются пустыми.
    private sealed class MenuMessageForwarder : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly IntPtr _previousProc;
        private readonly WndProcDelegate _hook; // ссылка держит делегат от GC
        private readonly IContextMenu2? _cm2;
        private readonly IContextMenu3? _cm3;

        public MenuMessageForwarder(IntPtr hwnd, object contextMenu)
        {
            _hwnd = hwnd;
            _cm2 = contextMenu as IContextMenu2;
            _cm3 = contextMenu as IContextMenu3;
            _hook = Hook;
            _previousProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_hook));
        }

        private IntPtr Hook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg is WM_INITMENUPOPUP or WM_MEASUREITEM or WM_DRAWITEM or WM_MENUCHAR)
            {
                if (_cm3 is not null &&
                    _cm3.HandleMenuMsg2(msg, wParam, lParam, out var result) == 0)
                    return msg == WM_MENUCHAR ? result : IntPtr.Zero;
                if (_cm2 is not null && _cm2.HandleMenuMsg(msg, wParam, lParam) == 0)
                    return IntPtr.Zero;
            }
            return CallWindowProc(_previousProc, hwnd, msg, wParam, lParam);
        }

        public void Dispose() => SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _previousProc);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    // --- COM-интерфейсы (ComImport требует переобъявления методов базы) ---

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags,
            out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpTitle;
        public IntPtr lpVerbW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTitleW;
        public Native.POINT ptInvoke;
    }

    // --- P/Invoke ---

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc,
        out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid,
        out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags,
        int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hwnd,
        uint msg, IntPtr wParam, IntPtr lParam);
}
