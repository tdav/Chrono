namespace Chrono.Controls;

/// <summary>
/// Цветной значок строки списка: квадрат 30 × 30 с радиусом 7, заливка <see cref="Fill"/> (роль цвета через DynamicResource),
/// внутри белый символ <see cref="Source"/>. Статусные значки — <c>icon_check.png</c> на <c>Success</c> и <c>icon_alert.png</c> на <c>Warning</c>.
/// </summary>
public partial class StatusIcon : ContentView
{
    public static readonly BindableProperty FillProperty = BindableProperty.Create(
        nameof(Fill), typeof(Color), typeof(StatusIcon), null);

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(ImageSource), typeof(StatusIcon), null);

    public StatusIcon()
    {
        this.InitializeComponent();
    }

    /// <summary>Заливка квадрата: <c>Fill="{DynamicResource Success}"</c>.</summary>
    public Color? Fill
    {
        get => (Color?)this.GetValue(FillProperty);
        set => this.SetValue(FillProperty, value);
    }

    /// <summary>Символ (одноцветный значок из Resources/Images; красится в OnTint, цвет файла значения не имеет).</summary>
    public ImageSource? Source
    {
        get => (ImageSource?)this.GetValue(SourceProperty);
        set => this.SetValue(SourceProperty, value);
    }
}
