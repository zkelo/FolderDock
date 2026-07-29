using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderDock;

public static class FolderReader
{
    private const int MaxEntries = 500;

    public static IReadOnlyList<FolderEntry> Read(string folder)
    {
        try
        {
            var directories = Directory.EnumerateDirectories(folder)
                .Select(path => FolderEntry.Directory(path));
            var files = Directory.EnumerateFiles(folder)
                .Select(path => FolderEntry.File(path));

            return directories.Concat(files)
                .Where(entry => !IsHidden(entry.FullPath))
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxEntries)
                .ToArray();
        }
        catch
        {
            return Array.Empty<FolderEntry>();
        }
    }

    private static bool IsHidden(string path)
    {
        try
        {
            return (File.GetAttributes(path) & System.IO.FileAttributes.Hidden) != 0;
        }
        catch
        {
            return false;
        }
    }
}
