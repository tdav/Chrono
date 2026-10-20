using System.Globalization;
using Android.App;
using Android.Content;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Срабатывание записи уровня Notify: обычное уведомление без звонка и full-screen intent.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ScheduleReceiver : BroadcastReceiver
{
    public const string ChannelId = "schedule";

    /// <summary>День для вкладки «Распорядок» в формате yyyy-MM-dd (InvariantCulture).</summary>
    public const string ExtraOpenDay = "open_day";

    /// <summary>
    /// PendingIntent срабатывания. От будильника (chrono://alarm/{id}) отличается и классом, и Data-URI:
    /// иначе Cancel одного снимал бы другой.
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId)
    {
        var intent = new Intent(context, typeof(ScheduleReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://notify/{alarmId}"))!
            .PutExtra(AlarmReceiver.ExtraAlarmId, alarmId.ToString())!;
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || !Guid.TryParse(intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId))
        {
            return;
        }

        var alarm = new AlarmStore(FileSystem.AppDataDirectory).Load().Alarms.FirstOrDefault(a => a.Id == alarmId);

        // RescheduleAll обязан выполниться даже при сбое показа: иначе повторяющаяся запись не встанет на следующее срабатывание.
        try
        {
            // Запись удалили, выключили или перевели на другой уровень после постановки будильника: показывать нечего.
            if (alarm is { IsEnabled: true, Alert: AlertKind.Notify })
            {
                var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
                // Канал создаётся при каждом срабатывании: повторное создание с тем же id ничего не меняет.
                notifications.CreateNotificationChannel(
                    new NotificationChannel(ChannelId, AppResources.ChannelSchedule, NotificationImportance.Default));

                var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
                var timeText = alarm.DurationMinutes > 0
                    ? $"{alarm.At:HH:mm}–{alarm.At.AddMinutes(alarm.DurationMinutes):HH:mm}"
                    : alarm.At.ToString("HH:mm");

                // Id 1..3 заняты (RingNotificationId, MissedNotificationId, уведомление умного пробуждения): старший бит
                // исключает совпадение хэша с ними, иначе Notify затёрло бы уведомление звонка.
                var notificationId = alarmId.GetHashCode() | 0x40000000;

                // requestCode = хэш id: extras в ключ PendingIntent не входят, с общим кодом все уведомления открывали бы один день.
                // Data делает ключ уникальным и при совпадении хэша с requestCode 0/2/3 других PendingIntent на MainActivity.
                var content = PendingIntent.GetActivity(
                    context,
                    alarmId.GetHashCode(),
                    new Intent(context, typeof(MainActivity))
                        .SetData(global::Android.Net.Uri.Parse($"chrono://day/{alarmId}"))!
                        .PutExtra(ExtraOpenDay, DateOnly.FromDateTime(alarm.At).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))!
                        .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

                var notification = new Notification.Builder(context, ChannelId)
                    .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
                    .SetContentTitle(label)!
                    .SetContentText(timeText)!
                    .SetContentIntent(content)!
                    .SetAutoCancel(true)!
                    .Build()!;
                notifications.Notify(notificationId, notification);
            }
        }
        finally
        {
            // Повторяющаяся запись встаёт на следующее срабатывание сразу, не дожидаясь открытия приложения.
            new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System).RescheduleAll();
        }
    }
}
