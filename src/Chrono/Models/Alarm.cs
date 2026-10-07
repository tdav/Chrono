namespace Chrono.Models;

public enum RepeatKind
{
    None,
    Daily,
    Weekly,
    Yearly,
}

/// <summary>Набор дней недели; бит N соответствует (DayOfWeek)N.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Sunday = 1 << DayOfWeek.Sunday,
    Monday = 1 << DayOfWeek.Monday,
    Tuesday = 1 << DayOfWeek.Tuesday,
    Wednesday = 1 << DayOfWeek.Wednesday,
    Thursday = 1 << DayOfWeek.Thursday,
    Friday = 1 << DayOfWeek.Friday,
    Saturday = 1 << DayOfWeek.Saturday,
}

/// <summary>Будильник: разовый или повторяющийся. У повторяющегося At — ближайшее срабатывание.</summary>
public sealed record Alarm
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Локальное «настенное» время срабатывания (Kind = Unspecified или Local).</summary>
    public DateTime At { get; init; }

    public string Label { get; init; } = "";

    public bool SoundEnabled { get; init; } = true;

    public string SoundId { get; init; } = Sounds.Default;

    public bool VibrationEnabled { get; init; } = true;

    public bool IsEnabled { get; init; } = true;

    public RepeatKind Repeat { get; init; } = RepeatKind.None;

    /// <summary>Дни недели для RepeatKind.Weekly; None считается «каждый день».</summary>
    public WeekDays Days { get; init; } = WeekDays.None;

    /// <summary>
    /// Первое срабатывание по правилу повтора строго позже moment: время суток берётся из At,
    /// для Yearly — ещё день и месяц (29 февраля — только в високосные годы).
    /// </summary>
    public DateTime NextAfter(DateTime moment)
    {
        var time = TimeOnly.FromDateTime(this.At);
        if (this.Repeat == RepeatKind.Yearly)
        {
            for (var year = moment.Year; ; year++)
            {
                if (this.At.Day <= DateTime.DaysInMonth(year, this.At.Month)
                    && new DateTime(new DateOnly(year, this.At.Month, this.At.Day), time) is var candidate
                    && candidate > moment)
                {
                    return candidate;
                }
            }
        }

        // Завершается не позже чем через 7 дней: каждый день недели встречается в любой неделе.
        for (var day = DateOnly.FromDateTime(moment); ; day = day.AddDays(1))
        {
            var candidate = new DateTime(day, time);
            if (candidate > moment
                && (this.Repeat != RepeatKind.Weekly || this.Days == WeekDays.None || this.Days.HasFlag((WeekDays)(1 << (int)candidate.DayOfWeek))))
            {
                return candidate;
            }
        }
    }
}
