namespace Chrono.Models;

/// <summary>Набор иконок дел распорядка. Ключ хранится в записи, картинка берётся из Resources/Images.</summary>
public static class ItemIcons
{
    public static readonly IReadOnlyList<string> Keys =
        [
            "bell", "yoga", "shower", "coffee", "food", "bike", "work", "book", "pill", "phone", "walk", "sleep",
            "run", "dumbbell", "swim", "ball", "heart", "tooth", "water", "egg", "tea", "cake", "apple", "cart", "home",
            "broom", "laundry", "plant", "pet", "child", "laptop", "meeting", "mail", "calendar", "pen", "study", "idea",
            "car", "bus", "plane", "music", "movie", "game", "camera", "gift", "sun", "star", "money", "target", "flower"
        ];

    /// <summary>Имя картинки для ключа; неизвестный ключ (например, из более нового файла) даёт колокольчик.</summary>
    public static string ImageFor(string key) =>
        Keys.Contains(key) ? $"icon_item_{key}.png" : "icon_item_bell.png";
}
