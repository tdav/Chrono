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
    // Настройки канала после создания не меняются: вибрацию получил только новый id (старый «schedule» без неё удаляется).
    public const string ChannelId = "schedule_v2";

    private const string OldChannelId = "schedule";

    /// <summary>День для вкладки «Распорядок» в формате yyyy-MM-dd (InvariantCulture).</summary>
    public const string ExtraOpenDay = "open_day";

    // Время срабатывания едет в самом PendingIntent: к запуску приёмника GetAll (RescheduleAll соседнего приёмника, таймер вкладки)
    // уже мог выключить разовую запись или перенести повторяющуюся на следующий раз, и в хранилище At стал другим.
    private const string ExtraAtTicks = "at_ticks";

    /// <summary>
    /// PendingIntent срабатывания. От будильника (chrono://alarm/{id}) отличается и классом, и Data-URI:
    /// иначе Cancel одного снимал бы другой. Напоминание — суффикс /r{минуты}, основной остаётся прежним.
    /// at — время начала записи для постановки; для Cancel не нужно (extras в ключ PendingIntent не входят).
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId, int reminderMinutes = 0, DateTime? at = null)
    {
        var suffix = reminderMinutes > 0 ? $"/r{reminderMinutes}" : "";
        var intent = new Intent(context, typeof(ScheduleReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://notify/{alarmId}{suffix}"))!
            .PutExtra(AlarmReceiver.ExtraAlarmId, alarmId.ToString())!
            .PutExtra(AlarmReceiver.ExtraReminderMinutes, reminderMinutes)!;
        if (at is not null)
        {
            intent.PutExtra(ExtraAtTicks, at.Value.Ticks);
        }

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
            // Запись удалили или перевели на другой уровень после постановки будильника: показывать нечего.
            // IsEnabled не проверяется: выключенную пользователем запись Sync уже снял с AlarmManager, а у сработавшей
            // разовой GetAll мог успеть сбросить флаг раньше этого приёмника (D3). Таймер напоминания, которого в записи
            // уже нет (поменяли в Google), тоже ничего не показывает.
            var reminderMinutes = intent.GetIntExtra(AlarmReceiver.ExtraReminderMinutes, 0);
            if (alarm is { Alert: AlertKind.Notify } && (reminderMinutes == 0 || alarm.Reminders().Contains(reminderMinutes)))
            {
                var ticks = intent.GetLongExtra(ExtraAtTicks, 0);
                var at = ticks != 0 ? new DateTime(ticks) : alarm.At;
                var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
                notifications.DeleteNotificationChannel(OldChannelId);
                // Канал создаётся при каждом срабатывании: повторное создание с тем же id ничего не меняет.
                // Звук канала системный (по умолчанию), вибрация включена явно.
                var channel = new NotificationChannel(ChannelId, AppResources.ChannelSchedule, NotificationImportance.Default);
                channel.EnableVibration(true);
                notifications.CreateNotificationChannel(channel);

                var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
                var timeText = alarm.DurationMinutes > 0
                    ? $"{at:HH:mm}–{at.AddMinutes(alarm.DurationMinutes):HH:mm}"
                    : at.ToString("HH:mm");

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
                        .PutExtra(ExtraOpenDay, DateOnly.FromDateTime(at).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))!
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
            new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System, new SyncStore(FileSystem.AppDataDirectory)).RescheduleAll();
        }
    }
}
