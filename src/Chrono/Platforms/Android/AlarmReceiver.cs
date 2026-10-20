using Android.App;
using Android.Content;

namespace Chrono.Platform;

/// <summary>Получает срабатывание AlarmManager и запускает foreground-сервис звонка.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AlarmReceiver : BroadcastReceiver
{
    public const string ExtraAlarmId = "alarm_id";

    /// <summary>За сколько минут до начала сработал таймер (0 или нет — само начало).</summary>
    public const string ExtraReminderMinutes = "reminder_minutes";

    /// <summary>
    /// PendingIntent срабатывания. Будильники различаются Data-URI (requestCode у всех 0),
    /// поэтому Cancel находит ровно тот же PendingIntent. Напоминание отличается суффиксом /r{минуты}:
    /// extras в ключ PendingIntent не входят, с тем же Data оно затёрло бы основной звонок.
    /// Основной (reminderMinutes = 0) остаётся chrono://alarm/{id}, как в прежних версиях.
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId, int reminderMinutes = 0)
    {
        var suffix = reminderMinutes > 0 ? $"/r{reminderMinutes}" : "";
        var intent = new Intent(context, typeof(AlarmReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://alarm/{alarmId}{suffix}"))!
            .PutExtra(ExtraAlarmId, alarmId.ToString())!
            .PutExtra(ExtraReminderMinutes, reminderMinutes)!;
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        var service = new Intent(context!, typeof(AlarmRingService))
            .PutExtra(ExtraAlarmId, intent?.GetStringExtra(ExtraAlarmId))
            .PutExtra(ExtraReminderMinutes, intent?.GetIntExtra(ExtraReminderMinutes, 0) ?? 0);
        context!.StartForegroundService(service);
    }
}
