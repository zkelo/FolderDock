using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        ArgumentNullException.ThrowIfNull(entries);
        CancelPending();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _current = cancellation;
        try
        {
            foreach (var entry in entries)
            {
                if (token.IsCancellationRequested) return;
                var thumbnail = await TryGetThumbnailAsync(entry);
                // Токен мог быть отменён во время await — не пишем миниатюру
                // чужой (уже переоткрытой) папке
                if (token.IsCancellationRequested) return;
                entry.Thumbnail = thumbnail;
            }
        }
        finally
        {
            if (ReferenceEquals(_current, cancellation)) _current = null;
        }
    }

    /// Остановить фоновую загрузку (например, при скрытии попапа).
    public void CancelPending()
    {
        // Called on the UI thread. Each load owns disposal after its final await;
        // cancellation must not dispose a source still used by that operation.
        var pending = _current;
        _current = null;
        pending?.Cancel();
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
        catch (Exception exception)
        {
            // Нет миниатюры (нет прав, файл удалён, формат без превью) —
            // элемент отображается без картинки, это штатный случай
            Debug.WriteLine($"FolderDock: thumbnail unavailable: {exception}");
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
