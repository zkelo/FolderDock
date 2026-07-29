using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FolderDock;

public static class FolderAumid
{
    public static string For(string folderPath)
    {
        var normalized = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ToLowerInvariant();
        var hash = Convert.ToHexString(
            SHA1.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        return $"FolderDock.Pin.{hash}";
    }

    public static string ShortSuffix(string folderPath) => For(folderPath)[^6..];
}
