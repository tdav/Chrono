using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Chrono.Platform;
using Chrono.Services;

namespace Chrono;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Запуск из full-screen intent сигнала: показываемся поверх блокировки и открываем RingPage.
        if (Guid.TryParse(this.Intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId))
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

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

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
}
