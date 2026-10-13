namespace Chrono.Services;

/// <summary>Ячейка календаря; Date == null — пустая ячейка перед первым числом (неделя с понедельника).</summary>
public readonly record struct CalendarCell(DateOnly? Date, bool IsPast, bool IsToday, bool IsWeekend, bool IsEnabled);

/// <summary>Расчёты экрана будильника без зависимостей от MAUI: «Ближайшее», обратный отсчёт, сетка месяца.</summary>
public static class AlarmSchedule
{
    /// <summary>Ближайший момент с этим временем суток: сегодня, если он строго позже now, иначе завтра.</summary>
    public static DateTime ResolveNearest(TimeOnly time, DateTime now)
    {
        var today = DateOnly.FromDateTime(now).ToDateTime(time);
        return today > now ? today : today.AddDays(1);
    }

    /// <summary>
    /// Сколько осталось до target, с округлением минут вверх: за 20 секунд до срока — «1 мин», а не «0 мин».
    /// null, если target уже наступил.
    /// </summary>
    public static (int Days, int Hours, int Minutes)? Until(DateTime target, DateTime now)
    {
        if (target <= now)
        {
            return null;
        }

        var total = (int)Math.Ceiling((target - now).TotalMinutes);
        return (total / 1440, total % 1440 / 60, total % 60);
    }

    /// <summary>Сетка месяца с понедельника: пустые ячейки до 1-го числа, затем все дни месяца.</summary>
    public static IReadOnlyList<CalendarCell> Month(int year, int month, DateOnly today, bool allowPast)
    {
        var first = new DateOnly(year, month, 1);
        var lead = ((int)first.DayOfWeek + 6) % 7;
        var cells = new List<CalendarCell>(lead + 31);
        for (var i = 0; i < lead; i++)
        {
            cells.Add(new CalendarCell(null, false, false, false, false));
        }

        for (var day = first; day.Month == month; day = day.AddDays(1))
        {
            var past = day < today;
            cells.Add(new CalendarCell(
                day,
                past,
                day == today,
                day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                allowPast || !past));
        }

        return cells;
    }
}
