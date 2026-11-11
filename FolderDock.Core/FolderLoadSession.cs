namespace FolderDock.Core;

/// Owns cancellation and latest-request-wins policy independently of a window.
public sealed class FolderLoadSession
{
    private readonly IFolderSnapshotReader _reader;
    private readonly LatestRequestSession<FolderSnapshot> _requests = new();

    public FolderLoadSession(IFolderSnapshotReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public bool IsLoading => _requests.IsPending;

    /// Returns null for a cancelled or superseded read. Check IsCurrent after
    /// awaiting, before presenting a completion on the UI thread.
    public async Task<FolderLoadCompletion?> LoadAsync(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var completion = await _requests.RunAsync(token => _reader.ReadAsync(folder, token))
            .ConfigureAwait(false);
        return completion is null ? null
            : new FolderLoadCompletion(completion.RequestId, completion.Result);
    }

    public bool IsCurrent(FolderLoadCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return _requests.IsCurrent(completion.RequestId);
    }

    public void Cancel() => _requests.Cancel();
}
