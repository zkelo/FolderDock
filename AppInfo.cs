using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace FolderDock;

/// Метаданные приложения для отображения в UI.
/// Версия и момент сборки впечатываются в сборку на этапе компиляции
/// (см. FolderDock.csproj: Version + AssemblyMetadata BuildTimestampUtc).
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/zkelo/FolderDock";

    /// «1.2.3» — без хвоста «+commit» из InformationalVersion.
    public static string Version { get; } = ReadVersion();

    /// Момент сборки (UTC) или null, если метаданные отсутствуют
    /// (например, локальная сборка без таргета в csproj).
    public static DateTime? BuiltAtUtc { get; } = ReadBuildTimestamp();

    /// «11.08.2026 21:15 UTC» для футера; пустая строка без метаданных.
    public static string BuildStampText =>
        BuiltAtUtc is { } utc
            ? utc.ToString("dd.MM.yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string ReadVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            // SourceLink дописывает «+<sha>» — пользователю он не нужен
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static DateTime? ReadBuildTimestamp()
    {
        var raw = typeof(AppInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestampUtc")?
            .Value;
        return DateTime.TryParseExact(raw, "o", CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? utc
            : null;
    }
}
