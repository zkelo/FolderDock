using System;

namespace FolderDock;

public static class CommandLine
{
    private const string FolderOption = "--folder";

    public static string? FolderFrom(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], FolderOption, StringComparison.OrdinalIgnoreCase))
                return CleanPath(args[i + 1]);
        return null;
    }

    public static string? FolderFrom(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;

        var index = commandLine.IndexOf(FolderOption, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        var value = commandLine[(index + FolderOption.Length)..].Trim();
        if (value.Length == 0) return null;

        return CleanPath(value.StartsWith('"') ? TakeQuoted(value) : TakeUntilSpace(value));
    }

    private static string TakeQuoted(string value)
    {
        var closing = value.IndexOf('"', 1);
        return closing > 1 ? value[1..closing] : value.Trim('"');
    }

    private static string TakeUntilSpace(string value)
    {
        var space = value.IndexOf(' ');
        return space < 0 ? value : value[..space];
    }

    private static string? CleanPath(string raw)
    {
        var path = raw.Trim().Trim('"');
        if (path.Length == 0) return null;
        if (IsBareDriveLetter(path)) path += '\\';
        return path;
    }

    private static bool IsBareDriveLetter(string path) =>
        path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':';
}
