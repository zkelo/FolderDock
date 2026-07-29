using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FolderDock;

public sealed class ThumbnailLoader
{
    private const uint ThumbnailSize = 64;
    private CancellationTokenSource? _current;

    public async Task LoadAsync(IReadOnlyList<FolderEntry> entries)
    {
        _current?.Cancel();
        var cts = new CancellationTokenSource();
        _current = cts;

        foreach (var entry in entries)
        {
            if (cts.IsCancellationRequested) return;
            entry.Thumbnail = await TryGetThumbnailAsync(entry);
        }
    }

    private static async Task<BitmapImage?> TryGetThumbnailAsync(FolderEntry entry)
    {
        try
        {
            using var thumbnail = await GetStorageThumbnailAsync(entry);
            if (thumbnail is null) return null;

            var image = new BitmapImage();
            await image.SetSourceAsync(thumbnail);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<StorageItemThumbnail?> GetStorageThumbnailAsync(FolderEntry entry)
    {
        if (entry.IsDirectory)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(entry.FullPath);
            return await folder.GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize);
        }
        var file = await StorageFile.GetFileFromPathAsync(entry.FullPath);
        return await file.GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize);
    }
}
