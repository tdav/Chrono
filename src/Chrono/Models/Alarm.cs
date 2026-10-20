namespace Chrono.Models;

public enum RepeatKind
{
    None,
    Daily,
    Weekly,
    Yearly,

    /// <summary>Разовая запись, которая после срабатывания и окончания длительности удаляется. Добавлено последним: enum хранится числом.</summary>
    OnceThenDelete,
}

/// <summary>Уровень оповещения записи.</summary>
public enum AlertKind
{
    None,
    Notify,
    Ring,
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

    /// <summary>Длительность дела в минутах; 0 — без длительности.</summary>
    public int DurationMinutes { get; init; }

    /// <summary>Ключ иконки из ItemIcons.Keys.</summary>
    public string Icon { get; init; } = "bell";

    /// <summary>Уровень оповещения; в старых файлах поля нет — тогда Ring, как звонили раньше.</summary>
    public AlertKind Alert { get; init; } = AlertKind.Ring;

    /// <summary>Первый день действия записи; null только у ещё не промигрированных старых записей.</summary>
    public DateOnly? StartsOn { get; init; }

    /// <summary>Сколько звонок звонит до автоматической остановки (единый источник).</summary>
    public static readonly TimeSpan RingTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Разовая запись: без повтора или «Один раз и удалить».</summary>
    public bool IsOneShot() => this.Repeat is RepeatKind.None or RepeatKind.OnceThenDelete;

    /// <summary>Конец срабатывания: At плюс бо́льшее из длительности и (для Ring) времени звонка.</summary>
    public DateTime ExpiresAt()
    {
        var duration = TimeSpan.FromMinutes(this.DurationMinutes);
        var ring = this.Alert == AlertKind.Ring ? RingTimeout : TimeSpan.Zero;
        return this.At + (duration > ring ? duration : ring);
    }

    /// <summary>Приходится ли срабатывание записи на указанный день.</summary>
    public bool OccursOn(DateOnly day)
    {
        if (this.StartsOn is { } start && day < start)
        {
            return false;
        }

        var at = DateOnly.FromDateTime(this.At);
        return this.Repeat switch
        {
            RepeatKind.Daily => true,
            RepeatKind.Weekly => this.Days == WeekDays.None || this.Days.HasFlag((WeekDays)(1 << (int)day.DayOfWeek)),
            RepeatKind.Yearly => day.Month == at.Month && day.Day == at.Day,
            _ => day == at,
        };
    }

    /// <summary>
    /// Первое срабатывание по правилу повтора строго позже moment: время суток берётся из At,
    /// для Yearly — ещё день и месяц (29 февраля — только в високосные годы).
    /// </summary>
    public DateTime NextAfter(DateTime moment)
    {
        // Запись, начинающаяся в будущем, не срабатывает раньше своего первого дня.
        if (this.StartsOn is { } start && start.ToDateTime(TimeOnly.MinValue).AddTicks(-1) is var beforeStart && beforeStart > moment)
        {
            moment = beforeStart;
        }

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
