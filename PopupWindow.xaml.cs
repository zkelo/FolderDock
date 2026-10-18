using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDock.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace FolderDock;

public sealed partial class PopupWindow : Window
{
    /// Дебаунс повторного клика по значку: light-dismiss прячет попап ДО того,
    /// как придёт перенаправленная активация, иначе toggle мгновенно переоткрывает.
    private static readonly TimeSpan ToggleDebounceInterval = TimeSpan.FromMilliseconds(600);

    // Segoe Fluent Icons; дублируют стартовые Glyph в PopupWindow.xaml
    private const string GridModeGlyph = "\uE8FD"; // ViewAll (сетка)
    private const string ListModeGlyph = "\uE80A"; // ViewList (список)
    private const int InlineSearchWidth = 160;
    private const int InlineSearchGap = 8;
    private static readonly TimeSpan SearchDebounceInterval = TimeSpan.FromMilliseconds(180);

    private readonly PopupChrome _chrome;
    private readonly ShareCoordinator _share;
    private readonly PopupSession _session = new();
    private readonly ThumbnailLoader _thumbnails = new();
    private readonly ObservableCollection<FolderEntry> _entries = new();
    private readonly Stack<string> _navigationStack = new();
    private FolderContents _contents = new(Array.Empty<FolderEntry>(), Failed: false);
    private string _searchQuery = string.Empty;
    private CancellationTokenSource? _searchCancellation;

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
        _share = new ShareCoordinator(_chrome, shareFinished: OnShareCompleted);
        ItemsGrid.ItemsSource = _entries;
        ItemsList.ItemsSource = _entries;
        InitializeHeaderLabels();
        Activated += OnActivationChanged;
    }

    private void InitializeHeaderLabels()
    {
        // Тултипы из ресурсов: x:Uid не умеет attached-свойства с обычным Text
        ToolTipService.SetToolTip(BackButton, Loc.Get("Tooltip_Back"));
        ToolTipService.SetToolTip(ViewToggle, Loc.Get("Tooltip_ViewToggle"));
        ToolTipService.SetToolTip(OpenExplorerButton, Loc.Get("Tooltip_OpenInExplorer"));
        var closeLabel = Loc.Get("Tooltip_ClosePopup");
        ToolTipService.SetToolTip(CloseButton, closeLabel);
        AutomationProperties.SetName(CloseButton, closeLabel);
        var searchLabel = Loc.Get("Search_Placeholder");
        SearchBox.PlaceholderText = searchLabel;
        AutomationProperties.SetName(SearchBox, searchLabel);
        RenderPinState();
    }

    private void RenderPinState()
    {
        PinToggle.IsChecked = _session.IsPinned;
        var label = Loc.Get(_session.IsPinned ? "Tooltip_UnpinPopup" : "Tooltip_PinPopup");
        ToolTipService.SetToolTip(PinToggle, label);
        AutomationProperties.SetName(PinToggle, label);
    }

    public bool IsOpenFor(string folder) =>
        _chrome.IsVisible && PathsEqual(_rootFolder, folder);

    public bool WasJustDismissed(string folder) =>
        PathsEqual(_lastDismissedFolder, folder) &&
        DateTime.UtcNow - _lastDismissedAt < ToggleDebounceInterval;

    public void ShowForFolder(string folder)
    {
        _session.Begin();
        RenderPinState();
        _rootFolder = Path.GetFullPath(folder);
        _currentFolder = _rootFolder;
        _navigationStack.Clear();
        // Идентичность окна на панели задач — закреплённая (корневая) папка
        WindowAumid.Apply(_chrome.Hwnd, FolderAumid.For(_rootFolder));
        // Привязка к точке клика по значку панели задач: последующие Reload
        // (навигация по папкам) не должны двигать окно за курсором
        _chrome.AnchorToCursor();
        SetTitle(_rootFolder);
        Reload(resetSearch: true);
        _shownAt = DateTime.UtcNow;
        _chrome.Show();
        Activate();
        // Без фокуса на XAML-контроле Esc не попадает в KeyboardAccelerators.
        SearchBox.Focus(FocusState.Programmatic);
    }

    public void Dismiss() => RequestDismiss(PopupDismissalReason.Explicit);

    private void RequestDismiss(PopupDismissalReason reason)
    {
        var context = new PopupDismissalContext(
            IsActive: _isActive,
            IsVisible: _chrome.IsVisible,
            IsShareInProgress: _share.IsShareInProgress,
            IsShellMenuOpen: _shellMenuOpen,
            TimeSinceShown: DateTime.UtcNow - _shownAt);

        if (_session.ShouldDismiss(reason, context))
            HidePopup();
    }

    private void HidePopup()
    {
        if (_chrome.IsVisible)
        {
            // Для toggle-дебаунса запоминаем КОРНЕВУЮ папку: повторный клик
            // по значку приходит с ней, даже если внутри попапа ушли вглубь
            _lastDismissedFolder = _rootFolder;
            _lastDismissedAt = DateTime.UtcNow;
        }
        CancelSearch();
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

    private void Reload(bool resetSearch = false)
    {
        if (_currentFolder is null) return;

        _contents = FolderReader.Read(_currentFolder);
        if (resetSearch)
        {
            // Обновляем запрос до Text, чтобы событие не фильтровало повторно.
            _searchQuery = string.Empty;
            SearchBox.Text = string.Empty;
        }
        ApplySearch();

        BackButton.Visibility = _navigationStack.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        ResizeToFit();
        _chrome.MoveToTaskbarArea();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // TextChanged может прийти при InitializeComponent, до готовности окна.
        if (_currentFolder is null) return;
        var query = SearchBox.Text.Trim();
        if (query == _searchQuery) return;
        var wasSearching = _searchQuery.Length > 0;
        _searchQuery = query;
        ApplySearch();
        if (wasSearching != (_searchQuery.Length > 0))
        {
            ResizeToFit();
            _chrome.MoveToTaskbarArea();
        }
    }

    private void ApplySearch()
    {
        CancelSearch();
        _thumbnails.CancelPending();
        SearchStatus.Visibility = Visibility.Collapsed;
        if (_searchQuery.Length > 0 && _currentFolder is not null)
        {
            _entries.Clear();
            EmptyText.Text = Loc.Get("Search_InProgress");
            EmptyText.Visibility = Visibility.Visible;
            _ = SearchRecursivelyAsync(_currentFolder, _searchQuery);
            return;
        }

        var matches = FolderSearch.Filter(_contents.Entries, null, entry => entry.Name);
        ShowEntries(matches);
        EmptyText.Text = Loc.Get(_contents.Failed ? "Folder_AccessDenied" : "Folder_Empty");
        EmptyText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = null;
    }

    private async Task SearchRecursivelyAsync(string folder, string query)
    {
        using var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        var token = cancellation.Token;
        try
        {
            await Task.Delay(SearchDebounceInterval, token);
            var result = await Task.Run(() => RecursiveFolderSearch.Search(folder, query, token), token);
            // Старый запрос не должен менять результаты после навигации/закрытия.
            if (token.IsCancellationRequested || _searchCancellation != cancellation
                || !PathsEqual(_currentFolder, folder) || _searchQuery != query || !_chrome.IsVisible)
                return;

            var matches = result.Entries.Select(entry => new FolderEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                IsDirectory = entry.IsDirectory,
                RelativePath = Path.GetRelativePath(folder, entry.FullPath)
            }).ToArray();
            ShowEntries(matches);
            EmptyText.Text = Loc.Get(result.Failed ? "Folder_AccessDenied" : "Search_NoResults");
            EmptyText.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

            var status = new List<string>();
            if (result.Incomplete || (result.Failed && matches.Length > 0))
                status.Add(Loc.Get("Search_PartialResults"));
            if (result.LimitReached) status.Add(Loc.Get("Search_ResultLimit"));
            SearchStatus.Text = string.Join(" · ", status);
            SearchStatus.Visibility = status.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Новый запрос, переход или закрытие отменяют обход и задержку.
        }
        finally
        {
            if (_searchCancellation == cancellation)
                _searchCancellation = null;
        }
    }

    private void ShowEntries(IReadOnlyList<FolderEntry> matches)
    {
        _entries.Clear();
        foreach (var entry in matches)
            _entries.Add(entry);
        _ = _thumbnails.LoadAsync(matches.Where(entry => entry.Thumbnail is null).ToArray());
    }

    private void ResizeToFit()
    {
        // Все непустые запросы используют одинаковую высоту для результатов.
        var count = Math.Min(_contents.Entries.Count, FolderSearch.MaxResults);
        var (width, _) = PopupLayout.Measure(count, _gridMode);
        var inlineSearch = PlaceSearchInHeader(width);
        var searchMode = _searchQuery.Length > 0;
        var (_, height) = PopupLayout.Measure(searchMode ? FolderSearch.MaxResults : count,
            _gridMode, separateSearchRow: !inlineSearch);
        if (searchMode) height += 40; // сообщения о неполном поиске и лимите
        var scale = _chrome.Scale;
        _chrome.Resize((int)(width * scale), (int)(height * scale));
    }

    private bool PlaceSearchInHeader(int width)
    {
        // Измеряем полное название независимо от текущего обрезания в Grid.
        // Решение зависит от ширины окна, поэтому смена строки не создаёт цикл.
        var title = new TextBlock { Text = TitleText.Text, Style = TitleText.Style };
        var unconstrained = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);
        title.Measure(unconstrained);
        BackButton.Measure(unconstrained);
        HeaderActions.Measure(unconstrained);
        var requiredWidth = title.DesiredSize.Width + BackButton.DesiredSize.Width
            + HeaderActions.DesiredSize.Width + InlineSearchWidth + InlineSearchGap
            + Header.Padding.Left + Header.Padding.Right
            + Root.BorderThickness.Left + Root.BorderThickness.Right;
        var inline = Math.Ceiling(requiredWidth) <= width;

        Grid.SetRow(SearchBox, inline ? 0 : 1);
        Grid.SetColumn(SearchBox, inline ? 2 : 0);
        Grid.SetColumnSpan(SearchBox, inline ? 1 : 4);
        SearchColumn.Width = new GridLength(inline ? InlineSearchWidth + InlineSearchGap : 0);
        SearchBox.Margin = inline
            ? new Thickness(InlineSearchGap, 0, 0, 0)
            : new Thickness(2, 6, 2, 2);
        SearchBox.PlaceholderText = Loc.Get(inline ? "Search_CompactPlaceholder" : "Search_Placeholder");
        return inline;
    }

    private void OnActivationChanged(object sender, WindowActivatedEventArgs e)
    {
        _isActive = e.WindowActivationState != WindowActivationState.Deactivated;
        if (_isActive)
        {
            _share.Reset();
            return;
        }

        RequestDismiss(PopupDismissalReason.Deactivated);
    }

    private void OnShareCompleted()
    {
        // Состояние читается на UI-потоке после очереди: фокус или закрепление
        // могли измениться с момента завершения Share.
        DispatchToUi(() => RequestDismiss(PopupDismissalReason.ShareCompleted));
    }

    private void DispatchToUi(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else DispatcherQueue.TryEnqueue(() => action());
    }

    private async void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FolderEntry entry)
            await OpenEntryAsync(entry);
    }

    private async Task OpenEntryAsync(FolderEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (_currentFolder is not null)
                _navigationStack.Push(_currentFolder);
            _currentFolder = entry.FullPath;
            TitleText.Text = entry.Name;
            Reload(resetSearch: true);
            return;
        }

        // Незакреплённый попап прячем до запуска чужого окна, чтобы
        // light-dismiss не зависел от порядка событий активации.
        RequestDismiss(PopupDismissalReason.ExternalLaunch);

        // Launcher с NeighboringFilesQuery — просмотрщик получает соседей
        // по папке (листание стрелками, как из Проводника); при неудаче
        // (exe, недоступная папка) — обычный ShellExecute
        if (!await Shell.OpenWithNeighborsAsync(entry.FullPath))
            Shell.Open(entry.FullPath);
    }

    private void OnNavigateBack(object sender, RoutedEventArgs e)
    {
        if (_navigationStack.Count == 0) return;
        _currentFolder = _navigationStack.Pop();
        SetTitle(_currentFolder);
        Reload(resetSearch: true);
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
        ResizeToFit();
        _chrome.MoveToTaskbarArea();
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        _session.TogglePin();
        RenderPinState();
    }

    private void OnClosePopup(object sender, RoutedEventArgs e) => Dismiss();

    private void OnDismissAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (SearchBox.Text.Length > 0)
        {
            SearchBox.Text = string.Empty;
            SearchBox.Focus(FocusState.Programmatic);
            return;
        }
        Dismiss();
    }

    private void OnSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private void OnOpenInExplorer(object sender, RoutedEventArgs e)
    {
        if (_currentFolder is null) return;
        Shell.OpenFolder(_currentFolder);
        RequestDismiss(PopupDismissalReason.ExternalLaunch);
    }
}
