using Chrono.Models;

namespace Chrono.Services;

/// <param name="ReminderMinutes">За сколько минут до начала срабатывает уведомление; 0 — само начало.</param>
public readonly record struct PlannedNotification(string Id, Alarm Alarm, DateTime FireAt, int ReminderMinutes = 0);

/// <summary>
/// Цепочка уведомлений iOS: 4 уведомления на срабатывание Ring (0/30/60/90 с) и одно на Notify и Beep (смещение 0),
/// не более 16 ближайших срабатываний на оба вида — iOS хранит не более 64 запланированных уведомлений.
/// Напоминания (Alarm.AlertTimes) — отдельные цепочки того же вида за N минут до начала; лимит считается на моменты.
/// Записи с Alert == None не планируются. Повторяющийся будильник разворачивается в несколько
/// срабатываний: без запуска приложения iOS не переставит следующее.
/// </summary>
public static class IosNotificationPlan
{
    public const int MaxAlarms = 16;

    public static readonly IReadOnlyList<TimeSpan> Offsets =
        [TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90)];

    // ponytail: запас — 16 срабатываний (у ежедневного 16 дней); дальше нужен запуск приложения.
    // ponytail: лимит 16 общий — уведомления дел вытесняют дальние будильники; при нехватке нужны отдельные лимиты.
    // Срабатывание k получает номера k*4..k*4+3: у ближайшего (звонящего) это ChainIds,
    // и снятие его хвоста не трогает следующие срабатывания.
    public static IReadOnlyList<PlannedNotification> Build(IEnumerable<Alarm> alarms, DateTime now)
    {
        var moments = new List<(Alarm Alarm, DateTime At, int K, int Minutes)>();
        foreach (var alarm in alarms.Where(a => a.IsEnabled && a.Alert != AlertKind.None && a.At > now))
        {
            var at = alarm.At;
            for (var k = 0; k < (alarm.IsOneShot() ? 1 : MaxAlarms); k++)
            {
                // Серия закончилась: NextAfter дату окончания не учитывает, дальше срабатываний нет.
                if (alarm.Ended(at))
                {
                    break;
                }

                // Напоминание, время которого уже прошло, не планируется: iOS не сработает по прошедшему времени.
                foreach (var moment in alarm.AlertTimes(at).Where(t => t > now))
                {
                    moments.Add((alarm, moment, k, (int)(at - moment).TotalMinutes));
                }

                at = alarm.NextAfter(at);
            }
        }

        // Номера цепочки: срабатывание k получает k*4..k*4+3 — у ближайшего начала это ChainIds,
        // и снятие его хвоста не трогает следующие срабатывания. У напоминания тот же счёт со своим префиксом.
        return moments
            .OrderBy(o => o.At)
            .Take(MaxAlarms)
            .SelectMany(o => Offsets
                .Take(o.Alarm.Alert is AlertKind.Notify or AlertKind.Beep ? 1 : Offsets.Count)
                .Select((offset, i) => new PlannedNotification(
                    o.Minutes == 0 ? $"{o.Alarm.Id}-{o.K * Offsets.Count + i}" : $"{o.Alarm.Id}-r{o.Minutes}-{o.K * Offsets.Count + i}",
                    o.Alarm,
                    o.At + offset,
                    o.Minutes)))
            .ToList();
    }

    /// <summary>Идентификаторы цепочки ближайшего срабатывания будильника.</summary>
    public static IReadOnlyList<string> ChainIds(Guid alarmId) =>
        Enumerable.Range(0, Offsets.Count).Select(i => $"{alarmId}-{i}").ToList();

    /// <summary>Идентификатор принадлежит напоминанию ({id}-r{минуты}-{n}); в GUID буквы r нет.</summary>
    public static bool IsReminder(string notificationId) => notificationId.Contains("-r");

    /// <summary>Собственная цепочка напоминания: остановка его звонка не должна снимать цепочку начала (ChainIds).</summary>
    public static IReadOnlyList<string> ReminderChainIds(string notificationId)
    {
        var cut = notificationId.LastIndexOf('-');
        var first = int.Parse(notificationId[(cut + 1)..], System.Globalization.CultureInfo.InvariantCulture) / Offsets.Count * Offsets.Count;
        return Enumerable.Range(first, Offsets.Count).Select(n => $"{notificationId[..cut]}-{n}").ToList();
    }
}
