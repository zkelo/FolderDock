using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace FolderDock;

/// <summary>A Windows file drag whose lifetime follows the originating WinUI pointer.</summary>
internal interface IFileDragDrop
{
    Task<bool> StartAsync(UIElement source, PointerPoint pointer, string path);
}
