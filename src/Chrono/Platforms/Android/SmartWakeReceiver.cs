using Android.App;
using Android.Content;

namespace Chrono.Platform;

/// <summary>Начало окна умного пробуждения (за 30 минут до будильника): запускает слежение за фазой сна.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class SmartWakeReceiver : BroadcastReceiver
{
    /// <summary>PendingIntent начала окна; отличается от будильника Data-URI chrono://smart/{id}.</summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId)
    {
        var intent = new Intent(context, typeof(SmartWakeReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://smart/{alarmId}"))!
            .PutExtra(AlarmReceiver.ExtraAlarmId, alarmId.ToString())!;
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        // Точный будильник разрешает запуск foreground-сервиса из фона.
        var service = new Intent(context!, typeof(SmartWakeService))
            .PutExtra(AlarmReceiver.ExtraAlarmId, intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId));
        context!.StartForegroundService(service);
    }
}
