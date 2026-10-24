using System;
using System.IO;

namespace FolderDock;

/// <summary>Observes file operations that can finish after the Shell drag loop returns.</summary>
internal sealed class FolderChangeMonitor : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public FolderChangeMonitor(string folder, bool recursive, Action changed)
    {
        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = recursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };
        _watcher.Created += (_, _) => changed();
        _watcher.Deleted += (_, _) => changed();
        _watcher.Renamed += (_, _) => changed();
        _watcher.Error += (_, _) => changed();
        try
        {
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            _watcher.Dispose();
            throw;
        }
    }

    public void Dispose() => _watcher.Dispose();
}
