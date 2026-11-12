using System.Security;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

public sealed class FileSystemFolderChangeSource : IFolderChangeSource
{
    public IDisposable Watch(string folder, bool recursive, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        try
        {
            return new Subscription(folder, recursive, changed);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or ArgumentException or SecurityException)
        {
            throw new FolderWatchException("Folder changes could not be observed.", exception);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action _changed;
        private int _disposed;

        public Subscription(string folder, bool recursive, Action changed)
        {
            _changed = changed;
            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Created += OnEntryChanged;
            _watcher.Deleted += OnEntryChanged;
            _watcher.Renamed += OnEntryRenamed;
            _watcher.Error += OnWatchError;
            try
            {
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void OnEntryChanged(object sender, FileSystemEventArgs args) => NotifyChanged();

        private void OnEntryRenamed(object sender, RenamedEventArgs args) => NotifyChanged();

        // Lost notifications require the same full refresh as an ordinary change.
        private void OnWatchError(object sender, ErrorEventArgs args) => NotifyChanged();

        private void NotifyChanged()
        {
            if (Volatile.Read(ref _disposed) == 0)
                _changed();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _watcher.Dispose();
        }
    }
}
