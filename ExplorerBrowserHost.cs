using System;
using System.Runtime.InteropServices;

namespace FolderDock;

/// Хост встроенного вида Проводника (IExplorerBrowser) внутри окна менеджера.
/// Шелл создаёт дочернее окно с настоящим представлением файлов: переименование,
/// перетаскивание, выделение и полное контекстное меню — как в Проводнике.
public sealed class ExplorerBrowserHost : IDisposable
{
    // EXPLORER_BROWSER_OPTIONS
    private const uint EBO_NAVIGATEONCE = 0x1;  // запрет навигации из вида (двойной клик по ярлыку запускает его, а не уводит вглубь)
    private const uint EBO_NOTRAVELLOG = 0x8;   // без журнала переходов
    private const uint EBO_NOBORDER = 0x40;     // рамку рисует XAML-Border, не шелл

    // FOLDERVIEWMODE: плитки — крупный значок + имя, удобно для ярлыков
    private const int FVM_TILE = 6;

    private static readonly Guid IID_IShellItem =
        new(0x43826d1e, 0xe718, 0x42ee, 0xbc, 0x55, 0xa1, 0xe2, 0x61, 0xc3, 0x7b, 0xfe);

    private IExplorerBrowser? _browser;

    /// Создаёт вид Проводника в прямоугольнике rect (физические пиксели,
    /// координаты клиентской области hwndParent) и открывает в нём folderPath.
    public void Initialize(IntPtr hwndParent, RECT rect, string folderPath, string emptyText)
    {
        var browser = (IExplorerBrowser)new ExplorerBrowserClass();
        try
        {
            var settings = new FOLDERSETTINGS { ViewMode = FVM_TILE, fFlags = 0 };
            browser.Initialize(hwndParent, ref rect, ref settings);
            browser.SetOptions(EBO_NAVIGATEONCE | EBO_NOTRAVELLOG | EBO_NOBORDER);
            if (emptyText.Length > 0) browser.SetEmptyText(emptyText);

            var iid = IID_IShellItem;
            SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref iid, out var item);
            try
            {
                browser.BrowseToObject(item, 0 /* SBSP_ABSOLUTE */);
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        catch
        {
            // Недосозданный браузер не оставляем: Destroy освобождает то,
            // что успел создать Initialize
            try { browser.Destroy(); } catch { }
            Marshal.ReleaseComObject(browser);
            throw;
        }

        _browser = browser;
    }

    /// Подгоняет вид под новый прямоугольник (ресайз окна, смена DPI).
    public void SetBounds(RECT rect)
    {
        try
        {
            _browser?.SetRect(IntPtr.Zero, rect);
        }
        catch
        {
            // Вид мог быть уже разрушен при закрытии окна — ресайз не критичен
        }
    }

    public void Dispose()
    {
        if (_browser is null) return;
        try { _browser.Destroy(); } catch { }
        Marshal.ReleaseComObject(_browser);
        _browser = null;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    [ComImport, Guid("71f96385-ddd6-48d3-a0c1-ae06e8b055fb")]
    private class ExplorerBrowserClass { }

    [StructLayout(LayoutKind.Sequential)]
    private struct FOLDERSETTINGS
    {
        public int ViewMode;
        public uint fFlags;
    }

    // Порядок методов = vtable IExplorerBrowser (ShObjIdl_core.h) — не менять
    [ComImport, Guid("dfd3b6b5-c10c-4be9-85f6-a66969f402f6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IExplorerBrowser
    {
        void Initialize(IntPtr hwndParent, ref RECT prc, ref FOLDERSETTINGS pfs);
        void Destroy();
        void SetRect(IntPtr phdwp, RECT rcBrowser);
        void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string pszPropertyBag);
        void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string pszEmptyText);
        void SetFolderSettings(ref FOLDERSETTINGS pfs);
        void Advise(IntPtr psbe, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint dwFlag);
        void GetOptions(out uint pdwFlag);
        void BrowseToIDList(IntPtr pidl, uint uFlags);
        void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
        void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int dwFlags);
        void RemoveAll();
        void GetCurrentView(ref Guid riid, out IntPtr ppv);
    }
}

/// Прямоугольник Win32 (физические пиксели).
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
