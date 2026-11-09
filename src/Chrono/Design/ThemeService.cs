using Chrono.Services;

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

    /// <summary>Акцент из настроек: ключ AccentPalette или null — системный (а где его нет — Tint из DESIGN.json).</summary>
    public string? AccentKey { get; private set; }

    /// <summary>Системный акцент есть (Android 12+): в настройках показывается кружок «Системный».</summary>
    public bool HasSystemAccent => SystemAccessibility.Accent(this.Current) is not null;

    /// <summary>
    /// Заливка акцентом и цвет символа на ней для выбора из настроек: ключ AccentPalette или null — системный акцент.
    /// null в ответе — системного акцента нет, действуют Tint и OnTint из DESIGN.json.
    /// </summary>
    public static (Color Fill, Color On)? Accent(string? key, Appearance appearance)
    {
        if (key is not null)
        {
            var hex = AccentPalette.Hex(key, appearance);
            return (Color.FromArgb(hex), Color.FromArgb(AccentPalette.OnHex(hex, appearance)));
        }

        // OnTint в DESIGN.json — чёрный: он рассчитан на запасной оранжевый. Тона Material You для Light, Dark и LightHighContrast
        // (40, 60, 30) тёмные, и чёрный на них нечитаем, поэтому символ и текст на акценте — белые; тон 80 у DarkHighContrast
        // светлый — там чёрный.
        return SystemAccessibility.Accent(appearance) is { } system
            ? (system, appearance == Appearance.DarkHighContrast ? Colors.Black : Colors.White)
            : null;
    }

    /// <summary>Выбор акцента в настройках: сохраняется и применяется сразу. null — системный.</summary>
    public void SetAccent(string? key)
    {
        ThemeSettings.Accent = AccentPalette.Normalize(key);
        this.Refresh();
    }

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
        // Акцент сравнивается тоже: сменив обои, телефон меняет цвета Material You, и при возврате их надо перечитать;
        // выбор цвета в настройках (SetAccent) приходит сюда же.
        var accentKey = AccentPalette.Normalize(ThemeSettings.Accent);
        var accent = Accent(accentKey, appearance);
        if (this.applied && appearance == this.Current && reduceMotion == this.ReduceMotion && reduceTransparency == this.ReduceTransparency
            && accentKey == this.AccentKey && Equals(accent?.Fill, this.accent))
        {
            return;
        }

        this.applied = true;
        this.Current = appearance;
        this.ReduceMotion = reduceMotion;
        this.ReduceTransparency = reduceTransparency;
        this.AccentKey = accentKey;
        this.accent = accent?.Fill;

        var colors = new Dictionary<string, Color>(ChronoColors.All.Count);
        foreach (var token in ChronoColors.All)
        {
            colors[token.Key] = Color.FromArgb(token.For(appearance));
        }

        // Акцент из настроек или системный (Android 12+) вместо Tint из DESIGN.json; TintFill — тот же цвет на 16 %,
        // Success — сам акцент (тумблер «вкл», ✓ статуса: зелёного в приложении нет, решение пользователя) — как в DESIGN.json.
        // OnTint — цвет символа на акценте (см. Accent). OnWarning и OnColor акцент не трогает: Warning остаётся оранжевым,
        // а OnColor лежит на заливках, которые от акцента не зависят.
        if (accent is var (fill, on))
        {
            colors["Tint"] = fill;
            colors["TintFill"] = fill.WithAlpha(0.16f);
            colors["Success"] = fill;
            colors["OnTint"] = on;
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
