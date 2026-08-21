using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;

namespace FolderDock;

public static class Shell
{
    /// Открытие файла с контекстом соседей по папке: просмотрщики («Фото»
    /// и другие) получают NeighboringFilesQuery и включают листание
    /// стрелками — ровно как при открытии из Проводника. Обычный
    /// ShellExecute открывает файл «в вакууме», и стрелки не работают.
    /// false — запуск не удался (Launcher, в частности, отказывается
    /// запускать исполняемые файлы) → вызывающий откатывается на Open().
    public static async Task<bool> OpenWithNeighborsAsync(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return false;

            var file = await StorageFile.GetFileFromPathAsync(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var options = new LauncherOptions
            {
                // Без фильтра типов: приёмник сам выберет, какие соседние
                // файлы ему интересны (Фото возьмёт только изображения)
                NeighboringFilesQuery = folder.CreateFileQueryWithOptions(new QueryOptions())
            };
            return await Launcher.LaunchFileAsync(file, options);
        }
        catch
        {
            // Папка недоступна/файл исчез/тип не поддержан Launcher'ом —
            // фолбэк на обычный ShellExecute у вызывающего
            return false;
        }
    }

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
