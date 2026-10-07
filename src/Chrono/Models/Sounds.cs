namespace Chrono.Models;

/// <summary>
/// Каталог встроенных мелодий. Id совпадает с именем файла Resources/Raw/{id}.wav
/// и с суффиксом ключа строки Sound_{id} в AppResources.
/// </summary>
public static class Sounds
{
    public const string Default = "bell";

    public static readonly IReadOnlyList<string> All = ["bell", "chime", "digital", "rising", "soft"];
}
