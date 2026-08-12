using System;
using System.IO;
using System.Linq;

namespace FolderDock;

public static class FolderReader
{
    private const int MaxEntries = 500;

    public static FolderContents Read(string folder)
    {
        try
        {
            var directories = Directory.EnumerateDirectories(folder)
                .Select(path => FolderEntry.Directory(path));
            var files = Directory.EnumerateFiles(folder)
                .Select(path => FolderEntry.File(path));

            var entries = directories.Concat(files)
                .Where(entry => !IsHidden(entry.FullPath))
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxEntries)
                .ToArray();

            return new FolderContents(entries, Failed: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or IOException
                                       or ArgumentException)
        {
            // Нет доступа / диск отключён / кривой путь: UI должен показать
            // «Нет доступа», а не вводящее в заблуждение «Папка пуста»
            return FolderContents.Error;
        }
    }

    private static bool IsHidden(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) != 0;
        }
        catch
        {
            // Атрибуты не читаются (гонка удаления, права) — считаем видимым,
            // лучше показать лишний элемент, чем молча скрыть существующий
            return false;
        }
    }
}
