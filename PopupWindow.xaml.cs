using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FolderDock;

public sealed partial class PopupWindow : Window
{
    private static readonly TimeSpan DismissGracePeriod = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ToggleWindow = TimeSpan.FromMilliseconds(600);

    private readonly PopupChrome _chrome;
    private readonly ThumbnailLoader _thumbnails = new();
    private readonly ObservableCollection<FolderEntry> _entries = new();
    private readonly Stack<string> _navigationStack = new();

    private string? _currentFolder;
    private bool _gridMode = true;
    private string? _lastDismissedFolder;
    private DateTime _lastDismissedAt;
    private DateTime _shownAt;

    private DataTransferManager? _shareManager;
    private FolderEntry? _shareEntry;
    private bool _shareInProgress;

    public PopupWindow()
    {
        InitializeComponent();
        _chrome = new PopupChrome(this);
        ItemsGrid.ItemsSource = _entries;
        ItemsList.ItemsSource = _entries;
        Activated += OnActivationChanged;
    }

    public bool IsOpenFor(string folder) =>
        _chrome.IsVisible && PathsEqual(_currentFolder, folder);

    public bool WasJustDismissed(string folder) =>
        PathsEqual(_lastDismissedFolder, folder) &&
        DateTime.UtcNow - _lastDismissedAt < ToggleWindow;

    public void ShowForFolder(string folder)
    {
        _currentFolder = Path.GetFullPath(folder);
        _navigationStack.Clear();
        // Идентичность окна на панели задач — закреплённая (корневая) папка,
        // навигация внутрь вложенных папок её не меняет
        WindowAumid.Apply(_chrome.Hwnd, FolderAumid.For(_currentFolder));
        // Привязка к точке клика по значку панели задач: последующие Reload
        // (навигация по папкам) не должны двигать окно за курсором
        _chrome.AnchorToCursor();
        SetTitle(_currentFolder);
        Reload();
        _shownAt = DateTime.UtcNow;
        _chrome.Show();
        Activate();
    }

    public void Dismiss()
    {
        if (_chrome.IsVisible)
        {
            _lastDismissedFolder = _currentFolder;
            _lastDismissedAt = DateTime.UtcNow;
        }
        _chrome.Hide();
    }

    private static bool PathsEqual(string? left, string right) =>
        left is not null &&
        string.Equals(left, Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void SetTitle(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        TitleText.Text = string.IsNullOrEmpty(name) ? folder : name;
    }

    private void Reload()
    {
        _entries.Clear();
        if (_currentFolder is null) return;

        var entries = FolderReader.Read(_currentFolder);
        foreach (var entry in entries)
            _entries.Add(entry);

        EmptyText.Visibility = entries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        BackButton.Visibility = _navigationStack.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _ = _thumbnails.LoadAsync(entries);
        ResizeToFit(entries.Count);
        _chrome.MoveToTaskbarArea();
    }

    private void ResizeToFit(int count)
    {
        var (width, height) = _gridMode
            ? GridDimensions(count)
            : ListDimensions(count);
        var scale = _chrome.Scale;
        _chrome.Resize((int)(width * scale), (int)(height * scale));
    }

    private static (int Width, int Height) GridDimensions(int count)
    {
        var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(count, 1))), 3, 6);
        var rows = Math.Clamp((int)Math.Ceiling(count / (double)columns), 1, 5);
        return (columns * 100 + 40, rows * 96 + 60);
    }

    private static (int Width, int Height) ListDimensions(int count) =>
        (340, Math.Clamp(count, 1, 14) * 36 + 60);

    private void OnActivationChanged(object sender, WindowActivatedEventArgs e)
    {
        var deactivated = e.WindowActivationState == WindowActivationState.Deactivated;

        if (deactivated && !_shareInProgress && DateTime.UtcNow - _shownAt > DismissGracePeriod)
            Dismiss();

        if (!deactivated)
            _shareInProgress = false;
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FolderEntry entry)
            OpenEntry(entry);
    }

    private void OpenEntry(FolderEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (_currentFolder is not null)
                _navigationStack.Push(_currentFolder);
            _currentFolder = entry.FullPath;
            TitleText.Text = entry.Name;
            Reload();
            return;
        }
        Shell.Open(entry.FullPath);
        Dismiss();
    }

    private void OnNavigateBack(object sender, RoutedEventArgs e)
    {
        if (_navigationStack.Count == 0) return;
        _currentFolder = _navigationStack.Pop();
        SetTitle(_currentFolder);
        Reload();
    }

    private async void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        try
        {
            var entries = e.Items.OfType<FolderEntry>().ToList();
            var items = await TypedDataPackage.ResolveStorageItemsAsync(entries);
            if (items.Count == 0)
            {
                e.Cancel = true;
                return;
            }
            TypedDataPackage.Fill(e.Data, entries, items);
        }
        catch
        {
            e.Cancel = true;
        }
    }

    private static FolderEntry? EntryFrom(object sender) =>
        (sender as FrameworkElement)?.DataContext as FolderEntry;

    private void OnOpenItem(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { } entry)
            OpenEntry(entry);
    }

    private async void OnCopyItem(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { } entry) return;
        try
        {
            var entries = new List<FolderEntry> { entry };
            var items = await TypedDataPackage.ResolveStorageItemsAsync(entries);
            var package = new DataPackage();
            TypedDataPackage.Fill(package, entries, items);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch { }
    }

    private void OnShareItem(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { } entry) return;

        _shareEntry = entry;
        _shareInProgress = true;
        try
        {
            if (_shareManager is null)
            {
                _shareManager = _chrome.GetShareManager();
                _shareManager.DataRequested += OnShareDataRequested;
            }
            _chrome.ShowShareUI();
        }
        catch
        {
            _shareInProgress = false;
        }
    }

    private void OnShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_shareEntry is not { } entry)
        {
            args.Request.FailWithDisplayText("Нет элемента для отправки");
            return;
        }
        var deferral = args.Request.GetDeferral();
        _ = FillShareRequestAsync(args.Request, entry, deferral);
    }

    private static async Task FillShareRequestAsync(
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
        }
    }

    private void OnRevealItem(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { } entry) return;
        Shell.RevealInExplorer(entry.FullPath);
        Dismiss();
    }

    private void OnToggleView(object sender, RoutedEventArgs e)
    {
        _gridMode = !_gridMode;
        ItemsGrid.Visibility = _gridMode ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = _gridMode ? Visibility.Collapsed : Visibility.Visible;
        ViewToggleIcon.Glyph = _gridMode ? "\uE8FD" : "\uE80A";
        ResizeToFit(_entries.Count);
        _chrome.MoveToTaskbarArea();
    }

    private void OnOpenInExplorer(object sender, RoutedEventArgs e)
    {
        if (_currentFolder is null) return;
        Shell.OpenFolder(_currentFolder);
        Dismiss();
    }
}
