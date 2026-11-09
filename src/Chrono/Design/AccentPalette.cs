namespace Chrono.Design;

/// <summary>
/// Акцентные цвета, которые можно выбрать в настройках: роли палитры DESIGN.json, у каждой есть значения всех четырёх видов.
/// Выбор хранится ключом роли (ThemeSettings.Accent); без выбора акцент системный (Material You) или Tint из DESIGN.json.
/// </summary>
public static class AccentPalette
{
    private const string White = "#FFFFFFFF";
    private const string Black = "#FF000000";

    /// <summary>Ключи ролей в порядке показа в настройках.</summary>
    public static IReadOnlyList<string> Keys { get; } = ["Orange", "Pink", "Purple", "Indigo", "Blue", "Green"];

    /// <summary>Цвет без выбора там, где системного акцента нет: Tint из DESIGN.json — тот же оранжевый.</summary>
    public const string Fallback = "Orange";

    /// <summary>Сохранённое значение → ключ палитры; пустое или незнакомое (цвет убрали из палитры) — null, «без выбора».</summary>
    public static string? Normalize(string? key) => key is not null && Keys.Contains(key) ? key : null;

    /// <summary>Цвет роли #AARRGGBB в заданном виде.</summary>
    public static string Hex(string key, Appearance appearance) => ChronoColors.All.First(token => token.Key == key).For(appearance);

    /// <summary>
    /// Цвет символа и текста на заливке акцентом: белый, если даёт не меньше 3:1, иначе чёрный (как OnCat1…OnCat6);
    /// в видах «контраст» — тот из двух, что контрастнее (не меньше 4.5:1).
    /// </summary>
    public static string OnHex(string argb, Appearance appearance)
    {
        var luminance = Luminance(argb);
        var white = 1.05 / (luminance + 0.05);
        var black = (luminance + 0.05) / 0.05;
        var highContrast = appearance is Appearance.LightHighContrast or Appearance.DarkHighContrast;
        return (highContrast ? white >= black : white >= 3) ? White : Black;
    }

    // Относительная яркость WCAG непрозрачного цвета #AARRGGBB.
    private static double Luminance(string argb)
    {
        static double Channel(string hex)
        {
            var value = Convert.ToInt32(hex, 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(argb[3..5]) + 0.7152 * Channel(argb[5..7]) + 0.0722 * Channel(argb[7..9]);
    }
}
