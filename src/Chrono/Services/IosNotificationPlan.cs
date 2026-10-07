using Chrono.Models;

namespace Chrono.Services;

public readonly record struct PlannedNotification(string Id, Alarm Alarm, DateTime FireAt);

/// <summary>
/// Цепочка уведомлений iOS: 4 уведомления на срабатывание (0/30/60/90 с), не более 16 ближайших срабатываний —
/// iOS хранит не более 64 запланированных уведомлений. Повторяющийся будильник разворачивается в несколько
/// срабатываний: без запуска приложения iOS не переставит следующее.
/// </summary>
public static class IosNotificationPlan
{
    public const int MaxAlarms = 16;

    public static readonly IReadOnlyList<TimeSpan> Offsets =
        [TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90)];

    // ponytail: запас — 16 срабатываний (у ежедневного 16 дней); дальше нужен запуск приложения.
    // Срабатывание k получает номера k*4..k*4+3: у ближайшего (звонящего) это ChainIds,
    // и снятие его хвоста не трогает следующие срабатывания.
    public static IReadOnlyList<PlannedNotification> Build(IEnumerable<Alarm> alarms, DateTime now)
    {
        var occurrences = new List<(Alarm Alarm, DateTime At, int K)>();
        foreach (var alarm in alarms.Where(a => a.IsEnabled && a.At > now))
        {
            var at = alarm.At;
            for (var k = 0; k < (alarm.Repeat == RepeatKind.None ? 1 : MaxAlarms); k++)
            {
                occurrences.Add((alarm, at, k));
                at = alarm.NextAfter(at);
            }
        }

        return occurrences
            .OrderBy(o => o.At)
            .Take(MaxAlarms)
            .SelectMany(o => Offsets.Select((offset, i) =>
                new PlannedNotification($"{o.Alarm.Id}-{o.K * Offsets.Count + i}", o.Alarm, o.At + offset)))
            .ToList();
    }

    /// <summary>Идентификаторы цепочки ближайшего срабатывания будильника.</summary>
    public static IReadOnlyList<string> ChainIds(Guid alarmId) =>
        Enumerable.Range(0, Offsets.Count).Select(i => $"{alarmId}-{i}").ToList();
}
