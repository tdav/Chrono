using System.Windows.Input;
using Chrono.Design;

namespace Chrono.Controls;

/// <summary>
/// Стеклянная кнопка панели навигации (DESIGN.md §3): круг 44 с символом (<see cref="Source"/>) или капсула 44
/// с короткой подписью (<see cref="Text"/>). <see cref="IsProminent"/> — главная кнопка панели (✓): заливка Tint, символ OnTint.
/// Нажатие увеличивает кнопку до 1.08 пружиной; при «Уменьшении движения» вместо этого меняется прозрачность.
/// </summary>
public partial class GlassIconButton : ContentView
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(ImageSource), typeof(GlassIconButton), null);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(GlassIconButton), string.Empty,
        propertyChanged: (b, _, _) => ((GlassIconButton)b).UpdateLook());

    public static readonly BindableProperty IsProminentProperty = BindableProperty.Create(
        nameof(IsProminent), typeof(bool), typeof(GlassIconButton), false,
        propertyChanged: (b, _, _) => ((GlassIconButton)b).UpdateLook());

    public static readonly BindableProperty IsAccentProperty = BindableProperty.Create(
        nameof(IsAccent), typeof(bool), typeof(GlassIconButton), false,
        propertyChanged: (b, _, _) => ((GlassIconButton)b).UpdateLook());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(GlassIconButton), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(GlassIconButton), null);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(GlassIconButton), string.Empty);

    public GlassIconButton()
    {
        this.InitializeComponent();
        this.UpdateLook();
        this.Pointer.PointerPressed += (_, _) => this.Press(true);
        this.Pointer.PointerReleased += (_, _) => this.Press(false);
        // Отмена касания (палец ушёл с кнопки, жест перехвачен прокруткой) приходит как PointerExited.
        this.Pointer.PointerExited += (_, _) => this.Press(false);
    }

    /// <summary>Символ круглой кнопки (одноцветный значок из Resources/Images; цвет задаёт роль, а не файл).</summary>
    public ImageSource? Source
    {
        get => (ImageSource?)this.GetValue(SourceProperty);
        set => this.SetValue(SourceProperty, value);
    }

    /// <summary>Подпись: если задана, кнопка становится капсулой с текстом вместо круга с символом.</summary>
    public string Text
    {
        get => (string)this.GetValue(TextProperty);
        set => this.SetValue(TextProperty, value);
    }

    /// <summary>Главная кнопка панели: заливка Tint, символ и подпись OnTint. Одна на панель.</summary>
    public bool IsProminent
    {
        get => (bool)this.GetValue(IsProminentProperty);
        set => this.SetValue(IsProminentProperty, value);
    }

    /// <summary>Акцентная кнопка «+» в шапке (решение пользователя): заливка Tint, символ цвета текста — чёрный в светлой теме, белый в тёмной.</summary>
    public bool IsAccent
    {
        get => (bool)this.GetValue(IsAccentProperty);
        set => this.SetValue(IsAccentProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => this.GetValue(CommandParameterProperty);
        set => this.SetValue(CommandParameterProperty, value);
    }

    /// <summary>Описание для TalkBack / VoiceOver (SemanticProperties.Description); у кнопки-символа обязательно.</summary>
    public string Description
    {
        get => (string)this.GetValue(DescriptionProperty);
        set => this.SetValue(DescriptionProperty, value);
    }

    // Круг или капсула, обычная или главная: стиль, ширина и цвет символа и подписи.
    private void UpdateLook()
    {
        var hasText = !string.IsNullOrEmpty(this.Text);
        this.Glyph.IsVisible = !hasText;
        this.Caption.IsVisible = hasText;

        // Капсула с подписью растёт по тексту (но не уже круга); у круга ширину 44 даёт стиль.
        if (hasText)
        {
            this.Surface.WidthRequest = -1;
        }
        else
        {
            this.Surface.ClearValue(VisualElement.WidthRequestProperty);
        }

        var tint = this.IsProminent ? "OnTint" : "Label";
        this.Surface.SetDynamicResource(NavigableElement.StyleProperty, this.IsProminent || this.IsAccent ? "GlassButtonProminent" : "GlassButton");
        this.Glyph.SetDynamicResource(IconTint.ColorProperty, tint);
        this.Caption.SetDynamicResource(Label.TextColorProperty, tint);
    }

    // Нажатие (DESIGN.md §2.9): масштаб 1.08 пружиной за 0.2 с; при «Уменьшении движения» пружины нет — только прозрачность 0.78.
    private void Press(bool pressed)
    {
        this.Surface.CancelAnimations();
        if (this.Handler?.MauiContext?.Services.GetService<ThemeService>()?.ReduceMotion == true)
        {
            this.Surface.Scale = 1;
            this.Surface.Opacity = pressed ? 0.78 : 1;
            return;
        }

        this.Surface.Opacity = 1;
        _ = this.Surface.ScaleToAsync(pressed ? 1.08 : 1, 200, Easing.SpringOut);
    }
}
