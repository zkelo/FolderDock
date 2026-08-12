using System;
using System.Collections.Generic;

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

        // Токенизируем, а не ищем подстроку по всей строке: "--folder"
        // внутри пути к exe не должен давать ложное срабатывание
        return FolderFrom(Tokenize(commandLine));
    }

    private static string[] Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var index = 0;
        while (index < commandLine.Length)
        {
            while (index < commandLine.Length && commandLine[index] == ' ') index++;
            if (index >= commandLine.Length) break;

            if (commandLine[index] == '"')
            {
                var closing = commandLine.IndexOf('"', index + 1);
                if (closing < 0) closing = commandLine.Length;
                tokens.Add(commandLine[(index + 1)..closing]);
                index = closing + 1;
            }
            else
            {
                var space = commandLine.IndexOf(' ', index);
                if (space < 0) space = commandLine.Length;
                tokens.Add(commandLine[index..space]);
                index = space;
            }
        }
        return tokens.ToArray();
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
