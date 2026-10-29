namespace Chrono.Design;

/// <summary>Системные настройки, которые меняют вид Chrono (DESIGN.md §2.2, §2.9). Реализации — Platforms/*/Design.</summary>
public static partial class SystemAccessibility
{
    /// <summary>iOS — «Увеличение контраста»; Android 14+ — «Контрастность цветов» средняя или высокая.</summary>
    public static partial bool IsHighContrast();

    /// <summary>iOS — «Уменьшение движения»; Android — «Отключить анимацию».</summary>
    public static partial bool IsReduceMotion();

    /// <summary>iOS — «Понижение прозрачности»; на Android такой настройки нет.</summary>
    public static partial bool IsReduceTransparency();

    /// <summary>
    /// Подписка на изменения настроек. null — платформа о них не сообщает:
    /// ThemeService перечитывает их при возврате в приложение.
    /// </summary>
    public static partial IDisposable? Observe(Action changed);

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

#if !ANDROID && !IOS
    // Сборка без платформы (unit-тесты, проверка XAML): системных настроек нет.
    public static partial bool IsHighContrast() => false;

    public static partial bool IsReduceMotion() => false;

    public static partial bool IsReduceTransparency() => false;

    public static partial IDisposable? Observe(Action changed) => null;
#endif
}
