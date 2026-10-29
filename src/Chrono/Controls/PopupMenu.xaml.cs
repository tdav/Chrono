using System.Collections;
using System.Windows.Input;
using Chrono.Design;
using Chrono.ViewModels;

namespace Chrono.Controls;

/// <summary>
/// Меню выбора одного значения (DESIGN.md §3 › Меню): короткий список (до ~7 пунктов) у строки-якоря. Пункты — <see cref="Items"/>
/// (<see cref="ChoiceOption"/> с признаком <see cref="ChoiceOption.IsSelected"/>), выбор выполняет <see cref="SelectCommand"/> с пунктом
/// в параметре и закрывает меню. Открывается от <see cref="Anchor"/> пружиной 0.5 с (при «Уменьшении движения» — растворением);
/// нажатие вне меню и <see cref="HandleBack"/> закрывают его. Логику выбора (например, проигрывание мелодии) ведёт модель, а не меню.
/// Кладётся поверх страницы в её корневой <c>Grid</c> — как <see cref="BottomSheet"/>.
/// </summary>
public partial class PopupMenu : ContentView
{
    // Пружина без перелёта cubic-bezier(0.32, 0.72, 0, 1) (DESIGN.md §2.9); ближайшая встроенная форма — быстрый старт и долгое затухание.
    private static readonly Easing Spring = new(t => 1 - Math.Pow(1 - t, 4));

    // Поля меню до края страницы и зазор до якоря.
    private const double EdgeGap = 12;
    private const double AnchorGap = 6;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), typeof(bool), typeof(PopupMenu), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => _ = ((PopupMenu)b).ShowAsync((bool)n));

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IEnumerable), typeof(PopupMenu), null);

    public static readonly BindableProperty SelectCommandProperty = BindableProperty.Create(
        nameof(SelectCommand), typeof(ICommand), typeof(PopupMenu), null);

    public static readonly BindableProperty AnchorProperty = BindableProperty.Create(
        nameof(Anchor), typeof(View), typeof(PopupMenu), null);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(PopupMenu), string.Empty);

    public PopupMenu()
    {
        this.InitializeComponent();
    }

    /// <summary>Меню открыто. Двусторонняя: нажатие вне меню и выбор пункта закрывают его сами.</summary>
    public bool IsOpen
    {
        get => (bool)this.GetValue(IsOpenProperty);
        set => this.SetValue(IsOpenProperty, value);
    }

    /// <summary>Пункты меню: <see cref="ChoiceOption"/> с названием и признаком выбора (галочка в ведущей колонке).</summary>
    public IEnumerable? Items
    {
        get => (IEnumerable?)this.GetValue(ItemsProperty);
        set => this.SetValue(ItemsProperty, value);
    }

    /// <summary>Выбор пункта; параметр — <see cref="ChoiceOption"/> нажатого пункта.</summary>
    public ICommand? SelectCommand
    {
        get => (ICommand?)this.GetValue(SelectCommandProperty);
        set => this.SetValue(SelectCommandProperty, value);
    }

    /// <summary>Строка или кнопка, от которой открывается меню; должна лежать в том же корневом контейнере, что и меню.</summary>
    public View? Anchor
    {
        get => (View?)this.GetValue(AnchorProperty);
        set => this.SetValue(AnchorProperty, value);
    }

    /// <summary>Название меню для TalkBack / VoiceOver («Мелодия»).</summary>
    public string Description
    {
        get => (string)this.GetValue(DescriptionProperty);
        set => this.SetValue(DescriptionProperty, value);
    }

    // «Уменьшение движения»: меню растворяется вместо пружины (DESIGN.md §2.9).
    private static bool ReduceMotion =>
        IPlatformApplication.Current?.Services.GetService<ThemeService>()?.ReduceMotion ?? false;

    /// <summary>Системная «Назад»: закрывает открытое меню. false — меню закрыто, «Назад» обрабатывает страница.</summary>
    public bool HandleBack()
    {
        if (!this.IsOpen)
        {
            return false;
        }

        this.IsOpen = false;
        return true;
    }

    private void OnDismissTapped(object? sender, TappedEventArgs e) => this.IsOpen = false;

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ChoiceOption option)
        {
            this.SelectCommand?.Execute(option);
            this.IsOpen = false;
        }
    }

    private void OnItemPressed(object? sender, PointerEventArgs e) =>
        (sender as VisualElement)?.SetDynamicResource(VisualElement.BackgroundColorProperty, "QuaternarySystemFill");

    // Отпускание и уход пальца с пункта (отмена касания) снимают подсветку.
    private void OnItemReleased(object? sender, PointerEventArgs e)
    {
        if (sender is VisualElement item)
        {
            item.RemoveDynamicResource(VisualElement.BackgroundColorProperty);
            item.ClearValue(VisualElement.BackgroundColorProperty);
        }
    }

    // Открытие: меню встаёт у якоря и растёт из его угла (масштаб 0.86 → 1, прозрачность 0 → 1); закрытие — быстрое растворение.
    private async Task ShowAsync(bool open)
    {
        this.Menu.CancelAnimations();
        var reduceMotion = ReduceMotion;

        if (open)
        {
            this.IsVisible = true;

            var size = ((IView)this.Menu).Measure(this.Menu.WidthRequest, double.PositiveInfinity);
            var container = this.Parent;
            var area = (container as VisualElement) is { Width: > 0, Height: > 0 } host ? new Size(host.Width, host.Height) : new Size(this.Width, this.Height);

            // Положение якоря в координатах контейнера: сумма смещений предков до контейнера с учётом прокрутки ScrollView.
            // Смещения TranslationX/Y предков (например, страница позади большой шторки) не учитываются. Якорь вне контейнера — без якоря.
            Rect? anchor = null;
            if (this.Anchor is { } view && container is not null)
            {
                double x = 0;
                double y = 0;
                Element? element = view;
                while (element is VisualElement visual && element != container)
                {
                    x += visual.X;
                    y += visual.Y;
                    if (visual.Parent is ScrollView scroll)
                    {
                        x -= scroll.ScrollX;
                        y -= scroll.ScrollY;
                    }

                    element = visual.Parent;
                }

                anchor = element == container ? new Rect(x, y, view.Width, view.Height) : null;
            }

            // Без якоря (или вне контейнера) меню встаёт по центру. С якорем — по его правому краю, не ближе EdgeGap к краю страницы:
            // под якорем, если помещается, иначе над ним.
            double left;
            double top;
            if (anchor is { } rect)
            {
                var below = rect.Bottom + AnchorGap + size.Height + EdgeGap <= area.Height;
                left = Math.Clamp(rect.Right - size.Width, EdgeGap, Math.Max(EdgeGap, area.Width - size.Width - EdgeGap));
                top = Math.Clamp(below ? rect.Bottom + AnchorGap : rect.Top - AnchorGap - size.Height, EdgeGap, Math.Max(EdgeGap, area.Height - size.Height - EdgeGap));
                this.Menu.AnchorX = Math.Clamp((rect.Right - left) / size.Width, 0, 1);
                this.Menu.AnchorY = below ? 0 : 1;
            }
            else
            {
                left = Math.Max(EdgeGap, (area.Width - size.Width) / 2);
                top = Math.Max(EdgeGap, (area.Height - size.Height) / 2);
                this.Menu.AnchorX = 0.5;
                this.Menu.AnchorY = 0.5;
            }

            this.Menu.Margin = new Thickness(left, top, 0, 0);

            // Тень shadowPopover: 18 % в светлой теме, 60 % в тёмной.
            this.Menu.Shadow.Opacity = Application.Current?.RequestedTheme == AppTheme.Dark ? 0.6f : 0.18f;
            this.Menu.Opacity = 0;
            this.Menu.Scale = reduceMotion ? 1 : 0.86;
            if (reduceMotion)
            {
                await this.Menu.FadeToAsync(1, 200);
            }
            else
            {
                await Task.WhenAll(this.Menu.FadeToAsync(1, 200), this.Menu.ScaleToAsync(1, 500, Spring));
            }

            return;
        }

        if (!this.IsVisible)
        {
            return;
        }

        await this.Menu.FadeToAsync(0, reduceMotion ? 200u : 150u);

        // Меню открыли снова, пока оно гасло: оставляем открытым.
        if (this.IsOpen)
        {
            return;
        }

        this.IsVisible = false;
        this.Menu.Opacity = 1;
        this.Menu.Scale = 1;
    }
}
