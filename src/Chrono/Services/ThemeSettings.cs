namespace Chrono.Services;

/// <summary>Оформление приложения из настроек: как в системе (Unspecified), светлое или тёмное. Контраст — всегда из системы.</summary>
public static class ThemeSettings
{
    private const string ThemeKey = "app_theme";
    private const string AccentKey = "app_accent";

    /// <summary>Акцентный цвет: ключ из AccentPalette или null — системный (Material You), без него Tint из DESIGN.json.</summary>
    public static string? Accent
    {
        get => Preferences.Default.Get<string?>(AccentKey, null);
        set
        {
            if (value is null)
            {
                Preferences.Default.Remove(AccentKey);
            }
            else
            {
                Preferences.Default.Set(AccentKey, value);
            }
        }
    }

    public static AppTheme Theme
    {
        get => (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.Unspecified);
        set => Preferences.Default.Set(ThemeKey, (int)value);
    }
}
