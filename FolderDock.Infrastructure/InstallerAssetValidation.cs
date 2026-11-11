namespace FolderDock.Infrastructure;

internal static class InstallerAssetValidation
{
    public static bool IsValid(string name, string url) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.IndexOfAny(['/', '\\', ':']) < 0 &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        name is not "." and not ".." &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;
}
