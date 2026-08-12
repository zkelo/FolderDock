using System;
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

    /// Загружает миниатюры последовательно; предыдущая загрузка отменяется.
    /// Ошибки по-элементно проглатываются (элемент остаётся без миниатюры).
    public async Task LoadAsync(IReadOnlyList<FolderEntry> entries)
    {
        var cts = RestartCancellation();

        foreach (var entry in entries)
        {
            if (cts.IsCancellationRequested) return;
            var thumbnail = await TryGetThumbnailAsync(entry);
            // Токен мог быть отменён во время await — не пишем миниатюру
            // чужой (уже переоткрытой) папке
            if (cts.IsCancellationRequested) return;
            entry.Thumbnail = thumbnail;
        }
    }

    /// Остановить фоновую загрузку (например, при скрытии попапа).
    public void CancelPending() => _current?.Cancel();

    private CancellationTokenSource RestartCancellation()
    {
        var previous = _current;
        previous?.Cancel();
        previous?.Dispose();

        var cts = new CancellationTokenSource();
        _current = cts;
        return cts;
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
            // Нет миниатюры (нет прав, файл удалён, формат без превью) —
            // элемент отображается без картинки, это штатный случай
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
