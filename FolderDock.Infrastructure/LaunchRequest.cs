namespace FolderDock.Infrastructure;

public enum LaunchKind
{
    Manager,
    Folder,
    Background
}

public sealed record LaunchRequest
{
    public LaunchKind Kind { get; }
    public string? FolderPath { get; }

    private LaunchRequest(LaunchKind kind, string? folderPath = null)
    {
        Kind = kind;
        FolderPath = folderPath;
    }

    public static LaunchRequest Manager { get; } = new(LaunchKind.Manager);
    public static LaunchRequest Background { get; } = new(LaunchKind.Background);

    public static LaunchRequest OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(LaunchKind.Folder, path);
    }
}
