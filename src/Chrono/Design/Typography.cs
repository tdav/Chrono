namespace Chrono.Design;

/// <summary>
/// Стиль текста Apple на элементе (DESIGN.md §2.4). Обычно приходит из стилей Typography.xaml (TextBody и т. п.):
/// они задают кегль, Inter нужного веса на Android, трекинг и интерлиньяж, а это свойство сообщает платформе,
/// какой это стиль. iOS строит по нему системный SF с весом и Dynamic Type (Typography.iOS.cs),
/// Android — включает табличные цифры времени и убирает лишние поля шрифта (Typography.Android.cs).
/// </summary>
public static partial class Typography
{
    public static readonly BindableProperty StyleProperty = BindableProperty.CreateAttached(
        "Style",
        typeof(TextStyle),
        typeof(Typography),
        TextStyle.None,
        propertyChanged: (bindable, _, _) => (bindable as VisualElement)?.Handler?.UpdateValue(nameof(ITextStyle.Font)));

    public static TextStyle GetStyle(BindableObject view) => (TextStyle)view.GetValue(StyleProperty);

    public static void SetStyle(BindableObject view, TextStyle value) => view.SetValue(StyleProperty, value);

    /// <summary>
    /// Стиль, который ведёт шрифт платформы: задан и не перебит локальными FontFamily или FontSize
    /// (у экранов, ещё не перенесённых на новый дизайн, — Manrope и свои кегли).
    /// </summary>
    internal static TextStyle? Leading(BindableObject view, string? family, double size)
    {
        var style = GetStyle(view);
        return style != TextStyle.None && string.IsNullOrEmpty(family) && Math.Abs(size - TextStyles.Get(style).Size) < 0.01
            ? style
            : null;
    }

    internal static void Register() => RegisterPlatform();

    static partial void RegisterPlatform();
}
