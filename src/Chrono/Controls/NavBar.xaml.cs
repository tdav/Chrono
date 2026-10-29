using System.Windows.Input;

namespace Chrono.Controls;

/// <summary>
/// Панель навигации вложенного экрана: inline-заголовок по центру, слева «назад» или произвольный элемент, справа элемент.
/// «Назад» (стеклянная кнопка с шевроном, описание «Назад») показывается, когда задана <see cref="BackCommand"/>
/// и нет <see cref="LeadingContent"/>; навигацию кнопка не выполняет — команду даёт страница.
/// </summary>
public partial class NavBar : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(NavBar), string.Empty);

    /// <summary>Прозрачность одного заголовка (кнопки не меняются): LargeTitleHeader проявляет его при прокрутке.</summary>
    public static readonly BindableProperty TitleOpacityProperty = BindableProperty.Create(
        nameof(TitleOpacity), typeof(double), typeof(NavBar), 1.0);

    public static readonly BindableProperty LeadingContentProperty = BindableProperty.Create(
        nameof(LeadingContent), typeof(View), typeof(NavBar), null,
        propertyChanged: (b, _, _) => ((NavBar)b).UpdateLeading());

    public static readonly BindableProperty TrailingContentProperty = BindableProperty.Create(
        nameof(TrailingContent), typeof(View), typeof(NavBar), null);

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand), typeof(ICommand), typeof(NavBar), null,
        propertyChanged: (b, _, _) => ((NavBar)b).UpdateLeading());

    public NavBar()
    {
        this.InitializeComponent();
        // Ширина сторон известна только после раскладки (и меняется со шрифтом, подписью капсулы): поля заголовка пересчитываются.
        this.BackButton.SizeChanged += (_, _) => this.CenterTitle();
        this.LeadingHost.SizeChanged += (_, _) => this.CenterTitle();
        this.TrailingHost.SizeChanged += (_, _) => this.CenterTitle();
    }

    public string Title
    {
        get => (string)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    public double TitleOpacity
    {
        get => (double)this.GetValue(TitleOpacityProperty);
        set => this.SetValue(TitleOpacityProperty, value);
    }

    /// <summary>Элемент слева вместо «назад» (например, ✕ «Отменить»).</summary>
    public View? LeadingContent
    {
        get => (View?)this.GetValue(LeadingContentProperty);
        set => this.SetValue(LeadingContentProperty, value);
    }

    /// <summary>Элемент справа (например, ✓ «Готово»).</summary>
    public View? TrailingContent
    {
        get => (View?)this.GetValue(TrailingContentProperty);
        set => this.SetValue(TrailingContentProperty, value);
    }

    /// <summary>Команда кнопки «назад»; без неё и без <see cref="LeadingContent"/> слева пусто.</summary>
    public ICommand? BackCommand
    {
        get => (ICommand?)this.GetValue(BackCommandProperty);
        set => this.SetValue(BackCommandProperty, value);
    }

    private void UpdateLeading()
    {
        this.BackButton.IsVisible = this.BackCommand is not null && this.LeadingContent is null;
        this.CenterTitle();
    }

    // Поля заголовка = большая из сторон + 8: заголовок по центру строки, а длинный не налезает на кнопки.
    private void CenterTitle()
    {
        var leading = (this.BackButton.IsVisible ? Math.Max(this.BackButton.Width, 0) : 0) + Math.Max(this.LeadingHost.Width, 0);
        var side = Math.Max(leading, Math.Max(this.TrailingHost.Width, 0)) + 8;
        this.TitleLabel.Margin = new Thickness(side, 0);
    }
}
