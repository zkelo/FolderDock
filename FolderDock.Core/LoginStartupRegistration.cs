namespace FolderDock.Core;

public sealed record LoginStartupRegistration(
    bool HasUserPreference,
    bool IsRegistered,
    bool UsesCurrentExecutable);
