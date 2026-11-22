namespace FolderDock.Core;

public sealed record FolderPreferences(
    FolderSortOrder Sort = FolderSortOrder.Name,
    FolderViewMode View = FolderViewMode.Grid);
