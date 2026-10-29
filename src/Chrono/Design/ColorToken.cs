namespace Chrono.Design;

/// <summary>Цвет DESIGN.json: ключ ресурса и значения #AARRGGBB для четырёх внешних видов (ChronoColors.g.cs).</summary>
public sealed record ColorToken(string Key, string Light, string Dark, string LightHighContrast, string DarkHighContrast)
{
    public string For(Appearance appearance) => appearance switch
    {
        Appearance.Dark => this.Dark,
        Appearance.LightHighContrast => this.LightHighContrast,
        Appearance.DarkHighContrast => this.DarkHighContrast,
        _ => this.Light,
    };
}
