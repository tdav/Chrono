namespace Chrono.Services;

/// <summary>Пользователь снял галочку с обязательной области в окне согласия Google: синхронизация невозможна.</summary>
public sealed class SyncScopesMissingException : Exception
{
}

/// <summary>Области Google Calendar, без которых синхронизация не работает.</summary>
public static class GoogleScopes
{
    public const string CalendarAppCreated = "https://www.googleapis.com/auth/calendar.app.created";

    public const string CalendarListReadonly = "https://www.googleapis.com/auth/calendar.calendarlist.readonly";

    /// <summary>
    /// Окно согласия позволяет снять галочку с отдельной области, и токен тогда выдаётся без неё.
    /// granted — области из ответа Google (Android: AuthorizationResult.GrantedScopes; iOS: поле scope, разделённое пробелами).
    /// </summary>
    public static void RequireCalendar(IEnumerable<string>? granted)
    {
        var set = granted?.ToHashSet(StringComparer.Ordinal) ?? [];
        if (!set.Contains(CalendarAppCreated) || !set.Contains(CalendarListReadonly))
        {
            throw new SyncScopesMissingException();
        }
    }
}
