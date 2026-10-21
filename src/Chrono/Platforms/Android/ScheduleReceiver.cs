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

    // Время срабатывания (At записи) входит в Data-URI ключа PendingIntent: следующее срабатывание повторяющейся записи
    // и правка времени не затирают ещё не доставленный таймер (Doze откладывает SetExactAndAllowWhileIdle). Подпись и длительность
    // едут в extras: к запуску приёмника GetAll соседнего приёмника уже мог удалить «Один раз и удалить» без длительности (D3).
    private const string ExtraAtTicks = "at_ticks";
    private const string ExtraLabel = "label";
    private const string ExtraDuration = "duration_minutes";

    /// <summary>
    /// PendingIntent срабатывания. От будильника (chrono://alarm/{id}) отличается и классом, и Data-URI:
    /// иначе Cancel одного снимал бы другой. Ключ — id, время начала at и (для напоминания) суффикс /r{минуты}.
    /// Подпись и длительность нужны только при постановке; для Cancel не важны (extras в ключ не входят).
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId, DateTime at, int reminderMinutes = 0, string label = "", int durationMinutes = 0)
    {
        var suffix = reminderMinutes > 0 ? $"/r{reminderMinutes}" : "";
        var intent = new Intent(context, typeof(ScheduleReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://notify/{alarmId}/{at.Ticks}{suffix}"))!
            .PutExtra(AlarmReceiver.ExtraAlarmId, alarmId.ToString())!
            .PutExtra(AlarmReceiver.ExtraReminderMinutes, reminderMinutes)!
            .PutExtra(ExtraAtTicks, at.Ticks)!
            .PutExtra(ExtraLabel, label)!
            .PutExtra(ExtraDuration, durationMinutes)!;
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
            var ticks = intent.GetLongExtra(ExtraAtTicks, 0);
            var reminderMinutes = intent.GetIntExtra(AlarmReceiver.ExtraReminderMinutes, 0);
            var at = new DateTime(ticks);

            // Записи нет: «Один раз и удалить» без длительности удалил GetAll соседнего приёмника или таймера вкладки раньше, чем
            // сработал этот (I1). Удаление снимает таймеры будущих срабатываний; таймер, чей момент уже прошёл и выпал из реестра,
            // но доставка отложена (Doze), не снимается — такое уведомление придёт и для записи, удалённой после её времени:
            // срок уже наступил, это допустимо. Напоминание раньше начала: записи к нему ещё не удаляются.
            // Запись есть: уровень сменили, время или напоминания поправили (таймер устарел) — показывать нечего. IsEnabled
            // проверяется только у повторяющейся: GetAll её не выключает, значит, выключил пользователь (M2); у разовой флаг
            // мог сбросить GetAll после срабатывания (D3), а выключенную заранее Sync уже снял с AlarmManager.
            var show = ticks != 0 && (alarm is null
                ? reminderMinutes == 0
                : alarm.Alert == AlertKind.Notify
                    && (alarm.IsEnabled || alarm.IsOneShot())
                    && alarm.IsOccurrence(at)
                    && (reminderMinutes == 0 || alarm.Reminders().Contains(reminderMinutes)));
            if (show)
            {
                var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
                notifications.DeleteNotificationChannel(OldChannelId);
                // Канал создаётся при каждом срабатывании: повторное создание с тем же id ничего не меняет.
                // Звук канала системный (по умолчанию), вибрация включена явно.
                var channel = new NotificationChannel(ChannelId, AppResources.ChannelSchedule, NotificationImportance.Default);
                channel.EnableVibration(true);
                notifications.CreateNotificationChannel(channel);

                var labelText = alarm?.Label ?? intent.GetStringExtra(ExtraLabel);
                var label = string.IsNullOrWhiteSpace(labelText) ? AppResources.DefaultLabel : labelText;
                var duration = alarm?.DurationMinutes ?? intent.GetIntExtra(ExtraDuration, 0);
                var timeText = duration > 0
                    ? $"{at:HH:mm}–{at.AddMinutes(duration):HH:mm}"
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
