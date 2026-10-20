namespace Chrono.Models;

/// <summary>Набор иконок дел распорядка. Ключ хранится в записи, картинка берётся из Resources/Images.</summary>
public static class ItemIcons
{
    public static readonly IReadOnlyList<string> Keys =
        ["bell", "yoga", "shower", "coffee", "food", "bike", "work", "book", "pill", "phone", "walk", "sleep"];

    /// <summary>Имя картинки для ключа; неизвестный ключ (например, из более нового файла) даёт колокольчик.</summary>
    public static string ImageFor(string key) =>
        Keys.Contains(key) ? $"icon_item_{key}.png" : "icon_item_bell.png";
}
