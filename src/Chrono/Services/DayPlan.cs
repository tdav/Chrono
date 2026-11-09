using Chrono.Models;

namespace Chrono.Services;

/// <summary>Запись дня в ленте «Распорядок».</summary>
public readonly record struct DayEntry(Alarm Alarm, DateTime Start, DateTime End, bool IsCurrent, bool IsDone, bool IsMuted);

/// <summary>Расчёты экрана «Распорядок» без зависимостей от MAUI: записи дня и неделя.</summary>
public static class DayPlan
{
    /// <summary>
    /// Записи, приходящиеся на день, по возрастанию начала, затем названия. Дело через полночь показывается только в дне начала.
    /// IsMuted — выключенная запись, чьё срабатывание в этот день ещё впереди (уже сработавшая разовая не приглушается).
    /// </summary>
    public static IReadOnlyList<DayEntry> For(IEnumerable<Alarm> alarms, IReadOnlyDictionary<Guid, HashSet<DateOnly>> done, DateOnly day, DateTime now)
    {
        return alarms
            .Where(a => a.OccursOn(day))
            .Select(a =>
            {
                var start = day.ToDateTime(TimeOnly.FromDateTime(a.At));
                var end = start.AddMinutes(a.DurationMinutes);
                return new DayEntry(
                    a,
                    start,
                    end,
                    IsCurrent: a.DurationMinutes > 0 && start <= now && now < end,
                    IsDone: done.TryGetValue(a.Id, out var days) && days.Contains(day),
                    IsMuted: !a.IsEnabled && start > now);
            })
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Alarm.Label, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Семь дней недели, содержащей day: с понедельника по воскресенье.</summary>
    public static IReadOnlyList<DateOnly> Week(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
    }

    /// <summary>
    /// Сетка месяца, содержащего day: целые недели с понедельника — от недели с 1-м числом до недели с последним (4–6 недель).
    /// Дни соседних месяцев в первой и последней неделе входят в сетку.
    /// </summary>
    public static IReadOnlyList<DateOnly> Month(DateOnly day)
    {
        var first = Week(new DateOnly(day.Year, day.Month, 1))[0];
        var last = Week(new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)))[6];
        return Enumerable.Range(0, last.DayNumber - first.DayNumber + 1).Select(first.AddDays).ToList();
    }
}
