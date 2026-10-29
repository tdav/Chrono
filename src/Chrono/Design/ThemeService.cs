namespace Chrono.Design;

/// <summary>
/// Внешний вид приложения = внешний вид системы (DESIGN.md §2.2): светлая или тёмная тема и повышенный контраст.
/// Сервис раскладывает цвета текущего вида в ресурсы приложения — в XAML они берутся через DynamicResource —
/// и перекладывает их при смене темы и контраста, а также при возврате в приложение (Refresh из Window.Resumed):
/// не все платформы сообщают об изменениях сами. Светлую или тёмную тему можно задать в настройках (ThemeSettings →
/// UserAppTheme): RequestedTheme учитывает этот выбор.
/// </summary>
public sealed class ThemeService
{
    private Application? app;
    private IDisposable? accessibility;
    private bool applied;
    private Color? accent;

    /// <summary>Текущий вид.</summary>
    public Appearance Current { get; private set; }

    /// <summary>«Уменьшение движения» / «Отключить анимацию»: шторки растворяются, пружин и волн сигнала нет.</summary>
    public bool ReduceMotion { get; private set; }

    /// <summary>«Понижение прозрачности» (iOS): стекло заменяется непрозрачной заливкой GlassOpaque.</summary>
    public bool ReduceTransparency { get; private set; }

    /// <summary>Вид или настройки доступности сменились: свои рисунки (GraphicsView циферблата) перерисовываются по нему.</summary>
    public event EventHandler? Changed;

    public void Attach(Application application)
    {
        this.app = application;
        // Разделитель строк — один физический пиксель (DESIGN.md §3 › Список).
        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        application.Resources["HairlineWidth"] = density > 0 ? 1 / density : 0.5;
        application.RequestedThemeChanged += (_, _) => this.Refresh();
        // Подписка держит слушателя платформы живым, пока жив сервис (он singleton).
        this.accessibility?.Dispose();
        this.accessibility = SystemAccessibility.Observe(() => MainThread.BeginInvokeOnMainThread(this.Refresh));
        this.Refresh();
    }

    public void Refresh()
    {
        if (this.app is null)
        {
            return;
        }

        var appearance = Appearances.From(this.app.RequestedTheme == AppTheme.Dark, SystemAccessibility.IsHighContrast());
        var reduceMotion = SystemAccessibility.IsReduceMotion();
        var reduceTransparency = SystemAccessibility.IsReduceTransparency();
        // Акцент сравнивается тоже: сменив обои, телефон меняет цвета Material You, и при возврате их надо перечитать.
        var accent = SystemAccessibility.Accent(appearance);
        if (this.applied && appearance == this.Current && reduceMotion == this.ReduceMotion && reduceTransparency == this.ReduceTransparency
            && Equals(accent, this.accent))
        {
            return;
        }

        this.applied = true;
        this.Current = appearance;
        this.ReduceMotion = reduceMotion;
        this.ReduceTransparency = reduceTransparency;
        this.accent = accent;

        var colors = new Dictionary<string, Color>(ChronoColors.All.Count);
        foreach (var token in ChronoColors.All)
        {
            colors[token.Key] = Color.FromArgb(token.For(appearance));
        }

        // Системный акцент (Android 12+) вместо Tint из DESIGN.json; TintFill — тот же цвет на 16 %, как в DESIGN.json.
        if (accent is not null)
        {
            colors["Tint"] = accent;
            colors["TintFill"] = accent.WithAlpha(0.16f);
        }

        // Крупное стекло (шторка, меню, алерт) непрозрачно всегда: размытия фона в MAUI нет, и сквозь 14 % заливки
        // текст страницы позади читался поверх барабана и списков. Малое стекло кнопок просвечивает — оно маленькое.
        colors["GlassFillLarge"] = colors["GlassOpaque"];
        if (reduceTransparency)
        {
            colors["GlassFill"] = colors["GlassOpaque"];
        }

        var resources = this.app.Resources;
        foreach (var (key, color) in colors)
        {
            resources[key] = color;
        }

        // Ключи темы «Шлифованный металл · Кость», пока экраны на них ссылаются; удаляются в задаче плана «Чистка и приёмка».
        foreach (var (oldKey, newKey) in ChronoColors.Legacy)
        {
            resources[oldKey] = colors[newKey];
        }

        resources["CanvasBrush"] = new SolidColorBrush(colors["SystemGroupedBackground"]);
        resources["MetalBrush"] = new SolidColorBrush(colors["Tint"]);
        resources["GoldEdgeBrush"] = new SolidColorBrush(colors["Tint"]);
        var panel = colors["SecondarySystemGroupedBackground"];
        resources["FadeDownBrush"] = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(panel, 0f), new GradientStop(panel.WithAlpha(0f), 1f) },
        };
        resources["FadeUpBrush"] = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(panel.WithAlpha(0f), 0f), new GradientStop(panel, 1f) },
        };

        // Scroll edge шапки (LargeTitleHeader): содержимое под строкой кнопок гаснет к фону экрана, 24 pt.
        var screen = colors["SystemGroupedBackground"];
        resources["ScrollEdgeBrush"] = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(screen, 0f), new GradientStop(screen.WithAlpha(0f), 1f) },
        };

        this.Changed?.Invoke(this, EventArgs.Empty);
    }
}
