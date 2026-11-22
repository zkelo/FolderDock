using System;
using System.Collections.Generic;
using System.Linq;

namespace FolderDock;

internal sealed class FolderItemSelection
{
    private HashSet<FolderEntry> _selected = new();

    public IReadOnlyCollection<FolderEntry> Items => _selected;

    public void Replace(IEnumerable<FolderEntry> entries)
    {
        var next = entries.ToHashSet();
        foreach (var entry in _selected)
            if (!next.Contains(entry)) entry.IsSelected = false;
        foreach (var entry in next)
            entry.IsSelected = true;
        _selected = next;
    }

    public void KeepDisplayed(IReadOnlyList<FolderEntry> entries)
    {
        if (_selected.Count > 0) Replace(entries.Where(_selected.Contains));
    }

    public IReadOnlyList<string> PathsFor(string origin, IReadOnlyList<FolderEntry> displayed)
    {
        if (_selected.Any(entry => string.Equals(entry.FullPath, origin, StringComparison.Ordinal)))
            return displayed.Where(_selected.Contains).Select(entry => entry.FullPath).ToArray();

        Replace(Array.Empty<FolderEntry>());
        return new[] { origin };
    }
}
