namespace FolderDock.Core;

/// <summary>The lifetime and current availability of folder change notifications.</summary>
public interface IFolderChangeSubscription : IDisposable
{
    /// <summary>False after disposal or when observation has stopped after an error.</summary>
    bool IsWatching { get; }
}
