namespace FolderDock.Core;

/// <summary>Observes changes that can complete after a file operation returns.</summary>
public interface IFolderChangeSource
{
    /// <remarks>
    /// Notifications may arrive on a worker thread. Dispose the subscription when
    /// leaving the folder; a callback already in flight can still complete.
    /// </remarks>
    IDisposable Watch(string folder, bool recursive, Action changed);
}
