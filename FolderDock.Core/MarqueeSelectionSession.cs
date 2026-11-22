namespace FolderDock.Core;

/// <summary>Tracks item bounds in content coordinates for a single selection gesture.</summary>
public sealed class MarqueeSelectionSession<T> where T : notnull
{
    private readonly Dictionary<T, SelectionBounds> _itemBounds = new();
    private readonly bool _addToSelection;

    public MarqueeSelectionSession(IEnumerable<T> previousSelection, bool addToSelection)
    {
        PreviousSelection = previousSelection.ToHashSet();
        _addToSelection = addToSelection;
    }

    public IReadOnlyCollection<T> PreviousSelection { get; }

    // Keep offscreen bounds when their item containers are recycled during scrolling.
    public void RecordBounds(T item, SelectionBounds bounds) => _itemBounds[item] = bounds;

    public IReadOnlyCollection<T> ItemsIn(SelectionBounds bounds)
    {
        var selected = _addToSelection ? new HashSet<T>(PreviousSelection) : new HashSet<T>();
        foreach (var (item, itemBounds) in _itemBounds)
        {
            if (bounds.Intersects(itemBounds))
                selected.Add(item);
        }
        return selected;
    }
}
