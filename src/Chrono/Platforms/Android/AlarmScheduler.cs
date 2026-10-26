using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Android.App;
using Android.Content;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Метаданные JSON реестра таймеров Notify, сгенерированные при компиляции (без отражения на холодном старте).</summary>
[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal sealed partial class NotifyKeysJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Планирование через AlarmManager по уровню оповещения: Ring — SetAlarmClock со звонком, Notify — SetAlarmClock
/// с уведомлением, Beep — то же уведомление и короткий сигнал, None — ничего. SetAlarmClock не ограничивают Doze,
/// группы ожидания и выгрузка процесса.
/// </summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    // ponytail: Cancel(id) и Sync снимают таймеры напоминаний только для этих типичных значений (Sync — ещё и для
    // текущих минут записи). Нетипичное значение (45 мин), убранное из записи, останется таймером, но AlarmReceiver
    // и ScheduleReceiver сверяют минуты с записью в хранилище и не звонят. Полный учёт — хранить прошлые минуты.
    private static readonly int[] KnownReminderMinutes = [5, 10, 15, 30, 60, 120, 1440];

    // Реестр таймеров Notify: id записи → "тики At" (начало) или "тики At/минуты" (напоминание). Время входит в ключ PendingIntent
    // (см. ScheduleReceiver), поэтому снять таймер прежнего времени (правка, удаление) можно, только зная его.
    private const string NotifyKeysPref = "notify_keys";

    private static readonly object NotifyKeysLock = new();

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var now = DateTime.Now;

        // Весь проход под замком: Sync вызывают и главный поток, и фоновая синхронизация с Google.
        lock (NotifyKeysLock)
        {
            Dictionary<string, string[]> notifyKeys;
            try
            {
                notifyKeys = JsonSerializer.Deserialize(Preferences.Default.Get(NotifyKeysPref, "{}"), NotifyKeysJsonContext.Default.DictionaryStringStringArray) ?? [];
            }
            catch (JsonException)
            {
                notifyKeys = [];
            }

            // Записей, которых нет в списке, уже нет: их удалил пользователь (таймеры снял Cancel) или GetAll (таймер сработал
            // или ждёт доставки, приёмник покажет уведомление по extras). Реестр их не держит.
            notifyKeys = notifyKeys.Where(k => alarms.Any(a => a.Id.ToString() == k.Key)).ToDictionary(k => k.Key, k => k.Value);

            try
            {
                foreach (var alarm in alarms)
                {
                    // После даты окончания повторяющаяся запись не срабатывает: ни звонка, ни уведомления, ни напоминаний.
                    var upcoming = alarm.IsEnabled && alarm.At > now && !alarm.Ended(alarm.At);
                    var ring = upcoming && alarm.Alert == AlertKind.Ring;
                    // Короткий сигнал едет теми же таймерами, что и уведомление: звук играет приёмник (см. ScheduleReceiver).
                    var notify = upcoming && alarm.Alert is AlertKind.Notify or AlertKind.Beep;

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

                    // Уведомление без звонка тоже через SetAlarmClock: SetExactAndAllowWhileIdle при выгруженном приложении
                    // система и оболочки производителей откладывают или не доставляют вовсе, и короткий сигнал не звучал.
                    // Цена — значок будильника в статус-баре и на этих временах. Нужные таймеры — начало и ещё не прошедшие
                    // напоминания; ключ каждого содержит At.
                    var wanted = new List<string>();
                    if (notify)
                    {
                        wanted.Add(alarm.At.Ticks.ToString(CultureInfo.InvariantCulture));
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

                        if (notify && fires)
                        {
                            wanted.Add($"{alarm.At.Ticks.ToString(CultureInfo.InvariantCulture)}/{minutes}");
                        }
                    }

                    // Прежние таймеры Notify, которые больше не нужны (правка времени или напоминаний, выключение, смена уровня), снимаются
                    // по известному ключу. Только будущие: прошедший момент уже доставлен или ждёт отложенной доставки (Doze), Cancel
                    // его потерял бы. Если запись ещё жива, приёмник сам отсечёт устаревший ключ (Alarm.IsOccurrence).
                    foreach (var key in notifyKeys.GetValueOrDefault(alarm.Id.ToString()) ?? [])
                    {
                        if (wanted.Contains(key))
                        {
                            continue;
                        }

                        var parts = key.Split('/');
                        var at = new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture));
                        var oldMinutes = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
                        if (at.AddMinutes(-oldMinutes) > now)
                        {
                            manager.Cancel(ScheduleReceiver.CreatePendingIntent(context, alarm.Id, at, oldMinutes));
                        }
                    }

                    foreach (var key in wanted)
                    {
                        var parts = key.Split('/');
                        var minutes = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
                        var triggerMs = new DateTimeOffset(DateTime.SpecifyKind(alarm.At.AddMinutes(-minutes), DateTimeKind.Local)).ToUnixTimeMilliseconds();
                        var notifyShow = PendingIntent.GetActivity(
                            context, 3, new Intent(context, typeof(MainActivity)), PendingIntentFlags.Immutable);
                        manager.SetAlarmClock(
                            new AlarmManager.AlarmClockInfo(triggerMs, notifyShow),
                            ScheduleReceiver.CreatePendingIntent(context, alarm.Id, alarm.At, minutes, alarm.Label, alarm.DurationMinutes, alarm.Alert == AlertKind.Beep));
                    }

                    notifyKeys[alarm.Id.ToString()] = [.. wanted];

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
            finally
            {
                // Сохраняем и при сбое посреди прохода: поставленные таймеры должны остаться известными, чтобы их можно было снять.
                Preferences.Default.Set(NotifyKeysPref, JsonSerializer.Serialize(notifyKeys, NotifyKeysJsonContext.Default.DictionaryStringStringArray));
            }
        }
    }

    public void Cancel(Guid id)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id));
        manager.Cancel(SmartWakeReceiver.CreatePendingIntent(context, id));
        foreach (var minutes in KnownReminderMinutes)
        {
            manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id, minutes));
        }

        // Уведомления: все таймеры записи из реестра, в том числе уже прошедшие и ждущие доставки (запись удалили).
        lock (NotifyKeysLock)
        {
            Dictionary<string, string[]> notifyKeys;
            try
            {
                notifyKeys = JsonSerializer.Deserialize(Preferences.Default.Get(NotifyKeysPref, "{}"), NotifyKeysJsonContext.Default.DictionaryStringStringArray) ?? [];
            }
            catch (JsonException)
            {
                notifyKeys = [];
            }

            foreach (var key in notifyKeys.GetValueOrDefault(id.ToString()) ?? [])
            {
                var parts = key.Split('/');
                manager.Cancel(ScheduleReceiver.CreatePendingIntent(
                    context, id, new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture)), parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0));
            }

            notifyKeys.Remove(id.ToString());
            Preferences.Default.Set(NotifyKeysPref, JsonSerializer.Serialize(notifyKeys, NotifyKeysJsonContext.Default.DictionaryStringStringArray));
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
