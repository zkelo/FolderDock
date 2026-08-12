using System.Diagnostics;

namespace FolderDock;

public static class Shell
{
    public static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Ассоциация сломана или файл удалён между показом и кликом;
            // shell сам показывает свой диалог ошибки в большинстве случаев
        }
    }

    public static void OpenFolder(string folder) =>
        StartExplorer($"\"{folder}\"");

    public static void RevealInExplorer(string path) =>
        StartExplorer($"/select,\"{path}\"");

    private static void StartExplorer(string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch
        {
            // explorer.exe недоступен только в сильно урезанных средах;
            // падать из-за вспомогательного действия не стоит
        }
    }
}
