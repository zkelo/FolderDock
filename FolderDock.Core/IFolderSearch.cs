namespace FolderDock.Core;

public interface IFolderSearch
{
    Task<RecursiveSearchResult> SearchAsync(
        string folder, string? query, FolderSortOrder sortOrder = FolderSortOrder.Name,
        CancellationToken cancellationToken = default);
}
