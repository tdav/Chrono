using System.Text.Json.Serialization;

namespace Chrono.Services;

// DTO ресурса Events Google Calendar API v3. Имена полей JSON заданы явно: без [JsonPropertyName]
// System.Text.Json молча не прочитает, например, "dateTime". Пустые поля не пишутся (date и dateTime взаимоисключающие).

/// <summary>Событие календаря Google.</summary>
public sealed record GoogleEvent
{
    [JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>confirmed, tentative или cancelled.</summary>
    [JsonPropertyName("status"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    [JsonPropertyName("summary"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("start"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleEventTime? Start { get; init; }

    [JsonPropertyName("end"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleEventTime? End { get; init; }

    /// <summary>Строки RRULE/EXRULE/RDATE/EXDATE.</summary>
    [JsonPropertyName("recurrence"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Recurrence { get; init; }

    [JsonPropertyName("reminders"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleReminders? Reminders { get; init; }

    [JsonPropertyName("extendedProperties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleExtendedProperties? ExtendedProperties { get; init; }

    /// <summary>Время последнего изменения; только для чтения.</summary>
    [JsonPropertyName("updated"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Updated { get; init; }
}

/// <summary>Начало или конец события: date — на весь день, dateTime — со временем.</summary>
public sealed record GoogleEventTime
{
    [JsonPropertyName("date"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Date { get; init; }

    [JsonPropertyName("dateTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DateTime { get; init; }

    [JsonPropertyName("timeZone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeZone { get; init; }
}

public sealed record GoogleReminders
{
    [JsonPropertyName("useDefault")]
    public bool UseDefault { get; init; }

    [JsonPropertyName("overrides"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleReminder[]? Overrides { get; init; }
}

/// <summary>Напоминание события: method — email или popup, minutes — за сколько минут до начала.</summary>
public sealed record GoogleReminder(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("minutes")] int Minutes);

public sealed record GoogleExtendedProperties
{
    [JsonPropertyName("private"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Private { get; init; }
}
