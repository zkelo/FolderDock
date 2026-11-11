namespace FolderDock.Core;

public sealed record SettingsResult(AppSettings Settings, string? Error = null);
