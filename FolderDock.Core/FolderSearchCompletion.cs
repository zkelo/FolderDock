namespace FolderDock.Core;

/// A search receipt that must still be current when the UI presents it.
public sealed class FolderSearchCompletion
{
    internal long RequestId { get; }
    public RecursiveSearchResult Result { get; }

    internal FolderSearchCompletion(long requestId, RecursiveSearchResult result)
    {
        RequestId = requestId;
        Result = result;
    }
}
