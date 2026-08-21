using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FolderDock;

/// Определение приложения-обработчика по расширению файла (AssocQueryString).
/// Нужно для обхода бага «Фото» Windows 11: оно игнорирует
/// NeighboringFilesQuery при обычном запуске, но строит контекст папки
/// (листание стрелками), если открыть его через URI ms-photos:viewer.
internal static class FileAssociation
{
    private const uint ASSOCF_INIT_IGNOREUNKNOWN = 0x400;
    private const int ASSOCSTR_PROGID = 20;
    private const int ASSOCSTR_APPID = 21;
    private const int S_OK = 0;

    // Классическая и AppX-регистрации Microsoft Photos
    private static readonly string[] PhotosMarkers =
    {
        "Microsoft.Windows.Photos",
        "ms-photos",
        "AppX43hnxtbyyps62jhe9sqpdzxn1790zetc"
    };

    public static bool IsMicrosoftPhotosDefault(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        if (!extension.StartsWith('.')) extension = "." + extension;

        // PROGID и APPID достаточно для современных ассоциаций и дёшево
        return MatchesPhotos(extension, ASSOCSTR_PROGID) ||
               MatchesPhotos(extension, ASSOCSTR_APPID);
    }

    private static bool MatchesPhotos(string extension, int association)
    {
        try
        {
            uint length = 0;
            // Первый вызов — узнать длину буфера
            AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN, association,
                extension, null, null, ref length);
            if (length <= 1) return false;

            var buffer = new StringBuilder((int)length);
            if (AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN, association,
                    extension, null, buffer, ref length) != S_OK)
                return false;

            var value = buffer.ToString();
            foreach (var marker in PhotosMarkers)
                if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        catch
        {
            // Реестр недоступен/битые ассоциации — считаем, что не «Фото»
            return false;
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, int str,
        string pszAssoc, string? pszExtra, StringBuilder? pszOut, ref uint pcchOut);
}
