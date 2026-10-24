using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Xunit;

namespace FolderDock.Shell.Tests;

public sealed class ShellDragDropTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "FolderDock-drag-tests-" + Guid.NewGuid().ToString("N"));

    public ShellDragDropTests() => Directory.CreateDirectory(_folder);

    [Theory]
    [InlineData("Обычный файл с пробелами.txt")]
    [InlineData("фото 🐈.png")]
    [InlineData("archive.zip")]
    [InlineData("application.exe")]
    [InlineData("unknown.custom-extension")]
    [InlineData("read-only.txt")]
    [InlineData("empty")]
    public void FilesOfferTheirActualPathInBothExplorerAndApplicationFormats(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "test content");
        if (name == "read-only.txt") File.SetAttributes(path, FileAttributes.ReadOnly);

        AssertShellFile(path);

        Assert.True(File.Exists(path));
        Assert.Equal("test content", File.ReadAllText(path));
    }

    [Fact]
    public void FolderIsOfferedAsAFolderWithoutEnumeratingOrTransferringItsChildren()
    {
        var path = Path.Combine(_folder, "Подпапка");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "child.txt"), "child");

        AssertShellFile(path);

        Assert.True(File.Exists(Path.Combine(path, "child.txt")));
    }

    [Fact]
    public void SearchResultUsesItsActualNestedPath()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_folder, "nested", "results"));
        var path = Path.Combine(nested.FullName, "result.txt");
        File.WriteAllText(path, "result");

        AssertShellFile(path);
    }

    [Fact]
    public void InternetShortcutIsTransferredAsTheShortcutFile()
    {
        var path = Path.Combine(_folder, "website.url");
        File.WriteAllText(path, "[InternetShortcut]\r\nURL=https://example.invalid/\r\n");

        AssertShellFile(path);
    }

    [Fact]
    public void WindowsShortcutIsTransferredWithoutReplacingItWithItsTarget()
    {
        InSta(() =>
        {
            var target = Path.Combine(_folder, "target.txt");
            File.WriteAllText(target, "target");
            var linkPath = Path.Combine(_folder, "shortcut.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(linkPath);
                try
                {
                    shortcut.TargetPath = target;
                    shortcut.Save();
                }
                finally { Marshal.ReleaseComObject(shortcut); }
            }
            finally { Marshal.ReleaseComObject(shell); }

            AssertShellFileOnCurrentThread(linkPath);
            Assert.Equal("target", File.ReadAllText(target));
        });
    }

    [Fact]
    public void MissingSourceFailsWithoutCreatingAReplacementFile()
    {
        var missing = Path.Combine(_folder, "deleted.txt");

        InSta(() => Assert.ThrowsAny<Exception>(() => ShellDragDrop.CreateDataObject(missing)));

        Assert.False(File.Exists(missing));
    }

    private static void AssertShellFile(string path) =>
        InSta(() => AssertShellFileOnCurrentThread(path));

    private static void AssertShellFileOnCurrentThread(string path)
    {
        var data = ShellDragDrop.CreateDataObject(path);
        try
        {
            var shellItems = Format(unchecked((short)RegisterClipboardFormatW("Shell IDList Array")));
            Assert.Equal(0, data.QueryGetData(ref shellItems));

            var fileDrop = Format(15); // CF_HDROP, used by Explorer and desktop applications.
            Assert.Equal(0, data.QueryGetData(ref fileDrop));
            data.GetData(ref fileDrop, out var medium);
            try
            {
                Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
                Assert.Equal(1u, DragQueryFileW(medium.unionmember, uint.MaxValue, null, 0));
                var length = DragQueryFileW(medium.unionmember, 0, null, 0);
                var text = new StringBuilder(checked((int)length + 1));
                Assert.Equal(length, DragQueryFileW(medium.unionmember, 0, text, (uint)text.Capacity));
                Assert.Equal(path, text.ToString(), ignoreCase: true);
            }
            finally { ReleaseStgMedium(ref medium); }
        }
        finally { Marshal.ReleaseComObject(data); }
    }

    private static FORMATETC Format(short id) => new()
    {
        cfFormat = id,
        dwAspect = DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        tymed = TYMED.TYMED_HGLOBAL
    };

    private static void InSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Shell format query timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(_folder, recursive: true);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint RegisterClipboardFormatW(string name);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint DragQueryFileW(IntPtr drop, uint index, StringBuilder? path, uint length);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
