using System;
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FolderDock;

/// Доступ к локализованным строкам (Strings/&lt;язык&gt;/Resources.resw).
/// Язык выбирается Windows по списку предпочитаемых языков пользователя;
/// при отсутствии перевода PRI откатывается к DefaultLanguage (en-US).
public static class Loc
{
    private static readonly ResourceLoader Loader = new();

    /// Строка по ключу; при отсутствии ресурса возвращает сам ключ,
    /// чтобы пропущенный перевод был виден в UI, а не ронял приложение.
    public static string Get(string key)
    {
        try
        {
            var value = Loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    /// Форматная строка по ключу: Loc.Format("Items_Count", 5).
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
