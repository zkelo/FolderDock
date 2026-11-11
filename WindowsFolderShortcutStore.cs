using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using FolderDock.Core;

namespace FolderDock;

/// Adapts the application's shortcut directory and Windows Shell links.
internal sealed class WindowsFolderShortcutStore : IFolderShortcutStore
{
    private readonly string _directory;

    public WindowsFolderShortcutStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public IReadOnlyList<FolderShortcut> ReadAll()
    {
        try
        {
            if (!Directory.Exists(_directory)) return Array.Empty<FolderShortcut>();

            var shortcuts = new List<FolderShortcut>();
            foreach (var path in Directory.EnumerateFiles(_directory, "*.lnk"))
            {
                var folder = ShortcutFactory.ReadFolderFromShortcut(path);
                if (folder is not null)
                    shortcuts.Add(ToShortcut(path, folder));
            }
            return shortcuts;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new FolderShortcutException(exception.Message, exception);
        }
    }

    public bool FolderExists(string folderPath) => Directory.Exists(folderPath);

    public FolderShortcut Create(string folderPath)
    {
        try
        {
            return ToShortcut(ShortcutFactory.CreateFolderShortcut(folderPath, _directory), folderPath);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new FolderShortcutException(exception.Message, exception);
        }
    }

    private static FolderShortcut ToShortcut(string path, string folder) =>
        new(Path.GetFileNameWithoutExtension(path), folder, path);

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or COMException;
}
