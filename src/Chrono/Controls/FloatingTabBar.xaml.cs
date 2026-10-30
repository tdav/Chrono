using Chrono.Design;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Controls;

/// <summary>
/// Нижнее меню Android (DESIGN.md §3.9): плавающая стеклянная капсула с четырьмя вкладками — «Будильники», «Распорядок», «Циферблат», «Настройки».
/// Ставится последним элементом корневого Grid каждой из четырёх страниц; <see cref="Current"/> — маршрут вкладки этой страницы.
/// Выбранную вкладку отмечает пилюля: значок и подпись — Tint, остальные — Label. На «Настройках» красный бейдж с числом замечаний «Надёжности».
/// На iOS элемент скрыт: там остаётся системный таб-бар Shell.
/// </summary>
public partial class FloatingTabBar : ContentView
{
    // Вкладка, на которой пилюля стояла последней. У каждой страницы своя капсула, и без этого пилюля новой страницы
    // просто возникала бы на своём месте: новая капсула начинает пилюлю с прежней вкладки и переезжает на свою — выглядит как одна панель.
    private static int shownIndex = -1;

    public static readonly BindableProperty CurrentProperty = BindableProperty.Create(
        nameof(Current), typeof(string), typeof(FloatingTabBar), "alarms",
        propertyChanged: (b, _, _) => ((FloatingTabBar)b).OnCurrentChanged());

    // Маршруты — из AppShell.xaml; подписи те же, что у вкладок Shell.
    private readonly (string Route, string Icon, string Title)[] tabs =
    [
        ("alarms", "icon_tab_alarms.png", AppResources.Tab_Alarms),
        ("schedule", "icon_tab_schedule.png", AppResources.Tab_Schedule),
        ("dial", "icon_tab_dial.png", AppResources.Tab_Dial),
        ("settings", "icon_gear.png", AppResources.SettingsTitle),
    ];

    private readonly Image[] icons;
    private readonly Label[] titles;
    private readonly Grid[] cells;
    private readonly Border? badge;
    private readonly Label? badgeText;
    private readonly Grid? badgeCell;
    private readonly string badgeTitle = string.Empty;
    private int index;
    private bool arrive = true;
    private Page? page;
    private Window? window;

    public FloatingTabBar()
    {
        this.InitializeComponent();
        this.icons = new Image[this.tabs.Length];
        this.titles = new Label[this.tabs.Length];
        this.cells = new Grid[this.tabs.Length];

        for (var i = 0; i < this.tabs.Length; i++)
        {
            var (route, iconSource, title) = this.tabs[i];

            // Значок 26 в держателе того же размера: бейдж «Настроек» выходит за его правый верхний угол.
            var icon = new Image { Source = iconSource, WidthRequest = 26, HeightRequest = 26 };
            var holder = new Grid { WidthRequest = 26, HeightRequest = 26, HorizontalOptions = LayoutOptions.Center, Children = { icon } };
            var label = new Label { Text = title, Style = (Style)this.Resources["TabTitle"] };
            var stack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { holder, label } };

            // Область нажатия — вся вкладка (88 × 54); прозрачный фон нужен, чтобы касание принимали и пустые места ячейки.
            var cell = new Grid { BackgroundColor = Colors.Transparent, Children = { stack } };
            cell.SetValue(SemanticProperties.DescriptionProperty, title);
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                // Нажатие на текущую вкладку ничего не делает: на корневой странице поверх неё ничего не открыто.
                if (route == this.Current)
                {
                    return;
                }

                try
                {
                    await Shell.Current.GoToAsync($"//{route}");
                }
                catch (Exception)
                {
                    // Переход прерван (идёт другая навигация, окно закрывается): остаёмся на текущей вкладке, приложение не падает.
                }
            };
            cell.GestureRecognizers.Add(tap);

            if (route == "settings")
            {
                this.badgeText = new Label { Style = (Style)this.Resources["TabBadgeText"] };
                this.badge = new Border { Style = (Style)this.Resources["TabBadge"], Content = this.badgeText };
                holder.Children.Add(this.badge);
                this.badgeCell = cell;
                this.badgeTitle = title;
            }

            this.Cells.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            this.Cells.Add(cell, i);
            this.icons[i] = icon;
            this.titles[i] = label;
            this.cells[i] = cell;
        }

        this.UpdateLook();
        this.Cells.SizeChanged += (_, _) => this.PlacePill();
        this.HandlerChanged += this.OnHandlerChanged;
    }

    /// <summary>Маршрут вкладки этой страницы: <c>alarms</c>, <c>schedule</c>, <c>dial</c> или <c>settings</c>.</summary>
    public string Current
    {
        get => (string)this.GetValue(CurrentProperty);
        set => this.SetValue(CurrentProperty, value);
    }

    private void OnCurrentChanged()
    {
        this.index = Math.Max(0, Array.FindIndex(this.tabs, tab => tab.Route == this.Current));
        this.UpdateLook();
        this.PlacePill();
    }

    // Выбранная вкладка — Tint, остальные — Label; у выбранной для чтения с экрана подсказка «Выбрано».
    private void UpdateLook()
    {
        for (var i = 0; i < this.tabs.Length; i++)
        {
            var selected = i == this.index;
            var role = selected ? "Tint" : "Label";
            this.icons[i].SetDynamicResource(IconTint.ColorProperty, role);
            this.titles[i].SetDynamicResource(Label.TextColorProperty, role);
            this.cells[i].SetValue(SemanticProperties.HintProperty, selected ? AppResources.SegmentSelected : string.Empty);
        }
    }

    // Пилюля: ширина вкладки (все равны) и сдвиг по индексу. Из SizeChanged — без анимации; при показе страницы (arrive) — пружиной
    // 0.35 с с вкладки, где пилюля стояла на прежней странице; при «Уменьшении движения» — сразу.
    private void PlacePill()
    {
        var width = this.Cells.Width / this.tabs.Length;
        if (width <= 0)
        {
            return;
        }

        this.Pill.WidthRequest = width;
        this.Pill.CancelAnimations();
        var target = this.index * width;
        var start = target;
        if (this.arrive)
        {
            this.arrive = false;
            if (shownIndex >= 0)
            {
                start = shownIndex * width;
            }

            shownIndex = this.index;
        }

        if (start != target && this.Handler?.MauiContext?.Services.GetService<ThemeService>()?.ReduceMotion != true)
        {
            this.Pill.TranslationX = start;
            _ = this.Pill.TranslateToAsync(target, 0, 350, Easing.SpringOut);
        }
        else
        {
            this.Pill.TranslationX = target;
        }
    }

    // Страница — ближайший предок типа Page; её показ и скрытие управляют пилюлей и бейджем. Подписка живёт столько же, сколько страница:
    // капсула — её собственный элемент.
    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        // На iOS элемент скрыт (IsVisible в XAML), и перечитывать там нечего.
        if (this.Handler is null || this.page is not null || !this.IsVisible)
        {
            return;
        }

        for (var parent = this.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Page found)
            {
                this.page = found;
                break;
            }
        }

        if (this.page is null)
        {
            return;
        }

        this.page.Appearing += this.OnPageAppearing;
        this.page.Disappearing += this.OnPageDisappearing;
        // Первый показ страницы Shell приходит раньше, чем у капсулы появляется обработчик (Appearing — до ToHandler),
        // и повторного Appearing не будет: подписка на Resumed и бейдж — здесь же, как при показе.
        this.OnPageAppearing(this, EventArgs.Empty);
    }

    private void OnPageAppearing(object? sender, EventArgs e)
    {
        this.arrive = true;
        this.PlacePill();

        // Возврат из фона OnAppearing не вызывает: бейдж перечитывается по Resumed.
        // Page.Window у страницы Shell в OnAppearing ещё null — берём окно приложения (как в SettingsPage).
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
            this.window.Resumed += this.OnWindowResumed;
        }

        this.RefreshBadge();
    }

    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
        }
    }

    private void OnWindowResumed(object? sender, EventArgs e) => this.RefreshBadge();

    // Число замечаний — то же, что у строки «Надёжность» (SettingsViewModel.ReliabilityIssues): пункты, не прошедшие проверку.
    // Читается при показе любой корневой страницы и при возврате в приложение: исправить можно в системных настройках.
    private async void RefreshBadge()
    {
        if (this.badge is null || this.Handler?.MauiContext?.Services.GetService<IReliabilityChecks>() is not { } checks)
        {
            return;
        }

        int issues;
        try
        {
            issues = (await checks.GetStatusAsync()).Count(status => !status.Ok);
        }
        catch (Exception)
        {
            // Бейдж — подсказка, а не функция: сбой проверки не должен ронять приложение из Appearing/Resumed.
            this.badge.IsVisible = false;
            return;
        }

        this.badgeText!.Text = issues.ToString();
        this.badge.IsVisible = issues > 0;
        // Число бейджа экранный диктор прочёл бы отдельно от вкладки: описание вкладки называет раздел и смысл числа.
        this.badgeCell!.SetValue(
            SemanticProperties.DescriptionProperty,
            issues > 0 ? $"{this.badgeTitle}, {string.Format(AppResources.ReliabilityIssues, issues)}" : this.badgeTitle);
    }
}
