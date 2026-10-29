using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chrono.Design;

namespace Chrono.Tests;

/// <summary>
/// DESIGN.json — источник правды дизайна Apple. Код цветов и стилей текста генерирует tools/gen_design.py;
/// тесты ловят расхождение (правку руками или забытый запуск генератора), читаемость текста в четырёх видах
/// и ключи ресурсов, на которые ссылаются стили XAML.
/// </summary>
public sealed class DesignTokensTests
{
    private static readonly Appearance[] AllAppearances =
        [Appearance.Light, Appearance.Dark, Appearance.LightHighContrast, Appearance.DarkHighContrast];

    private static readonly string[] Backgrounds =
    [
        "SystemBackground", "SecondarySystemBackground", "TertiarySystemBackground",
        "SystemGroupedBackground", "SecondarySystemGroupedBackground", "TertiarySystemGroupedBackground",
        "ElevatedGroupedBackground", "ElevatedSecondaryGroupedBackground",
    ];

    private static string RepoPath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Chrono.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Корень репозитория (Chrono.slnx) не найден.");
        }

        return Path.Combine([dir.FullName, .. parts]);
    }

    private static JsonElement Design() => JsonDocument.Parse(File.ReadAllText(RepoPath("DESIGN.json"))).RootElement;

    private static string JsonKey(Appearance appearance) => appearance switch
    {
        Appearance.Dark => "dark",
        Appearance.LightHighContrast => "lightHighContrast",
        Appearance.DarkHighContrast => "darkHighContrast",
        _ => "light",
    };

    private static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>Цвет DESIGN.json в #AARRGGBB — те же правила, что у генератора: ссылки {имя}, суффикс @альфа, rgba().</summary>
    private static string Resolve(JsonElement colors, string name, Appearance appearance)
    {
        var value = colors.GetProperty(name);
        var text = value.ValueKind == JsonValueKind.Object ? value.GetProperty(JsonKey(appearance)).GetString()! : value.GetString()!;
        var reference = Regex.Match(text, @"^\{(\w+)\}(?:@([0-9.]+))?$");
        if (reference.Success)
        {
            var (r, g, b, a) = Parse(Resolve(colors, reference.Groups[1].Value, appearance));
            var factor = reference.Groups[2].Success ? double.Parse(reference.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            return Hex(r, g, b, a * factor);
        }

        if (text.StartsWith('#'))
        {
            return Hex(Convert.ToInt32(text[1..3], 16), Convert.ToInt32(text[3..5], 16), Convert.ToInt32(text[5..7], 16), 1);
        }

        var rgba = Regex.Match(text.Replace(" ", ""), @"^rgba\((\d+),(\d+),(\d+),([0-9.]+)\)$");
        Assert.True(rgba.Success, $"Цвет не разобран: {name} = {text}");
        return Hex(int.Parse(rgba.Groups[1].Value), int.Parse(rgba.Groups[2].Value), int.Parse(rgba.Groups[3].Value),
            double.Parse(rgba.Groups[4].Value, CultureInfo.InvariantCulture));
    }

    private static string Hex(int r, int g, int b, double alpha) => $"#{(int)(alpha * 255 + 0.5):X2}{r:X2}{g:X2}{b:X2}";

    private static (int R, int G, int B, double A) Parse(string argb) =>
        (Convert.ToInt32(argb[3..5], 16), Convert.ToInt32(argb[5..7], 16), Convert.ToInt32(argb[7..9], 16), Convert.ToInt32(argb[1..3], 16) / 255.0);

    private static string Token(string key, Appearance appearance) => ChronoColors.All.Single(t => t.Key == key).For(appearance);

    /// <summary>Контраст WCAG: полупрозрачный текст сначала накладывается на фон.</summary>
    private static double Contrast(string foreground, string background)
    {
        var (br, bg, bb, _) = Parse(background);
        var (fr, fg, fb, fa) = Parse(foreground);
        static double Channel(double v)
        {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        static double Luminance(double r, double g, double b) => 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
        var text = Luminance(fr * fa + br * (1 - fa), fg * fa + bg * (1 - fa), fb * fa + bb * (1 - fa));
        var back = Luminance(br, bg, bb);
        return (Math.Max(text, back) + 0.05) / (Math.Min(text, back) + 0.05);
    }

    private static double WorstContrast(string key, Appearance appearance) =>
        Backgrounds.Min(background => Contrast(Token(key, appearance), Token(background, appearance)));

    [Fact]
    public void Colors_MatchDesignJson()
    {
        var colors = Design().GetProperty("color");
        var names = colors.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(names.Select(Pascal), ChronoColors.All.Select(t => t.Key));
        foreach (var name in names)
        {
            foreach (var appearance in AllAppearances)
            {
                Assert.True(
                    Resolve(colors, name, appearance) == Token(Pascal(name), appearance),
                    $"{name} ({appearance}): ChronoColors.g.cs не совпадает с DESIGN.json — запустите python tools/gen_design.py");
            }
        }
    }

    [Fact]
    public void TextStyles_MatchDesignJson()
    {
        foreach (var style in Design().GetProperty("type").EnumerateObject())
        {
            var metrics = TextStyles.Get(Enum.Parse<TextStyle>(Pascal(style.Name)));
            var json = style.Value;
            Assert.Equal(json.GetProperty("size").GetDouble(), metrics.Size);
            Assert.Equal(json.GetProperty("lineHeight").GetDouble(), metrics.LineHeight);
            Assert.Equal(json.GetProperty("weight").GetInt32(), (int)metrics.Weight);
            Assert.Equal(json.GetProperty("tracking").GetDouble(), metrics.Tracking, 6);
            Assert.Equal(Pascal(json.GetProperty("dynamicType").GetString()!), metrics.Scale.ToString());
            Assert.Equal(json.TryGetProperty("maxSize", out var max) ? max.GetDouble() : null, metrics.MaxSize);
            Assert.Equal(json.TryGetProperty("tabular", out var tabular) && tabular.GetBoolean(), metrics.Tabular);
        }
    }

    [Theory]
    [InlineData(Appearance.Light)]
    [InlineData(Appearance.Dark)]
    [InlineData(Appearance.LightHighContrast)]
    [InlineData(Appearance.DarkHighContrast)]
    public void Label_IsReadableOnEveryBackground(Appearance appearance) =>
        Assert.True(WorstContrast("Label", appearance) >= 7, $"Label ({appearance}): {WorstContrast("Label", appearance):F2}:1");

    /// <summary>
    /// В видах «контраст» вторичный текст поднят до 7:1, третичный и плейсхолдер — до 4.5:1 (DESIGN.md §2.2).
    /// В обычной светлой теме у SecondaryLabel пара самой Apple (около 3.3:1) — она сохранена и здесь не проверяется.
    /// </summary>
    [Theory]
    [InlineData(Appearance.LightHighContrast)]
    [InlineData(Appearance.DarkHighContrast)]
    public void HighContrast_RaisesSecondaryText(Appearance appearance)
    {
        Assert.True(WorstContrast("SecondaryLabel", appearance) >= 7, $"SecondaryLabel: {WorstContrast("SecondaryLabel", appearance):F2}:1");
        Assert.True(WorstContrast("TertiaryLabel", appearance) >= 4.5, $"TertiaryLabel: {WorstContrast("TertiaryLabel", appearance):F2}:1");
        Assert.True(WorstContrast("PlaceholderText", appearance) >= 4.5, $"PlaceholderText: {WorstContrast("PlaceholderText", appearance):F2}:1");
        Assert.True(Contrast(Token("OnTint", appearance), Token("Tint", appearance)) >= 4.5, "Текст на заливке Tint");
    }

    /// <summary>
    /// DynamicResource на несуществующий ключ MAUI молча пропускает, StaticResource падает только при открытии экрана —
    /// поэтому ключи стилей проверяются здесь.
    /// </summary>
    [Fact]
    public void Styles_ReferenceExistingKeys()
    {
        string[] files = ["Colors.xaml", "Metrics.xaml", "Typography.xaml", "Styles.xaml"];
        var xaml = files.ToDictionary(f => f, f => File.ReadAllText(RepoPath("src", "Chrono", "Resources", "Styles", f)));
        var defined = xaml.Values.SelectMany(text => Regex.Matches(text, "x:Key=\"(\\w+)\"").Select(m => m.Groups[1].Value)).ToHashSet();
        var colorKeys = ChronoColors.All.Select(t => t.Key).ToHashSet();

        foreach (var (file, text) in xaml)
        {
            foreach (Match m in Regex.Matches(text, @"\{DynamicResource (\w+)\}"))
            {
                Assert.True(colorKeys.Contains(m.Groups[1].Value) || m.Groups[1].Value == "HairlineWidth", $"{file}: DynamicResource {m.Groups[1].Value}");
            }

            foreach (Match m in Regex.Matches(text, @"\{StaticResource (\w+)\}"))
            {
                Assert.True(defined.Contains(m.Groups[1].Value), $"{file}: StaticResource {m.Groups[1].Value}");
            }
        }
    }

    [Theory]
    [InlineData(false, false, Appearance.Light)]
    [InlineData(true, false, Appearance.Dark)]
    [InlineData(false, true, Appearance.LightHighContrast)]
    [InlineData(true, true, Appearance.DarkHighContrast)]
    public void Appearance_FollowsSystem(bool dark, bool highContrast, Appearance expected) =>
        Assert.Equal(expected, Appearances.From(dark, highContrast));
}
