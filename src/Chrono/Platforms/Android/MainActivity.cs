using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Chrono.Platform;
using Chrono.Services;

namespace Chrono;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
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

        base.OnCreate(savedInstanceState);

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

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        SmartWake.OnActivityResult(requestCode);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent?.Action == ActionShowHealthRationale)
        {
            SettingsLauncher.Request();
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
}
