using Chrono.Design;
using Chrono.Resources.Strings;
using Chrono.Services;
using Chrono.ViewModels;

namespace Chrono.Controls;

/// <summary>
/// Календарь в форме (DESIGN.md §3.3): строка <see cref="Title"/> с чипом значения и раскрывающийся под ней календарь месяца.
/// Нажатие на строку раскрывает календарь (<see cref="IsExpanded"/>), повторное — сворачивает. Данные и команды — в <see cref="Model"/>;
/// выбор применяется сразу, форма следит за <see cref="CalendarSheetModel.Selected"/>.
/// Раскрытие и сворачивание — пружина 0.5 с; при «Уменьшении движения» — растворение.
/// </summary>
public partial class InlineCalendar : ContentView
{
    private const string ExpandAnimation = "InlineCalendarExpand";

    // Пружина без перелёта cubic-bezier(0.32, 0.72, 0, 1) (DESIGN.md §2.9); ближайшая встроенная форма — быстрый старт и долгое затухание.
    private static readonly Easing Spring = new(t => 1 - Math.Pow(1 - t, 4));

    public static readonly BindableProperty ModelProperty = BindableProperty.Create(
        nameof(Model), typeof(CalendarSheetModel), typeof(InlineCalendar), null);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(InlineCalendar), string.Empty,
        propertyChanged: (b, _, _) => ((InlineCalendar)b).UpdateRowDescription());

    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(InlineCalendar), false, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((InlineCalendar)b).UpdateExpanded(((InlineCalendar)b).IsLoaded));

    private readonly ISoundPlayer? sound = IPlatformApplication.Current?.Services.GetService<ISoundPlayer>();

    public InlineCalendar()
    {
        this.InitializeComponent();
        this.UpdateExpanded(false);

        // Значение чипа приходит из модели привязкой; описание строки читает его с самой подписи, поэтому следить за моделью не нужно.
        this.ChipLabel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == Label.TextProperty.PropertyName)
            {
                this.UpdateRowDescription();
            }
        };
    }

    /// <summary>Модель календаря: выбранная дата, месяц, команды. Её же форма читает как результат.</summary>
    public CalendarSheetModel? Model
    {
        get => (CalendarSheetModel?)this.GetValue(ModelProperty);
        set => this.SetValue(ModelProperty, value);
    }

    /// <summary>Название строки слева от чипа: «Дата», «Окончание».</summary>
    public string Title
    {
        get => (string)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>Календарь раскрыт: чип подсвечен TintFill. Двусторонняя — форма может свернуть календарь сама (например, раскрыт один из двух).</summary>
    public bool IsExpanded
    {
        get => (bool)this.GetValue(IsExpandedProperty);
        set => this.SetValue(IsExpandedProperty, value);
    }

    // «Уменьшение движения»: календарь растворяется вместо раскрытия пружиной (DESIGN.md §2.9).
    private static bool ReduceMotion =>
        IPlatformApplication.Current?.Services.GetService<ThemeService>()?.ReduceMotion ?? false;

    private void OnRowTapped(object? sender, TappedEventArgs e) => this.IsExpanded = !this.IsExpanded;

    // Листание месяца и «Сегодня» всегда меняют показанное — щелчок на каждое нажатие.
    private void OnTick(object? sender, EventArgs e) => this.sound?.Tick();

    // Прошедший или пустой день не выбирается — и не щёлкает (то же условие, что в PickCommand).
    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is CalendarDayItem { IsDisabled: false, Cell.Date: not null } day)
        {
            this.Model?.PickCommand.Execute(day);
            this.sound?.Tick();
        }
    }

    // Описание строки для TalkBack / VoiceOver: название, значение чипа и состояние — «Дата, сб, 24 окт., свёрнуто».
    // Меняется с названием, значением чипа и раскрытием, поэтому собирается здесь, а не привязкой.
    private void UpdateRowDescription() =>
        SemanticProperties.SetDescription(
            this.Line,
            string.Format(this.IsExpanded ? AppResources.CalendarRowExpanded : AppResources.CalendarRowCollapsed, this.Title, this.ChipLabel.Text));

    // Вид чипа и показ календаря. Вызывается из конструктора и при смене IsExpanded; без animate — сразу конечное состояние
    // (и по окончании анимации). Анимация прерывается новой: сворачивание на середине раскрытия начинает с текущей высоты.
    private void UpdateExpanded(bool animate)
    {
        this.AbortAnimation(ExpandAnimation);
        var expanded = this.IsExpanded;
        this.UpdateRowDescription();
        this.Chip.SetDynamicResource(VisualElement.BackgroundColorProperty, expanded ? "TintFill" : "TertiarySystemFill");

        if (!animate)
        {
            this.Panel.IsVisible = expanded;
            this.Panel.HeightRequest = -1;
            this.Panel.Opacity = 1;
            return;
        }

        var start = this.Panel.IsVisible ? Math.Max(this.Panel.Height, 0) : 0;
        // Растворение («Уменьшение движения») начинается с текущей прозрачности: повторное нажатие на середине не прыгает к 0 или 1.
        // Скрытая панель стартует с нуля.
        var startOpacity = this.Panel.IsVisible ? this.Panel.Opacity : 0;
        this.Panel.HeightRequest = -1;
        this.Panel.IsVisible = true;
        var full = ((IView)this.Panel).Measure(this.Width, double.PositiveInfinity).Height;

        var reduceMotion = ReduceMotion;
        Animation animation;
        if (reduceMotion)
        {
            this.Panel.Opacity = startOpacity;
            animation = new Animation(v => this.Panel.Opacity = v, startOpacity, expanded ? 1 : 0);
        }
        else
        {
            this.Panel.Opacity = 1;
            this.Panel.HeightRequest = start;
            animation = new Animation(v => this.Panel.HeightRequest = v, start, expanded ? full : 0);
        }

        animation.Commit(this, ExpandAnimation, 16, 500, reduceMotion ? Easing.Linear : Spring, (_, canceled) =>
        {
            if (!canceled)
            {
                this.UpdateExpanded(false);
            }
        });
    }
}
