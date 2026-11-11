namespace FolderDock.Core;

public sealed record ReleaseInfo(string Tag, IReadOnlyList<ReleaseAsset> Assets);
