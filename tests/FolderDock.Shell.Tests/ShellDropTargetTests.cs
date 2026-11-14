using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Xunit;

namespace FolderDock.Shell.Tests;

/// <summary>Transfers disposable files through Windows' real filesystem drop target.</summary>
public sealed class ShellDropTargetTests : IDisposable
{
    private const uint Shift = 4;
    private const uint Control = 8;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fd-drop-" + Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly string _destination;

    public ShellDropTargetTests()
    {
        _source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        _destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlDropCopiesToOrdinaryAndNestedUnicodeFolders(bool nestedUnicode)
    {
        var source = CreateFile("payload.txt");
        var destination = nestedUnicode
            ? Directory.CreateDirectory(Path.Combine(_destination, "nested", "Получатель 日本")).FullName
            : _destination;

        Transfer(source, destination, Control, NativeFolderDrop.Copy);

        Assert.Equal("original bytes", File.ReadAllText(Path.Combine(destination, "payload.txt")));
        Assert.Equal("original bytes", File.ReadAllText(source));
    }

    [Fact]
    public void ShiftDropMovesTheFileWithoutSourceSideDeletion()
    {
        var source = CreateFile("move.txt");

        Transfer(source, _destination, Shift, NativeFolderDrop.Move);

        Assert.False(File.Exists(source));
        Assert.Equal("original bytes", File.ReadAllText(Path.Combine(_destination, "move.txt")));
    }

    [Fact]
    public void ControlShiftDropCreatesAShortcutToTheOriginal()
    {
        var source = CreateFile("linked.txt");

        Transfer(source, _destination, Control | Shift, NativeFolderDrop.Link);

        Assert.Equal("original bytes", File.ReadAllText(source));
        var link = Assert.Single(Directory.GetFiles(_destination, "*.lnk"));
        InSta(() => WithShortcut(link, shortcut =>
            Assert.Equal(source, (string)shortcut.TargetPath, ignoreCase: true)));
    }

    [Fact]
    public void CopyingAFolderPreservesItsNestedFiles()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_source, "folder")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(folder, "nested")).FullName;
        File.WriteAllText(Path.Combine(nested, "child.txt"), "nested content");

        Transfer(folder, _destination, Control, NativeFolderDrop.Copy);

        Assert.Equal("nested content", File.ReadAllText(Path.Combine(_destination, "folder", "nested", "child.txt")));
        Assert.Equal("nested content", File.ReadAllText(Path.Combine(nested, "child.txt")));
    }

    [Fact]
    public void CopyingAShortcutTransfersTheLinkInsteadOfItsTarget()
    {
        var target = CreateFile("target.txt");
        var link = Path.Combine(_source, "shortcut.lnk");
        InSta(() => WithShortcut(link, shortcut =>
        {
            shortcut.TargetPath = target;
            shortcut.Save();
        }));

        Transfer(link, _destination, Control, NativeFolderDrop.Copy);

        var copied = Path.Combine(_destination, "shortcut.lnk");
        Assert.True(File.Exists(copied));
        Assert.False(File.Exists(Path.Combine(_destination, "target.txt")));
        Assert.Equal("original bytes", File.ReadAllText(target));
        InSta(() => WithShortcut(copied, shortcut =>
            Assert.Equal(target, (string)shortcut.TargetPath, ignoreCase: true)));
    }

    [Fact]
    public void LeavingAnAcceptedDestinationDoesNotChangeEitherFolder()
    {
        var source = CreateFile("canceled.txt");
        InSta(() =>
        {
            using var drop = new NativeFolderDrop(source, _destination);
            Assert.Equal(NativeFolderDrop.Copy, drop.Enter(Control));
            drop.Leave();
        });

        Assert.Equal("original bytes", File.ReadAllText(source));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_destination));
    }

    [Fact]
    public void DestinationRejectsADragWithNoAllowedOperations()
    {
        var source = CreateFile("rejected.txt");
        InSta(() =>
        {
            using var drop = new NativeFolderDrop(source, _destination);
            Assert.Equal(0u, drop.Enter(Control, allowedEffects: 0));
        });

        Assert.Equal("original bytes", File.ReadAllText(source));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_destination));
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_source, name);
        File.WriteAllText(path, "original bytes");
        return path;
    }

    private static void Transfer(string source, string destination, uint modifiers, uint expectedEffect) =>
        InSta(() =>
        {
            using var drop = new NativeFolderDrop(source, destination);
            Assert.Equal(expectedEffect, drop.Enter(modifiers));
            // Optimized Shell moves can return zero here. Verify the actual
            // filesystem result rather than treating the reported effect as proof.
            drop.Drop(modifiers);
        });

    private static void WithShortcut(string path, Action<dynamic> action)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(path);
            try { action(shortcut); }
            finally { Marshal.ReleaseComObject(shortcut); }
        }
        finally { Marshal.ReleaseComObject(shell); }
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
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Filesystem drop target timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void OleUninitialize();
}
