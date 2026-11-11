namespace FolderDock.Core;

/// Navigation and query state for one popup. Paths are normalized by the caller.
public sealed class FolderNavigation
{
    private readonly Stack<string> _parents = new();

    public string? RootFolder { get; private set; }
    public string? CurrentFolder { get; private set; }
    public string SearchQuery { get; private set; } = string.Empty;
    public bool HasSearchQuery => SearchQuery.Length > 0;
    public bool CanNavigateBack => _parents.Count > 0;

    public void Begin(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        RootFolder = folder;
        CurrentFolder = folder;
        _parents.Clear();
        ClearSearch();
    }

    public void NavigateTo(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (CurrentFolder is null)
            throw new InvalidOperationException("A root folder must be opened before navigating.");

        _parents.Push(CurrentFolder);
        CurrentFolder = folder;
        ClearSearch();
    }

    public bool TryNavigateBack()
    {
        if (!_parents.TryPop(out var parent)) return false;
        CurrentFolder = parent;
        ClearSearch();
        return true;
    }

    public bool SetSearchQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var trimmed = query.Trim();
        if (SearchQuery == trimmed) return false;
        SearchQuery = trimmed;
        return true;
    }

    public void ClearSearch() => SearchQuery = string.Empty;
}
