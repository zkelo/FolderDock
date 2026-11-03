namespace FolderDock.Core;

public interface IFolderSearch
{
    Task<RecursiveSearchResult> SearchAsync(
        string folder, string? query, CancellationToken cancellationToken = default);
}
