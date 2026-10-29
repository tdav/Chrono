namespace Chrono.Design;

/// <summary>
/// Цвет значка по роли (DESIGN.md §2.8): <c>design:IconTint.Color="{DynamicResource Label}"</c> на Image
/// перекрашивает одноцветный SVG/PNG из Resources/Images; цвет, записанный в файле, значения не имеет.
/// </summary>
public static partial class IconTint
{
    internal const string MapperKey = "ChronoIconTint";

    public static readonly BindableProperty ColorProperty = BindableProperty.CreateAttached(
        "Color",
        typeof(Color),
        typeof(IconTint),
        null,
        propertyChanged: (bindable, _, _) => (bindable as Image)?.Handler?.UpdateValue(MapperKey));

    public static Color? GetColor(BindableObject view) => (Color?)view.GetValue(ColorProperty);

    public static void SetColor(BindableObject view, Color? value) => view.SetValue(ColorProperty, value);

    internal static void Register() => RegisterPlatform();

    static partial void RegisterPlatform();
}
