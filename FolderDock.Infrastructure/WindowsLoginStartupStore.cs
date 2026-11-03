using System.Runtime.Versioning;
using System.Security;
using FolderDock.Core;
using Microsoft.Win32;

namespace FolderDock.Infrastructure;

/// Current-user registry adapter. Windows owns Startup Apps approval separately;
/// this adapter never reads or changes the StartupApproved keys.
[SupportedOSPlatform("windows")]
public sealed class WindowsLoginStartupStore : ILoginStartupStore
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string PreferencesKey = @"Software\FolderDock";
    private const string ValueName = "FolderDock";
    private const string ConfiguredValue = "StartupConfigured";
    private const int MaximumCommandLength = 260;
    private readonly string _executablePath;

    public WindowsLoginStartupStore(string executablePath)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        _executablePath = executablePath;
    }

    public LoginStartupRegistration ReadRegistration() => AccessRegistry(() =>
    {
        using var preferences = Registry.CurrentUser.OpenSubKey(PreferencesKey);
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        var hasPreference = preferences?.GetValue(ConfiguredValue) is int value && value == 1;
        var command = run?.GetValue(ValueName) as string;
        var isRegistered = !string.IsNullOrEmpty(command);
        var isCurrent = isRegistered &&
            string.Equals(command, CreateStartupCommand(), StringComparison.OrdinalIgnoreCase);
        return new LoginStartupRegistration(hasPreference, isRegistered, isCurrent);
    });

    public void Enable() => AccessRegistry(() =>
    {
        var command = CreateStartupCommand();
        using var preferences = Registry.CurrentUser.CreateSubKey(PreferencesKey);
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        run.SetValue(ValueName, command, RegistryValueKind.String);
        // A failed first registration must remain eligible for a later retry.
        preferences.SetValue(ConfiguredValue, 1, RegistryValueKind.DWord);
    });

    public void Disable() => AccessRegistry(() =>
    {
        using var preferences = Registry.CurrentUser.CreateSubKey(PreferencesKey);
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        // Record the choice first so a later launch cannot recreate a removed
        // entry if recording the preference itself failed.
        preferences.SetValue(ConfiguredValue, 1, RegistryValueKind.DWord);
        run.DeleteValue(ValueName, throwOnMissingValue: false);
    });

    private string CreateStartupCommand()
    {
        if (string.IsNullOrWhiteSpace(_executablePath) ||
            !Path.IsPathFullyQualified(_executablePath) ||
            _executablePath.Contains('"') ||
            !string.Equals(Path.GetExtension(_executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new LoginStartupException(LoginStartupError.InvalidExecutable,
                "FolderDock executable path is unavailable or invalid.");

        var command = $"\"{_executablePath}\" --background";
        if (command.Length > MaximumCommandLength)
            throw new LoginStartupException(LoginStartupError.CommandTooLong,
                "The FolderDock installation path is too long for login startup.");
        return command;
    }

    private static void AccessRegistry(Action operation) => AccessRegistry(() =>
    {
        operation();
        return true;
    });

    private static T AccessRegistry<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            throw new LoginStartupException(LoginStartupError.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            throw new LoginStartupException(LoginStartupError.StorageUnavailable, exception.Message);
        }
    }
}
