using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Chrono.Platform;
using Chrono.Services;

namespace Chrono;

// Тема запуска — Maui.SplashTheme со своим значком системной заставки Android 12+ (Resources/values*/styles.xml, DESIGN.md §4.7).
[Activity(Theme = "@style/Chrono.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Health Connect (Android 13 и ниже) открывает это действие, чтобы показать, зачем приложению данные о здоровье.
[IntentFilter([ActionShowHealthRationale])]
public class MainActivity : MauiAppCompatActivity
{
    public const string ActionShowHealthRationale = "androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (this.Intent?.Action == ActionShowHealthRationale)
        {
            SettingsLauncher.Request();
        }

        // Запуск из full-screen intent сигнала. Id передаётся RingLauncher до base.OnCreate: там MAUI
        // создаёт окно (App.CreateWindow), и оно должно знать о сигнале, чтобы пропустить splash.
        var startedByAlarm = Guid.TryParse(this.Intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId);
        if (startedByAlarm)
        {
            RingLauncher.Request(alarmId);
        }

        // Нажатие на уведомление распорядка: день запоминается до создания Shell. Поверх блокировки не показываемся.
        if (DateOnly.TryParseExact(
            this.Intent?.GetStringExtra(ScheduleReceiver.ExtraOpenDay),
            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var openDay))
        {
            ScheduleLauncher.Request(openDay);
        }

        // Окно покажет заставку (Views/SplashPage), если создаётся не ради сигнала — то же условие, что в App.CreateWindow.
        var splash = RingLauncher.PendingId is null;

        StartTrace.Mark("MainActivity.OnCreate: до base");
        base.OnCreate(savedInstanceState);
        StartTrace.Mark("MainActivity.OnCreate: после base");

        // Системная заставка Android 12+ показывает тот же кадр, с которого начинается SplashPage (DESIGN.md §4.7), поэтому
        // убирается сразу, без системной анимации ухода: та на разных версиях Android своя, и кадры разошлись бы на её время.
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            this.SplashScreen.SetOnExitAnimationListener(new SplashExit());
        }

        // Значки статус-бара и панели навигации: тёмные в светлой теме, светлые в тёмной (DESIGN.md §2.10).
        // Тема — из настроек или системы; AppCompat отражает её в Configuration.UiMode. Фон заставки чёрный в любой теме:
        // пока она на экране, значки светлые, по теме их вернёт SplashPage.
        var dark = (this.Resources!.Configuration!.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var insets = AndroidX.Core.View.WindowCompat.GetInsetsController(this.Window!, this.Window!.DecorView)!;
        insets.AppearanceLightStatusBars = !dark && !splash;
        insets.AppearanceLightNavigationBars = !dark && !splash;

        // Панель навигации прозрачная и при трёх кнопках: без этого Android рисует под ними полупрозрачную подложку.
        // Статус-бар прозрачен всегда: targetSdk 37, на Android 15+ edge-to-edge принудителен.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            this.Window!.NavigationBarContrastEnforced = false;
        }

        // Модальные страницы (маршрут edit) — отдельные окна: значки их панелей ставятся по теме так же.
        this.SupportFragmentManager!.RegisterFragmentLifecycleCallbacks(new ModalSystemBars(), false);

        // Показываемся поверх блокировки.
        if (startedByAlarm)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(27))
            {
                this.SetShowWhenLocked(true);
                this.SetTurnScreenOn(true);
            }
            else
            {
                this.Window!.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
            }
        }
    }

    // Смена темы в настройках или в системе: активити не пересоздаётся (ConfigChanges.UiMode), значки панелей перекрашиваются здесь.
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        var dark = (newConfig.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var insets = AndroidX.Core.View.WindowCompat.GetInsetsController(this.Window!, this.Window!.DecorView)!;
        insets.AppearanceLightStatusBars = !dark;
        insets.AppearanceLightNavigationBars = !dark;
        ModalSystemBars.Refresh(this.SupportFragmentManager!, newConfig);
    }

    protected override void OnResume()
    {
        StartTrace.Mark("MainActivity.OnResume");
        base.OnResume();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        SmartWake.OnActivityResult(requestCode);
        GoogleAuth.OnActivityResult(requestCode, resultCode, data);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent?.Action == ActionShowHealthRationale)
        {
            SettingsLauncher.Request();
            return;
        }

        if (DateOnly.TryParseExact(
            intent?.GetStringExtra(ScheduleReceiver.ExtraOpenDay),
            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var openDay))
        {
            ScheduleLauncher.Request(openDay);
            return;
        }

        // Активити уже запущена (SingleTop): тот же сценарий, что и в OnCreate.
        if (Guid.TryParse(intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId))
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(27))
            {
                this.SetShowWhenLocked(true);
                this.SetTurnScreenOn(true);
            }
            else
            {
                this.Window!.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
            }

            RingLauncher.Request(alarmId);
        }
    }

    // Уход системной заставки: она снимается в тот же миг — под ней уже нарисован такой же первый кадр SplashPage.
    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private sealed class SplashExit : Java.Lang.Object, Android.Window.ISplashScreenOnExitAnimationListener
    {
        public void OnSplashScreenExit(Android.Window.SplashScreenView view) => view.Remove();
    }
}
