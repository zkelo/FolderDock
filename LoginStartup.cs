using System;
using System.IO;
using Microsoft.Win32;

namespace FolderDock;

/// Current-user login startup. Windows owns the separate Startup Apps approval;
/// never modify it, so disabling FolderDock in Windows remains effective.
public static class LoginStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string PreferencesKey = @"Software\FolderDock";
    private const string ValueName = "FolderDock";
    private const string ConfiguredValue = "StartupConfigured";
    private static readonly object Sync = new();

    public static string? LastError { get; private set; }

    public static bool IsRegistered
    {
        get
        {
            lock (Sync)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string command && command.Length > 0;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return false;
                }
            }
        }
    }

    /// Enable once on the first foreground launch. Later launches only repair
    /// the path of an existing registration (for example after a portable move).
    /// An externally removed entry is deliberately left absent.
    public static string? EnsureConfigured()
    {
        lock (Sync)
        {
            try
            {
                using var preferences = Registry.CurrentUser.OpenSubKey(PreferencesKey);
                if (preferences?.GetValue(ConfiguredValue) is not int configured || configured != 1)
                {
                    TrySetEnabled(true, out var error);
                    return error;
                }

                using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (run?.GetValue(ValueName) is string existing && existing.Length > 0)
                {
                    var command = StartupCommand();
                    if (!string.Equals(existing, command, StringComparison.OrdinalIgnoreCase))
                        run.SetValue(ValueName, command, RegistryValueKind.String);
                }
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return LastError;
        }
    }

    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        lock (Sync)
        {
            try
            {
                // Open both keys before changing either value. Marking the
                // preference first prevents a later launch undoing a disable.
                using var preferences = Registry.CurrentUser.CreateSubKey(PreferencesKey);
                using var run = Registry.CurrentUser.CreateSubKey(RunKey);
                var command = enabled ? StartupCommand() : null;
                preferences.SetValue(ConfiguredValue, 1, RegistryValueKind.DWord);
                if (enabled)
                    run.SetValue(ValueName, command!, RegistryValueKind.String);
                else
                    run.DeleteValue(ValueName, throwOnMissingValue: false);
                error = LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                error = LastError = ex.Message;
                return false;
            }
        }
    }

    private static string StartupCommand()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable)
            || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FolderDock executable path is unavailable.");

        var command = $"\"{executable}\" --background";
        // The Run key contract limits its command line to 260 characters.
        if (command.Length > 260)
            throw new InvalidOperationException("The FolderDock installation path is too long for login startup.");
        return command;
    }
}
