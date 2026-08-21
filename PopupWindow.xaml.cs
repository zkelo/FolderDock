using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace FolderDock;

public sealed partial class PopupWindow : Window
{
    /// Показ ранее 300 мс: SetForegroundWindow из фонового процесса может быть
    /// отклонён Windows → ложный Deactivated сразу после Show.
    private static readonly TimeSpan DismissGracePeriod = TimeSpan.FromMilliseconds(300);

    /// Дебаунс повторного клика по значку: light-dismiss прячет попап ДО того,
    /// как придёт перенаправленная активация, иначе toggle мгновенно переоткрывает.
    private static readonly TimeSpan ToggleDebounceInterval = TimeSpan.FromMilliseconds(600);

    // Segoe Fluent Icons; дублируют стартовые Glyph в PopupWindow.xaml
    private const string GridModeGlyph = "\uE8FD"; // ViewAll (сетка)
    private const string ListModeGlyph = "\uE80A"; // ViewList (список)

    private readonly PopupChrome _chrome;
    private readonly ShareCoordinator _share;
    private readonly ThumbnailLoader _thumbnails = new();
    private readonly ObservableCollection<FolderEntry> _entries = new();
    private readonly Stack<string> _navigationStack = new();

    /// Закреплённая (корневая) папка попапа. Задаёт идентичность окна на панели
    /// задач и участвует в toggle-логике; навигация вглубь её НЕ меняет.
    private string? _rootFolder;

    /// Папка, содержимое которой сейчас показано (меняется при навигации).
    private string? _currentFolder;

    private bool _gridMode = true;
    private bool _isActive;
    private bool _shellMenuOpen;
    private string? _lastDismissedFolder;
    private DateTime _lastDismissedAt;
    private DateTime _shownAt;

    public PopupWindow()
    {
        InitializeComponent();
        _chrome = new PopupChrome(this);
        _share = new ShareCoordinator(_chrome, shareFinished: HideIfInactive);
        ItemsGrid.ItemsSource = _entries;
        ItemsList.ItemsSource = _entries;
        ApplyTooltips();
        Activated += OnActivationChanged;
    }

    private void ApplyTooltips()
    {
        // Тултипы из ресурсов: x:Uid не умеет attached-свойства с обычным Text
        ToolTipService.SetToolTip(BackButton, Loc.Get("Tooltip_Back"));
        ToolTipService.SetToolTip(ViewToggle, Loc.Get("Tooltip_ViewToggle"));
        ToolTipService.SetToolTip(OpenExplorerButton, Loc.Get("Tooltip_OpenInExplorer"));
    }

    public bool IsOpenFor(string folder) =>
        _chrome.IsVisible && PathsEqual(_rootFolder, folder);

    public bool WasJustDismissed(string folder) =>
        PathsEqual(_lastDismissedFolder, folder) &&
        DateTime.UtcNow - _lastDismissedAt < ToggleDebounceInterval;

    public void ShowForFolder(string folder)
    {
        _rootFolder = Path.GetFullPath(folder);
        _currentFolder = _rootFolder;
        _navigationStack.Clear();
        // Идентичность окна на панели задач — закреплённая (корневая) папка
        WindowAumid.Apply(_chrome.Hwnd, FolderAumid.For(_rootFolder));
        // Привязка к точке клика по значку панели задач: последующие Reload
        // (навигация по папкам) не должны двигать окно за курсором
        _chrome.AnchorToCursor();
        SetTitle(_rootFolder);
        Reload();
        _shownAt = DateTime.UtcNow;
        _chrome.Show();
        Activate();
    }

    public void Dismiss()
    {
        if (_chrome.IsVisible)
        {
            // Для toggle-дебаунса запоминаем КОРНЕВУЮ папку: повторный клик
            // по значку приходит с ней, даже если внутри попапа ушли вглубь
            _lastDismissedFolder = _rootFolder;
            _lastDismissedAt = DateTime.UtcNow;
        }
        _thumbnails.CancelPending(); // не грузим миниатюры скрытому окну
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

        var contents = FolderReader.Read(_currentFolder);
        foreach (var entry in contents.Entries)
            _entries.Add(entry);

        UpdateEmptyState(contents);

        BackButton.Visibility = _navigationStack.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Fire-and-forget осознанно: LoadAsync глотает ошибки по-элементно
        // и отменяется следующим вызовом / Dismiss
        _ = _thumbnails.LoadAsync(contents.Entries);
        ResizeToFit(contents.Entries.Count);
        _chrome.MoveToTaskbarArea();
    }

    private void UpdateEmptyState(FolderContents contents)
    {
        EmptyText.Text = Loc.Get(contents.Failed ? "Folder_AccessDenied" : "Folder_Empty");
        EmptyText.Visibility = contents.Entries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ResizeToFit(int count)
    {
        var (width, height) = PopupLayout.Measure(count, _gridMode);
        var scale = _chrome.Scale;
        _chrome.Resize((int)(width * scale), (int)(height * scale));
    }

    private void OnActivationChanged(object sender, WindowActivatedEventArgs e)
    {
        var deactivated = e.WindowActivationState == WindowActivationState.Deactivated;
        _isActive = !deactivated;

        if (deactivated && !_share.IsShareInProgress && !_shellMenuOpen &&
            DateTime.UtcNow - _shownAt > DismissGracePeriod)
            Dismiss();

        if (!deactivated)
            _share.Reset();
    }

    private void HideIfInactive()
    {
        // Share завершён (панель закрыта / данные переданы): если фокус так и
        // не вернулся к попапу, не оставляем always-on-top окно на экране
        DispatchToUi(() =>
        {
            if (!_isActive && _chrome.IsVisible)
                Dismiss();
        });
    }

    private void DispatchToUi(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else DispatcherQueue.TryEnqueue(() => action());
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

    private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // Событие синхронное: e.Data нужно заполнить не выходя в асинхронность,
        // иначе драг стартует с пустым пакетом (см. TypedDataPackage.FillForDrag)
        var entries = e.Items.OfType<FolderEntry>().ToList();
        if (entries.Count == 0)
        {
            e.Cancel = true;
            return;
        }

        // Move по умолчанию (как в Проводнике), Copy при зажатом Ctrl.
        // Приёмник вправе скорректировать операцию своими модификаторами при дропе.
        var ctrlHeld = (Native.GetKeyState(Native.VK_CONTROL) & 0x8000) != 0;
        TypedDataPackage.FillForDrag(e.Data, entries,
            ctrlHeld ? DataPackageOperation.Copy : DataPackageOperation.Move);
    }

    private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // После перемещения исходные элементы исчезли из папки — обновляем список
        if (args.DropResult == DataPackageOperation.Move)
            Reload();
    }

    private static FolderEntry? EntryFrom(object sender) =>
        (sender as FrameworkElement)?.DataContext as FolderEntry;

    private void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (EntryFrom(sender) is not { } entry) return;

        // TrackPopupMenuEx крутит модальный цикл на UI-потоке; флаг не даёт
        // light-dismiss спрятать попап, если Windows дёрнет Deactivated
        _shellMenuOpen = true;
        try
        {
            ShellContextMenu.Show(_chrome.Hwnd, entry.FullPath);
        }
        catch
        {
            // Сломанное shell-расширение или файл исчез — попап важнее меню
        }
        finally
        {
            _shellMenuOpen = false;
        }

        // Команда могла изменить папку (удаление, переименование, вставка);
        // InvokeCommand асинхронен на стороне шелла, но обновиться дёшево
        Reload();
    }

    private void OnToggleView(object sender, RoutedEventArgs e)
    {
        _gridMode = !_gridMode;
        ItemsGrid.Visibility = _gridMode ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = _gridMode ? Visibility.Collapsed : Visibility.Visible;
        ViewToggleIcon.Glyph = _gridMode ? GridModeGlyph : ListModeGlyph;
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
