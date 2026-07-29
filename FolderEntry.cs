using System.ComponentModel;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FolderDock;

public sealed class FolderEntry : INotifyPropertyChanged
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }

    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static FolderEntry Directory(string path) => new()
    {
        Name = Path.GetFileName(path),
        FullPath = path,
        IsDirectory = true
    };

    public static FolderEntry File(string path) => new()
    {
        Name = Path.GetFileName(path),
        FullPath = path,
        IsDirectory = false
    };
}
