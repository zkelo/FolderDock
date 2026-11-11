namespace FolderDock.Core;

/// Debounces searches and rejects cancelled or superseded results.
public sealed class FolderSearchSession
{
    public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(180);

    private readonly IFolderSearch _search;
    private readonly TimeSpan _debounceInterval;
    private readonly TimeProvider _timeProvider;
    private readonly LatestRequestSession<RecursiveSearchResult> _requests = new();

    public FolderSearchSession(IFolderSearch search, TimeSpan? debounceInterval = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(search);
        _search = search;
        _debounceInterval = debounceInterval ?? DefaultDebounceInterval;
        if (_debounceInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(debounceInterval));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsSearching => _requests.IsPending;

    public async Task<FolderSearchCompletion?> SearchAsync(string folder, string query)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(query);
        var completion = await _requests.RunAsync(async token =>
        {
            await Task.Delay(_debounceInterval, _timeProvider, token).ConfigureAwait(false);
            return await _search.SearchAsync(folder, query, token).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return completion is null ? null
            : new FolderSearchCompletion(completion.RequestId, completion.Result);
    }

    public bool IsCurrent(FolderSearchCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return _requests.IsCurrent(completion.RequestId);
    }

    public void Cancel() => _requests.Cancel();
}
