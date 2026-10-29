namespace Chrono.Design;

/// <summary>
/// Внешний вид: светлая или тёмная тема и повышенный контраст. Приложение не выбирает его само —
/// вид берётся из настроек телефона (DESIGN.md §2.2), своего переключателя темы нет.
/// </summary>
public enum Appearance
{
    Light,
    Dark,
    LightHighContrast,
    DarkHighContrast,
}

public static class Appearances
{
    public static Appearance From(bool dark, bool highContrast) => (dark, highContrast) switch
    {
        (false, false) => Appearance.Light,
        (true, false) => Appearance.Dark,
        (false, true) => Appearance.LightHighContrast,
        (true, true) => Appearance.DarkHighContrast,
    };
}
