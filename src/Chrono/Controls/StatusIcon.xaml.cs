namespace Chrono.Controls;

/// <summary>
/// Цветной значок строки списка: квадрат 30 × 30 с радиусом 7, заливка <see cref="Fill"/> (роль цвета через DynamicResource),
/// внутри символ <see cref="Source"/> цвета <see cref="GlyphColor"/>. Статусные значки — <c>icon_check.png</c> на <c>Success</c>
/// (символ <c>OnTint</c>) и <c>icon_alert.png</c> на <c>Warning</c> (символ <c>OnWarning</c>).
/// </summary>
public partial class StatusIcon : ContentView
{
    public static readonly BindableProperty FillProperty = BindableProperty.Create(
        nameof(Fill), typeof(Color), typeof(StatusIcon), null);

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(ImageSource), typeof(StatusIcon), null);

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(
        nameof(GlyphColor), typeof(Color), typeof(StatusIcon), null);

    public StatusIcon()
    {
        this.InitializeComponent();
        // Без явного цвета символ — OnColor (белый на насыщенных заливках); разметка вызова перекрывает его своим DynamicResource.
        this.SetDynamicResource(GlyphColorProperty, "OnColor");
    }

    /// <summary>Заливка квадрата: <c>Fill="{DynamicResource Success}"</c>.</summary>
    public Color? Fill
    {
        get => (Color?)this.GetValue(FillProperty);
        set => this.SetValue(FillProperty, value);
    }

    /// <summary>Символ (одноцветный значок из Resources/Images; красится в <see cref="GlyphColor"/>, цвет файла значения не имеет).</summary>
    public ImageSource? Source
    {
        get => (ImageSource?)this.GetValue(SourceProperty);
        set => this.SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Цвет символа — роль «на заливке» по фону: на Success и Tint — <c>GlyphColor="{DynamicResource OnTint}"</c>, на Warning — <c>OnWarning</c>,
    /// на остальных (Indigo, Blue …) — по умолчанию <c>OnColor</c>.
    /// </summary>
    public Color? GlyphColor
    {
        get => (Color?)this.GetValue(GlyphColorProperty);
        set => this.SetValue(GlyphColorProperty, value);
    }
}
