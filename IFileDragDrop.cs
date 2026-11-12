namespace FolderDock;

/// <summary>A synchronous Windows file drag, isolated from pointer event handling.</summary>
internal interface IFileDragDrop
{
    bool Start(nint owner, string path);
}
