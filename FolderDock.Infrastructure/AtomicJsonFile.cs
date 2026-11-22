using System.Security;
using System.Text.Json;
using FolderDock.Core;

namespace FolderDock.Infrastructure;

internal static class AtomicJsonFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    // Windows can reject replacement while this process holds the destination open.
    private static readonly object FileAccessGate = new();

    public static T? Read<T>(string path)
    {
        lock (FileAccessGate)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return JsonSerializer.Deserialize<T>(stream);
        }
    }

    public static bool Write<T>(string path, T document, bool overwrite = true)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporaryPath = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, document, Options);

            lock (FileAccessGate)
                return Commit(temporaryPath, path, overwrite);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
        finally
        {
            RemoveTemporaryFile(temporaryPath);
        }
    }

    public static void Delete(string path)
    {
        try
        {
            lock (FileAccessGate)
                File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // An absent directory has no settings to reset.
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new SettingsStoreException(exception.Message, exception);
        }
    }

    private static bool Commit(string temporaryPath, string path, bool overwrite)
    {
        try
        {
            File.Move(temporaryPath, path, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            return false;
        }
    }

    private static void RemoveTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            System.Diagnostics.Trace.WriteLine($"FolderDock: settings cleanup failed: {exception.Message}");
        }
    }

    internal static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;
}
