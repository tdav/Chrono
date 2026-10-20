using System.Globalization;
using Chrono.Models;

namespace Chrono.Services;

/// <summary>Результат разбора события Google.</summary>
public abstract record ImportResult;

/// <summary>Создать или обновить запись.</summary>
public sealed record ImportUpsert(Alarm Alarm) : ImportResult;

/// <summary>Удалить запись (событие отменено в Google).</summary>
public sealed record ImportDelete(Guid AlarmId) : ImportResult;

/// <summary>Событие в Chrono не загружается: прошедшее, повреждённое, отменённое и неизвестное.</summary>
public sealed record ImportSkip(string Reason) : ImportResult;

/// <summary>Повтор, которого Chrono не умеет: нужно ближайшее повторение (events.instances), потом второй вызов FromEvent.</summary>
public sealed record ImportNeedsInstance(string EventId) : ImportResult;

/// <summary>Перевод записи в событие Google и обратно (спецификация 6.2). Без сети и без MAUI.</summary>
public static class CalendarMapping
{
    public const string ChronoIdKey = "chronoId";

    /// <summary>Коды дней в RRULE, с понедельника; индекс i соответствует (DayOfWeek)((i + 1) % 7).</summary>
    private static readonly string[] DayCodes = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"];

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>Событие для отправки в Google. Смещение пояса берётся на дату самого события, не на сегодня.</summary>
    public static GoogleEvent ToEvent(Alarm alarm, TimeZoneInfo zone)
    {
        // Разовая начинается в день At; у повтора At — ближайшее срабатывание, а первое повторение — StartsOn.
        var startDay = alarm.IsOneShot() ? DateOnly.FromDateTime(alarm.At) : alarm.StartsOn ?? DateOnly.FromDateTime(alarm.At);
        var start = startDay.ToDateTime(TimeOnly.FromDateTime(alarm.At));
        var end = start.AddMinutes(alarm.DurationMinutes);

        string[]? recurrence = alarm.Repeat switch
        {
            RepeatKind.Daily => ["RRULE:FREQ=DAILY"],
            RepeatKind.Yearly => ["RRULE:FREQ=YEARLY"],
            RepeatKind.Weekly when alarm.Days == WeekDays.None => ["RRULE:FREQ=DAILY"],
            RepeatKind.Weekly => [$"RRULE:FREQ=WEEKLY;BYDAY={string.Join(',', DayCodes.Where((_, i) => alarm.Days.HasFlag((WeekDays)(1 << ((i + 1) % 7)))))}"],
            _ => null,
        };

        return new GoogleEvent
        {
            Id = alarm.GoogleEventId,
            Summary = alarm.Label.Length == 0 ? "Будильник" : alarm.Label,
            Start = new GoogleEventTime
            {
                DateTime = new DateTimeOffset(start, zone.GetUtcOffset(start)).ToString(TimeFormat, CultureInfo.InvariantCulture),
                TimeZone = zone.Id,
            },
            End = new GoogleEventTime
            {
                DateTime = new DateTimeOffset(end, zone.GetUtcOffset(end)).ToString(TimeFormat, CultureInfo.InvariantCulture),
                TimeZone = zone.Id,
            },
            Recurrence = recurrence,
            // Google допускает не более 5 напоминаний.
            Reminders = new GoogleReminders
            {
                UseDefault = false,
                Overrides = alarm.Reminders().Take(5).Select(m => new GoogleReminder("popup", m)).ToArray(),
            },
            ExtendedProperties = new GoogleExtendedProperties
            {
                Private = new Dictionary<string, string>
                {
                    [ChronoIdKey] = alarm.Id.ToString(),
                    ["alert"] = alarm.Alert.ToString(),
                    ["icon"] = alarm.Icon,
                    ["soundId"] = alarm.SoundId,
                    ["soundEnabled"] = alarm.SoundEnabled ? "true" : "false",
                    ["vibration"] = alarm.VibrationEnabled ? "true" : "false",
                    ["enabled"] = alarm.IsEnabled ? "true" : "false",
                    ["repeat"] = alarm.Repeat.ToString(),
                },
            },
        };
    }

    /// <summary>
    /// Запись по событию Google. existing — запись с тем же GoogleEventId или chronoId, если есть.
    /// instanceStart — начало ближайшего повторения для второго прохода после ImportNeedsInstance.
    /// </summary>
    public static ImportResult FromEvent(GoogleEvent ev, TimeZoneInfo zone, DateTime now, Alarm? existing, DateTimeOffset? instanceStart = null)
    {
        if (ev.Status == "cancelled")
        {
            return existing is null ? new ImportSkip("cancelled") : new ImportDelete(existing.Id);
        }

        // Повреждённое событие без начала не должно ронять синхронизацию.
        if (ev.Start is null || (ev.Start.DateTime is null && ev.Start.Date is null))
        {
            return new ImportSkip("no start");
        }

        var allDay = ev.Start.DateTime is null;
        DateTimeOffset? startOffset = null;
        DateTime start;
        if (allDay)
        {
            if (!DateOnly.TryParseExact(ev.Start.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return new ImportSkip("bad start");
            }

            start = day.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            if (!DateTimeOffset.TryParse(ev.Start.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return new ImportSkip("bad start");
            }

            startOffset = parsed;
            start = TimeZoneInfo.ConvertTime(parsed, zone).DateTime;
        }

        // Разбор RRULE. Поддерживаются DAILY, WEEKLY (+BYDAY из двух букв) и YEARLY без INTERVAL≠1, COUNT, UNTIL и прочего;
        // остальное — «чужая серия». Отдельного метода нет: разбор нужен только здесь.
        var rules = (ev.Recurrence ?? []).Where(r => r.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)).ToList();
        var repeat = RepeatKind.None;
        var days = WeekDays.None;
        var supported = rules.Count <= 1;
        if (rules.Count == 1)
        {
            string? freq = null;
            string? byDay = null;
            foreach (var part in rules[0][6..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var pair = part.Split('=', 2);
                var value = pair.Length == 2 ? pair[1].ToUpperInvariant() : "";
                switch (pair[0].ToUpperInvariant())
                {
                    case "FREQ":
                        freq = value;
                        break;
                    case "BYDAY":
                        byDay = value;
                        break;
                    case "INTERVAL" when value == "1":
                    case "WKST":
                        break;
                    default:
                        supported = false;
                        break;
                }
            }

            switch (freq)
            {
                case "DAILY" when byDay is null:
                    repeat = RepeatKind.Daily;
                    break;
                case "YEARLY" when byDay is null:
                    repeat = RepeatKind.Yearly;
                    break;
                case "WEEKLY":
                    repeat = RepeatKind.Weekly;
                    foreach (var code in (byDay ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var index = Array.IndexOf(DayCodes, code);
                        if (index < 0)
                        {
                            supported = false;
                        }
                        else
                        {
                            days |= (WeekDays)(1 << ((index + 1) % 7));
                        }
                    }

                    break;
                default:
                    supported = false;
                    break;
            }
        }

        var foreign = false;
        if (!supported)
        {
            if (instanceStart is not { } instance)
            {
                return new ImportNeedsInstance(ev.Id ?? "");
            }

            // Чужая серия: разовая запись на ближайшее повторение; её правки в Google не уходят.
            foreign = true;
            repeat = RepeatKind.None;
            days = WeekDays.None;
            start = allDay ? TimeZoneInfo.ConvertTime(instance, zone).Date : TimeZoneInfo.ConvertTime(instance, zone).DateTime;
        }

        var props = ev.ExtendedProperties?.Private;
        if (repeat == RepeatKind.None && !foreign && props?.GetValueOrDefault("repeat") == nameof(RepeatKind.OnceThenDelete))
        {
            repeat = RepeatKind.OnceThenDelete;
        }

        var duration = !allDay && startOffset is { } startAt && DateTimeOffset.TryParse(ev.End?.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endAt)
            ? Math.Max(0, (int)(endAt - startAt).TotalMinutes)
            : 0;

        var alarm = new Alarm
        {
            Id = Guid.TryParse(props?.GetValueOrDefault(ChronoIdKey), out var chronoId) ? chronoId : existing?.Id ?? Guid.NewGuid(),
            At = start,
            Label = ev.Summary ?? "",
            SoundEnabled = props?.GetValueOrDefault("soundEnabled") != "false",
            SoundId = props?.GetValueOrDefault("soundId") ?? Sounds.Default,
            VibrationEnabled = props?.GetValueOrDefault("vibration") != "false",
            IsEnabled = props?.GetValueOrDefault("enabled") != "false",
            Repeat = repeat,
            Days = days,
            DurationMinutes = duration,
            Icon = props?.GetValueOrDefault("icon") ?? "bell",
            // Событие, созданное в Google (без свойств Chrono), звонит как Ring; на весь день — без оповещения.
            Alert = allDay ? AlertKind.None
                : Enum.TryParse<AlertKind>(props?.GetValueOrDefault("alert"), out var alert) && Enum.IsDefined(alert) ? alert : AlertKind.Ring,
            StartsOn = DateOnly.FromDateTime(start),
            GoogleEventId = ev.Id,
            // useDefault считается «без напоминаний»: у календаря «Chrono» нет напоминаний по умолчанию.
            ReminderMinutes = ev.Reminders is { UseDefault: false, Overrides: { } overrides } ? string.Join(',', overrides.Select(o => o.Minutes)) : "",
            ForeignSeries = foreign,
        };

        if (alarm.IsOneShot())
        {
            // Прошедшее и идущее прямо сейчас (начало позади) в Chrono не загружаются: Save отклоняет At <= now.
            return start <= now ? new ImportSkip("past") : new ImportUpsert(alarm);
        }

        return new ImportUpsert(alarm with { At = alarm.NextAfter(now) });
    }
}
