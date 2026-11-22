using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace FolderDock;

/// <summary>Uses WinUI's pointer lifecycle with the actual Windows Shell file data.</summary>
internal sealed class ShellDragDrop : IFileDragDrop
{
    private const DataPackageOperation AllowedOperations =
        DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;

    public async Task<bool> StartAsync(UIElement source, PointerPoint pointer, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pointer);
        Exception? preparationFailure = null;

        void OnDragStarting(UIElement sender, DragStartingEventArgs args)
        {
            try
            {
                // Keep native file, directory and shortcut identity. Do not
                // substitute text/bitmap representations or resolve .lnk targets.
                ShellDataPackage.SetFiles(args.Data, paths);
                args.AllowedOperations = AllowedOperations;
            }
            catch (Exception exception)
            {
                preparationFailure = exception;
                args.Cancel = true;
            }
        }

        source.DragStarting += OnDragStarting;
        try
        {
            var result = await source.StartDragAsync(pointer);
            if (preparationFailure is not null)
                ExceptionDispatchInfo.Capture(preparationFailure).Throw();
            return result != DataPackageOperation.None;
        }
        finally
        {
            source.DragStarting -= OnDragStarting;
        }
    }
}
