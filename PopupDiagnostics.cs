using System;
using System.IO;
using System.Security;

namespace FolderDock;

internal static class PopupDiagnostics
{
    private static readonly string? LogPath = Environment.GetEnvironmentVariable("FOLDERDOCK_POPUP_LOG");

    internal static bool IsEnabled => LogPath is { Length: > 0 } && Path.IsPathFullyQualified(LogPath);

    internal static void Write(nint hwnd, string stage, string details)
    {
        if (!IsEnabled) return;

        try
        {
            File.AppendAllText(LogPath!, $"{DateTime.UtcNow:O} pid={Environment.ProcessId} " +
                $"hwnd=0x{hwnd:X} {stage} {details}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                           or ArgumentException or NotSupportedException or SecurityException)
        {
            // A diagnostic file must not interfere with popup behavior.
        }
    }
}
