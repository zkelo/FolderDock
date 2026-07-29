using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FolderDock;

public sealed class PinInfo
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required string Lnk { get; init; }
}

public sealed partial class ManagerWindow : Window
{
    private readonly ObservableCollection<PinInfo> _pins = new();

    private static string ShortcutsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolderDock", "Shortcuts");

    public ManagerWindow()
    {
        InitializeComponent();
        PinsList.ItemsSource = _pins;
        LoadExistingShortcuts();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(720, 560));
    }

    private void LoadExistingShortcuts()
    {
        _pins.Clear();
        if (!Directory.Exists(ShortcutsDir)) return;

        foreach (var lnk in Directory.EnumerateFiles(ShortcutsDir, "*.lnk"))
        {
            _pins.Add(new PinInfo
            {
                Name = Path.GetFileNameWithoutExtension(lnk),
                Folder = "(ярлык создан ранее)",
                Lnk = lnk
            });
        }
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        try
        {
            var lnk = ShortcutFactory.CreateFolderShortcut(folder, ShortcutsDir);
            _pins.Add(new PinInfo
            {
                Name = Path.GetFileNameWithoutExtension(lnk),
                Folder = folder,
                Lnk = lnk
            });
            Shell.RevealInExplorer(lnk);
            await ShowDialogAsync("Ярлык создан",
                "В открывшемся Проводнике: ПКМ по ярлыку → «Показать дополнительные параметры» → " +
                "«Закрепить на панели задач».\n\n" +
                "После закрепления клик по значку откроет папку каскадом.");
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("Ошибка", ex.Message);
        }
    }

    private async System.Threading.Tasks.Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async System.Threading.Tasks.Task ShowDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnRevealShortcut(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string lnk)
            Shell.RevealInExplorer(lnk);
    }
}
