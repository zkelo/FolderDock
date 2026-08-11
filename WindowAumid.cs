using System;
using System.Runtime.InteropServices;

namespace FolderDock;

// AppUserModelID уровня окна: панель задач сопоставляет окно с ярлыком
// по AUMID, записанному в property store конкретного HWND.
internal static class WindowAumid
{
    // Идентификатор самого приложения (менеджер, ярлык в меню Пуск).
    public const string AppId = "FolderDock.App";

    public static void Apply(IntPtr hwnd, string aumid)
    {
        try
        {
            var iid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"); // IPropertyStore
            SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
            var key = PropertyKey.AppUserModelId;
            using var value = PropVariant.FromString(aumid);
            store.SetValue(ref key, value);
            store.Commit();
        }
        catch
        {
            // не критично: окно останется с process-wide AUMID
        }
    }

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHGetPropertyStoreForWindow(
        IntPtr hwnd, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
}
