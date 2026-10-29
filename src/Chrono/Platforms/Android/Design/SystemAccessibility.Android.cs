using Android.Animation;
using Android.App;

namespace Chrono.Design;

public static partial class SystemAccessibility
{
    // «Контрастность цветов» Android 14+: 0 — обычная, 0.5 — средняя, 1 — высокая. Средняя и высокая → контрастная тема.
    private const float HighContrastFrom = 0.5f;

    public static partial bool IsHighContrast() =>
        OperatingSystem.IsAndroidVersionAtLeast(34)
        && UiModeManager.FromContext(global::Android.App.Application.Context) is { } modes
        && modes.Contrast >= HighContrastFrom;

    // «Отключить анимацию» в специальных возможностях = масштаб длительности анимаций 0.
    public static partial bool IsReduceMotion() => !ValueAnimator.AreAnimatorsEnabled();

    public static partial bool IsReduceTransparency() => false;

    // О смене контраста Android 14+ сообщает слушателю; «Отключить анимацию» — нет, её ThemeService перечитывает в Refresh.
    public static partial IDisposable? Observe(Action changed)
    {
        var context = global::Android.App.Application.Context;
        if (!OperatingSystem.IsAndroidVersionAtLeast(34)
            || UiModeManager.FromContext(context) is not { } modes
            || context.MainExecutor is not { } executor)
        {
            return null;
        }

        var listener = new ContrastListener(changed);
        modes.AddContrastChangeListener(executor, listener);
        return new Subscription(() =>
        {
            // Проверка версии повторяется: анализатор платформ не переносит её внутрь лямбды (CA1416).
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                modes.RemoveContrastChangeListener(listener);
            }
        });
    }

    // Тона Material You под пары с OnTint из DESIGN.json: светлая — тон 40 (system_accent1_600, белый текст), тёмная — тон 60 (_400),
    // «контраст» светлая — тон 30 (_700), «контраст» тёмная — тон 80 (_200, текст на нём чёрный).
    public static partial Color? Accent(Appearance appearance) =>
        OperatingSystem.IsAndroidVersionAtLeast(31)
            ? Color.FromInt(global::Android.App.Application.Context.GetColor(appearance switch
            {
                Appearance.Dark => global::Android.Resource.Color.SystemAccent1400,
                Appearance.LightHighContrast => global::Android.Resource.Color.SystemAccent1700,
                Appearance.DarkHighContrast => global::Android.Resource.Color.SystemAccent1200,
                _ => global::Android.Resource.Color.SystemAccent1600,
            }))
            : null;

    private sealed class ContrastListener(Action changed) : Java.Lang.Object, UiModeManager.IContrastChangeListener
    {
        public void OnContrastChanged(float contrast) => changed();
    }
}
