using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderDock;

/// Настройки приложения: JSON в %LOCALAPPDATA%\FolderDock\settings.json.
public sealed class Settings
{
    [JsonPropertyName("autoCheckUpdates")]
    public bool AutoCheckUpdates { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolderDock", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))
                       ?? new Settings();
        }
        catch
        {
            // Битый/недоступный файл настроек — работаем с дефолтами,
            // ближайший Save() перезапишет его валидным содержимым
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Не удалось сохранить (диск/права) — настройка доживёт до выхода,
            // при следующем запуске вернётся прежнее значение
        }
    }
}
