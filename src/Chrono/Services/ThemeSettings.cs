namespace Chrono.Services;

/// <summary>Оформление приложения из настроек: как в системе (Unspecified), светлое или тёмное. Контраст — всегда из системы.</summary>
public static class ThemeSettings
{
    private const string ThemeKey = "app_theme";

    public static AppTheme Theme
    {
        get => (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.Unspecified);
        set => Preferences.Default.Set(ThemeKey, (int)value);
    }
}
