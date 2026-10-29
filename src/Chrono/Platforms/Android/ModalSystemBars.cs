using Android.Content.Res;
using AndroidX.Core.View;
using AndroidX.Fragment.App;

namespace Chrono;

/// <summary>
/// Значки статус-бара и панели навигации в модальных страницах (DESIGN.md §2.10). Модальная страница MAUI (маршрут edit,
/// Shell.PresentationMode="ModalAnimated") — отдельное окно DialogFragment. MAUI выбирает в нём тёмные значки по яркости
/// colorPrimary, а у Chrono это цвет акцента, а не фон экрана: значки получались белыми на светлой странице. Здесь значки
/// ставятся по теме, как в MainActivity, а принудительная подложка панели навигации (3 кнопки) отключается.
/// </summary>
internal sealed class ModalSystemBars : FragmentManager.FragmentLifecycleCallbacks
{
    // Окно диалога готово и показано (после OnCreateDialog, где MAUI ставит свои значки): ставим свои.
    public override void OnFragmentStarted(FragmentManager? fm, Fragment? f)
    {
        if (f is DialogFragment { Dialog.Window: { } window, Resources.Configuration: { } config })
        {
            Apply(window, config);
        }
    }

    // Тема сменилась при открытой модальной странице: активити не пересоздаётся (ConfigChanges.UiMode), окна диалогов перекрашиваются здесь.
    internal static void Refresh(FragmentManager fm, Configuration config)
    {
        foreach (var fragment in fm.Fragments!)
        {
            if (fragment is DialogFragment { Dialog.Window: { } window })
            {
                Apply(window, config);
            }
        }
    }

    private static void Apply(Android.Views.Window window, Configuration config)
    {
        var dark = (config.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var insets = WindowCompat.GetInsetsController(window, window.DecorView)!;
        insets.AppearanceLightStatusBars = !dark;
        insets.AppearanceLightNavigationBars = !dark;
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            window.NavigationBarContrastEnforced = false;
        }
    }
}
