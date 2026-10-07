using Android.App;
using Android.Content;

namespace Chrono.Platform;

/// <summary>Получает срабатывание AlarmManager и запускает foreground-сервис звонка.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AlarmReceiver : BroadcastReceiver
{
    public const string ExtraAlarmId = "alarm_id";

    /// <summary>
    /// PendingIntent срабатывания. Будильники различаются Data-URI (requestCode у всех 0),
    /// поэтому Cancel находит ровно тот же PendingIntent.
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId)
    {
        var intent = new Intent(context, typeof(AlarmReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://alarm/{alarmId}"))!
            .PutExtra(ExtraAlarmId, alarmId.ToString())!;
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        var service = new Intent(context!, typeof(AlarmRingService))
            .PutExtra(ExtraAlarmId, intent?.GetStringExtra(ExtraAlarmId));
        context!.StartForegroundService(service);
    }
}
