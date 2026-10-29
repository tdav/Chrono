namespace Chrono.Design;

/// <summary>Цвета нативных контролов, которые стиль XAML не достаёт (тумблер Android) — Platforms/*/Design.</summary>
internal static partial class NativeControls
{
    internal static void Register() => RegisterPlatform();

    static partial void RegisterPlatform();

    /// <summary>Цвет текущего вида из ресурсов приложения (их раскладывает ThemeService).</summary>
    internal static Color Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Transparent;
}
