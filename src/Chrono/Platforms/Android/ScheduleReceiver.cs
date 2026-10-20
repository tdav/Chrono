using System.Globalization;
using Android.App;
using Android.Content;
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

        // Запись удалили после постановки будильника: показывать нечего, но остальные записи нужно переставить.
        if (alarm is not null)
        {
            var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
            // Канал создаётся при каждом срабатывании: повторное создание с тем же id ничего не меняет.
            notifications.CreateNotificationChannel(
                new NotificationChannel(ChannelId, AppResources.ChannelSchedule, NotificationImportance.Default));

            var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
            var timeText = alarm.DurationMinutes > 0
                ? $"{alarm.At:HH:mm}–{alarm.At.AddMinutes(alarm.DurationMinutes):HH:mm}"
                : alarm.At.ToString("HH:mm");

            // requestCode = хэш id: extras в ключ PendingIntent не входят, с общим кодом все уведомления открывали бы один день.
            var notificationId = alarmId.GetHashCode();
            var content = PendingIntent.GetActivity(
                context,
                notificationId,
                new Intent(context, typeof(MainActivity))
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

        // Повторяющаяся запись встаёт на следующее срабатывание сразу, не дожидаясь открытия приложения.
        new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System).RescheduleAll();
    }
}
