using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Xunit;

namespace FolderDock.Shell.Tests;

// Shell automation can share COM wrappers across apartments in one process.
[Collection("Windows Shell")]
public sealed class ShellDragDropTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "FolderDock-drag-tests-" + Guid.NewGuid().ToString("N"));

    public ShellDragDropTests() => Directory.CreateDirectory(_folder);

    [Theory]
    [InlineData("Обычный файл с пробелами.txt")]
    [InlineData("фото 🐈.png")]
    [InlineData("archive.zip")]
    [InlineData("archive.7z")]
    [InlineData("document.docx")]
    [InlineData("spreadsheet.xlsx")]
    [InlineData("presentation.pptx")]
    [InlineData("photo.jpg")]
    [InlineData("drawing.svg")]
    [InlineData("audio.mp3")]
    [InlineData("video.mp4")]
    [InlineData("script.ps1")]
    [InlineData("page.html")]
    [InlineData("application.exe")]
    [InlineData("unknown.custom-extension")]
    [InlineData("résumé 你好 𝄞.pdf")]
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
    public void ZeroByteFileIsTransferredByPath()
    {
        var path = Path.Combine(_folder, "zero-byte.bin");
        File.WriteAllBytes(path, Array.Empty<byte>());

        AssertShellFile(path);

        Assert.Equal(0, new FileInfo(path).Length);
    }

    [Theory]
    [InlineData(FileAttributes.Hidden)]
    [InlineData(FileAttributes.ReadOnly)]
    [InlineData(FileAttributes.Hidden | FileAttributes.ReadOnly)]
    public void FileAttributesArePreservedWhenOfferingTheSource(FileAttributes attributes)
    {
        var path = Path.Combine(_folder, "attributes.dat");
        File.WriteAllText(path, "unchanged");
        File.SetAttributes(path, attributes);

        AssertShellFile(path);

        Assert.Equal(attributes, File.GetAttributes(path) & attributes);
        Assert.Equal("unchanged", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(240)]
    [InlineData(259)]
    public void PathsWithinTheShellLengthLimitAreNotTruncatedInEitherFormat(int pathLength)
    {
        var nameLength = pathLength - _folder.Length - 5;
        Assert.InRange(nameLength, 1, 251);
        var path = Path.Combine(_folder, new string('x', nameLength) + ".txt");
        File.WriteAllText(path, "long name");

        AssertShellFile(path);
    }

    [Fact]
    public void EmptyFolderIsTransferredAsTheFolderItself()
    {
        var path = Directory.CreateDirectory(Path.Combine(_folder, "empty folder")).FullName;

        AssertShellFile(path);

        Assert.Empty(Directory.EnumerateFileSystemEntries(path));
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
    public void NestedFolderSearchResultIsNotReplacedWithItsParent()
    {
        var path = Directory.CreateDirectory(Path.Combine(_folder, "nested", "folder result")).FullName;

        AssertShellFile(path);
    }

    [Fact]
    public void InternetShortcutIsTransferredAsTheShortcutFile()
    {
        var path = Path.Combine(_folder, "website.url");
        File.WriteAllText(path, "[InternetShortcut]\r\nURL=https://example.invalid/\r\n");

        AssertShellFile(path);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void WindowsShortcutIsTransferredWithoutReplacingItWithItsTarget(
        bool targetsFolder, bool targetIsMissing)
    {
        InSta(() =>
        {
            var target = Path.Combine(_folder, targetsFolder ? "target folder" : "target.txt");
            if (!targetIsMissing)
            {
                if (targetsFolder) Directory.CreateDirectory(target);
                else File.WriteAllText(target, "target");
            }
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
            Assert.True(File.Exists(linkPath));
            Assert.Equal(!targetIsMissing, targetsFolder ? Directory.Exists(target) : File.Exists(target));
            if (!targetsFolder && !targetIsMissing) Assert.Equal("target", File.ReadAllText(target));
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
            ShellDataObjectAssertions.SingleFile(data, path);
        }
        finally { Marshal.ReleaseComObject(data); }
    }

    private static void InSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
                try { action(); }
                finally { OleUninitialize(); }
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
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

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void OleUninitialize();
}
