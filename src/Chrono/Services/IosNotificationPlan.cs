using Chrono.Models;

namespace Chrono.Services;

public readonly record struct PlannedNotification(string Id, Alarm Alarm, DateTime FireAt);

/// <summary>
/// Цепочка уведомлений iOS: 4 уведомления на будильник (0/30/60/90 с), не более 16 ближайших будильников —
/// iOS хранит не более 64 запланированных уведомлений.
/// </summary>
public static class IosNotificationPlan
{
    public const int MaxAlarms = 16;

    public static readonly IReadOnlyList<TimeSpan> Offsets =
        [TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90)];

    public static IReadOnlyList<PlannedNotification> Build(IEnumerable<Alarm> alarms, DateTime now) =>
        alarms
            .Where(a => a.IsEnabled && a.At > now)
            .OrderBy(a => a.At)
            .Take(MaxAlarms)
            .SelectMany(a => Offsets.Select((offset, i) => new PlannedNotification($"{a.Id}-{i}", a, a.At + offset)))
            .ToList();

    /// <summary>Идентификаторы всех уведомлений цепочки будильника.</summary>
    public static IReadOnlyList<string> ChainIds(Guid alarmId) =>
        Enumerable.Range(0, Offsets.Count).Select(i => $"{alarmId}-{i}").ToList();
}
