using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace FolderDock;

/// Поток «Поделиться»: владеет DataTransferManager, текущим элементом
/// и флагом, блокирующим light-dismiss попапа, пока открыта панель Share.
public sealed class ShareCoordinator
{
    private readonly PopupChrome _chrome;
    private readonly Action _shareFinished;
    private DataTransferManager? _manager;
    private FolderEntry? _entry;

    public bool IsShareInProgress { get; private set; }

    public ShareCoordinator(PopupChrome chrome, Action shareFinished)
    {
        _chrome = chrome;
        _shareFinished = shareFinished;
    }

    /// true — панель Share показана; false — интероп недоступен.
    public bool Share(FolderEntry entry)
    {
        _entry = entry;
        IsShareInProgress = true;
        try
        {
            if (_manager is null)
            {
                _manager = _chrome.GetShareManager();
                _manager.DataRequested += OnDataRequested;
            }
            _chrome.ShowShareUI();
            return true;
        }
        catch
        {
            // Share UI недоступен (политики/старая сборка Windows) —
            // не оставляем попап заблокированным от light-dismiss
            IsShareInProgress = false;
            _entry = null;
            return false;
        }
    }

    /// Снять блокировку light-dismiss (панель закрыта или попап реактивирован).
    public void Reset() => IsShareInProgress = false;

    private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_entry is not { } entry)
        {
            args.Request.FailWithDisplayText(Loc.Get("Share_NoItem"));
            return;
        }
        var deferral = args.Request.GetDeferral();
        _ = FillAsync(args.Request, entry, deferral);
    }

    private async Task FillAsync(
        DataRequest request, FolderEntry entry, DataRequestDeferral deferral)
    {
        try
        {
            var entries = new List<FolderEntry> { entry };
            var items = await TypedDataPackage.ResolveStorageItemsAsync(entries);
            TypedDataPackage.Fill(request.Data, entries, items);
        }
        catch (Exception ex)
        {
            request.FailWithDisplayText(ex.Message);
        }
        finally
        {
            deferral.Complete();
            _entry = null; // не удерживаем FolderEntry (и его BitmapImage) до следующего шаринга
            IsShareInProgress = false;
            _shareFinished();
        }
    }
}
