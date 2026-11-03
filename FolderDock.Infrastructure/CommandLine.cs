using System;
using System.Collections.Generic;

namespace FolderDock.Infrastructure;

public static class CommandLine
{
    private const string FolderOption = "--folder";
    private const string BackgroundOption = "--background";

    public static LaunchRequest Parse(string[] args)
    {
        // A real folder activation always wins, including a folder literally
        // named "--background". Login launches must never toggle a popup.
        var folder = FolderFrom(args);
        if (folder is not null) return LaunchRequest.OpenFolder(folder);
        var background = Array.Exists(args, arg =>
            string.Equals(arg, BackgroundOption, StringComparison.OrdinalIgnoreCase));
        return background ? LaunchRequest.Background : LaunchRequest.Manager;
    }

    public static LaunchRequest Parse(string commandLine) => Parse(Tokenize(commandLine));

    public static bool IsBackground(string[] args) => Parse(args).Kind == LaunchKind.Background;

    public static bool IsBackground(string commandLine) => Parse(commandLine).Kind == LaunchKind.Background;

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
