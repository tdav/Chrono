namespace Chrono.Services;

/// <summary>Настройки экрана «Распорядок».</summary>
public static class ScheduleSettings
{
    private const string MonthCalendarKey = "schedule_month_calendar";

    /// <summary>Календарь над лентой дня: весь месяц (по умолчанию, решение пользователя) или одна неделя.</summary>
    public static bool MonthCalendar
    {
        get => Preferences.Default.Get(MonthCalendarKey, true);
        set => Preferences.Default.Set(MonthCalendarKey, value);
    }
}
