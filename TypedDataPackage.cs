using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FolderDock;

public static class TypedDataPackage
{
    private const long MaxInlineTextBytes = 256 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".webp",
        ".tif", ".tiff", ".ico", ".heic", ".heif"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".json", ".xml", ".yaml", ".yml", ".csv",
        ".ini", ".cfg", ".conf", ".cs", ".js", ".ts", ".py", ".php",
        ".html", ".htm", ".css", ".sql", ".sh", ".ps1", ".bat", ".cmd"
    };

    public static async Task<List<IStorageItem>> ResolveStorageItemsAsync(
        IReadOnlyList<FolderEntry> entries)
    {
        var items = new List<IStorageItem>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.IsDirectory)
                items.Add(await StorageFolder.GetFolderFromPathAsync(entry.FullPath));
            else
                items.Add(await StorageFile.GetFileFromPathAsync(entry.FullPath));
        }
        return items;
    }

    /// Заполнение с уже разрешёнными IStorageItem — для буфера обмена и Share,
    /// где резолв можно сделать заранее (до SetContent / внутри deferral).
    public static void Fill(DataPackage package,
        IReadOnlyList<FolderEntry> entries, IReadOnlyList<IStorageItem> items)
    {
        FillCommonProperties(package, entries);
        if (items.Count > 0)
            package.SetStorageItems(items, readOnly: false);

        if (!IsSingleFile(entries)) return;

        var entry = entries[0];
        if (IsImage(entry) && items.Count == 1 && items[0] is StorageFile imageFile)
        {
            var stream = RandomAccessStreamReference.CreateFromFile(imageFile);
            package.SetBitmap(stream);
            package.Properties.Thumbnail = stream;
        }
        else if (IsText(entry))
        {
            TryAddTextContent(package, entry.FullPath);
        }
    }

    private static void FillCommonProperties(
        DataPackage package, IReadOnlyList<FolderEntry> entries)
    {
        package.RequestedOperation = DataPackageOperation.Copy;
        package.Properties.Title = entries.Count == 1
            ? entries[0].Name
            : Loc.Format("Items_Count", entries.Count);
        package.Properties.Description = "FolderDock";
    }

    private static bool IsSingleFile(IReadOnlyList<FolderEntry> entries) =>
        entries.Count == 1 && !entries[0].IsDirectory;

    private static bool IsImage(FolderEntry entry) =>
        ImageExtensions.Contains(Path.GetExtension(entry.FullPath));

    private static bool IsText(FolderEntry entry) =>
        TextExtensions.Contains(Path.GetExtension(entry.FullPath));

    private static void TryAddTextContent(DataPackage package, string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length <= MaxInlineTextBytes)
                package.SetText(File.ReadAllText(path));
        }
        catch
        {
            // Файл исчез/недоступен: пакет останется без текстового
            // представления, StorageItems всё равно доедут
        }
    }
}
