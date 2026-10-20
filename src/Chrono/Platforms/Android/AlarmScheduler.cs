using Android.App;
using Android.Content;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Планирование через AlarmManager по уровню оповещения: Ring — SetAlarmClock (точный, не ограничивается Doze),
/// Notify — точный будильник с уведомлением, None — ничего.
/// </summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    // ponytail: Cancel(id) и Sync снимают таймеры напоминаний только для этих типичных значений (Sync — ещё и для
    // текущих минут записи). Нетипичное значение (45 мин), убранное из записи, останется таймером, но AlarmReceiver
    // и ScheduleReceiver сверяют минуты с записью в хранилище и не звонят. Полный учёт — хранить прошлые минуты.
    private static readonly int[] KnownReminderMinutes = [5, 10, 15, 30, 60, 120, 1440];

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var now = DateTime.Now;

        foreach (var alarm in alarms)
        {
            var upcoming = alarm.IsEnabled && alarm.At > now;
            var ring = upcoming && alarm.Alert == AlertKind.Ring;
            var notify = upcoming && alarm.Alert == AlertKind.Notify;

            // Каждый уровень снимает чужой PendingIntent: запись, сменившая Ring на Notify, не должна звонить на весь экран.
            var operation = AlarmReceiver.CreatePendingIntent(context, alarm.Id);
            if (ring)
            {
                // Локальное «настенное» время → абсолютное по текущему поясу.
                var triggerAtMs = new DateTimeOffset(DateTime.SpecifyKind(alarm.At, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                // requestCode 3: при 0 этот PendingIntent совпал бы с full-screen intent сервиса
                // (extras в ключ PendingIntent не входят) и открывал бы экран сигнала чужого будильника.
                var showIntent = PendingIntent.GetActivity(
                    context, 3, new Intent(context, typeof(MainActivity)), PendingIntentFlags.Immutable);
                manager.SetAlarmClock(new AlarmManager.AlarmClockInfo(triggerAtMs, showIntent), operation);
            }
            else
            {
                manager.Cancel(operation);
            }

            // Уведомление без звонка: обычный точный будильник, SetAlarmClock занял бы значок будильника в статус-баре.
            var notifyOperation = ScheduleReceiver.CreatePendingIntent(context, alarm.Id);
            if (notify)
            {
                var notifyAtMs = new DateTimeOffset(DateTime.SpecifyKind(alarm.At, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, notifyAtMs, notifyOperation);
            }
            else
            {
                manager.Cancel(notifyOperation);
            }

            // Напоминания: по таймеру на каждый ещё не прошедший момент, тем же способом, что и начало.
            // Data у них свой (/r{минуты}), основной PendingIntent выше не затрагивается. Значения вне
            // текущего списка (смена в Google, Ring → Notify) снимаются как несрабатывающие.
            var reminders = alarm.Reminders();
            foreach (var minutes in reminders.Union(KnownReminderMinutes))
            {
                var reminderAt = alarm.At.AddMinutes(-minutes);
                var fires = reminders.Contains(minutes) && reminderAt > now;
                var reminderMs = new DateTimeOffset(DateTime.SpecifyKind(reminderAt, DateTimeKind.Local)).ToUnixTimeMilliseconds();

                var reminderRing = AlarmReceiver.CreatePendingIntent(context, alarm.Id, minutes);
                if (ring && fires)
                {
                    var reminderShow = PendingIntent.GetActivity(
                        context, 3, new Intent(context, typeof(MainActivity)), PendingIntentFlags.Immutable);
                    manager.SetAlarmClock(new AlarmManager.AlarmClockInfo(reminderMs, reminderShow), reminderRing);
                }
                else
                {
                    manager.Cancel(reminderRing);
                }

                var reminderNotify = ScheduleReceiver.CreatePendingIntent(context, alarm.Id, minutes);
                if (notify && fires)
                {
                    manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, reminderMs, reminderNotify);
                }
                else
                {
                    manager.Cancel(reminderNotify);
                }
            }

            // Начало окна умного пробуждения — обычный точный будильник: SetAlarmClock показал бы
            // в статус-баре время окна вместо времени будильника. Окно нужно только звонящим записям.
            var smartOperation = SmartWakeReceiver.CreatePendingIntent(context, alarm.Id);
            var windowStart = alarm.At - SleepPhaseEstimator.Window;
            if (ring && SmartWakeSettings.Enabled && windowStart > now)
            {
                var windowStartMs = new DateTimeOffset(DateTime.SpecifyKind(windowStart, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, windowStartMs, smartOperation);
            }
            else
            {
                manager.Cancel(smartOperation);
            }
        }
    }

    public void Cancel(Guid id)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id));
        manager.Cancel(ScheduleReceiver.CreatePendingIntent(context, id));
        manager.Cancel(SmartWakeReceiver.CreatePendingIntent(context, id));
        foreach (var minutes in KnownReminderMinutes)
        {
            manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id, minutes));
            manager.Cancel(ScheduleReceiver.CreatePendingIntent(context, id, minutes));
        }
    }

    public void StopRinging(Guid id)
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(AlarmRingService)));

        // Экран сигнала больше не должен показываться поверх блокировки.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity && OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            activity.SetShowWhenLocked(false);
            activity.SetTurnScreenOn(false);
        }
    }
}
