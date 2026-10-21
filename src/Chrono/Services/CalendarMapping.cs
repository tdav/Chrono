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

    /// <summary>Заголовок события для записи без подписи; при приёме эха обратно в подпись не попадает.</summary>
    public const string DefaultSummary = "Будильник";

    /// <summary>Коды дней в RRULE, с понедельника; индекс i соответствует (DayOfWeek)((i + 1) % 7).</summary>
    private static readonly string[] DayCodes = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"];

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>Событие для отправки в Google. Смещение пояса берётся на дату самого события, не на сегодня.</summary>
    public static GoogleEvent ToEvent(Alarm alarm, TimeZoneInfo zone)
    {
        // Разовая начинается в день At; у повтора At — ближайшее срабатывание, а первое повторение — StartsOn.
        var startDay = alarm.IsOneShot() ? DateOnly.FromDateTime(alarm.At) : alarm.StartsOn ?? DateOnly.FromDateTime(alarm.At);
        if (alarm.Repeat == RepeatKind.Yearly)
        {
            // Месяц и день берутся из At, год — первый, начиная с года StartsOn, где эта дата не раньше StartsOn
            // (29 февраля — ближайший високосный год, как в NextAfter). StartsOn после миграции может не совпадать с датой At.
            var floor = startDay;
            for (var year = floor.Year; ; year++)
            {
                if (alarm.At.Day <= DateTime.DaysInMonth(year, alarm.At.Month) && new DateOnly(year, alarm.At.Month, alarm.At.Day) is var candidate && candidate >= floor)
                {
                    startDay = candidate;
                    break;
                }
            }
        }

        if (alarm.Repeat == RepeatKind.Monthly)
        {
            // Число месяца берётся из At, первый месяц — не раньше StartsOn; месяцы без такого числа пропускаются, как в NextAfter.
            var floor = startDay;
            for (var month = new DateOnly(floor.Year, floor.Month, 1); ; month = month.AddMonths(1))
            {
                if (alarm.At.Day <= DateTime.DaysInMonth(month.Year, month.Month) && new DateOnly(month.Year, month.Month, alarm.At.Day) is var candidate && candidate >= floor)
                {
                    startDay = candidate;
                    break;
                }
            }
        }

        // Только семь определённых битов: маска из одних лишних битов (повреждённый файл) иначе не нашла бы дня никогда.
        var mask = alarm.Days & (WeekDays)127;
        if (alarm.Repeat == RepeatKind.Weekly && mask != WeekDays.None)
        {
            // DTSTART должен быть первым днём из маски не раньше startDay: Google считает DTSTART первым событием серии, даже если день не отмечен.
            while (!mask.HasFlag((WeekDays)(1 << (int)startDay.DayOfWeek)))
            {
                startDay = startDay.AddDays(1);
            }
        }

        var start = startDay.ToDateTime(TimeOnly.FromDateTime(alarm.At));
        var end = start.AddMinutes(alarm.DurationMinutes);

        string[]? recurrence = alarm.Repeat switch
        {
            RepeatKind.Daily => ["RRULE:FREQ=DAILY"],
            RepeatKind.Yearly => ["RRULE:FREQ=YEARLY"],
            RepeatKind.Monthly => ["RRULE:FREQ=MONTHLY"],
            RepeatKind.Weekly when alarm.Days == WeekDays.None => ["RRULE:FREQ=DAILY"],
            RepeatKind.Weekly => [$"RRULE:FREQ=WEEKLY;BYDAY={string.Join(',', DayCodes.Where((_, i) => alarm.Days.HasFlag((WeekDays)(1 << ((i + 1) % 7)))))}"],
            _ => null,
        };

        if (recurrence is not null && alarm.EndsOn is { } endsOn)
        {
            // Последний день включительно: UNTIL — конец местного дня в UTC (в Google UNTIL с временем включает само это мгновение).
            var endOfDay = endsOn.ToDateTime(new TimeOnly(23, 59, 59));
            recurrence[0] += $";UNTIL={new DateTimeOffset(endOfDay, zone.GetUtcOffset(endOfDay)).UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}";
        }

        return new GoogleEvent
        {
            Id = alarm.GoogleEventId,
            Summary = alarm.Label.Length == 0 ? DefaultSummary : alarm.Label,
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

        // Разбор RRULE. Поддерживаются DAILY, WEEKLY (+BYDAY из двух букв), MONTHLY (BYMONTHDAY — только равный числу старта) и YEARLY
        // с UNTIL (дата окончания), без INTERVAL≠1, COUNT и прочего; остальное — «чужая серия». Отдельного метода нет: разбор нужен только здесь.
        var rules = (ev.Recurrence ?? []).Where(r => r.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)).ToList();
        var repeat = RepeatKind.None;
        var days = WeekDays.None;
        DateOnly? endsOn = null;
        // RDATE и EXRULE Chrono выразить не может (их повторы молча потерялись бы); EXDATE игнорируется (6.6).
        var supported = rules.Count <= 1
            && !(ev.Recurrence ?? []).Any(r => r.StartsWith("RDATE", StringComparison.OrdinalIgnoreCase) || r.StartsWith("EXRULE", StringComparison.OrdinalIgnoreCase));
        if (rules.Count == 1)
        {
            string? freq = null;
            string? byDay = null;
            string? byMonthDay = null;
            DateOnly? until = null;
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
                    case "BYMONTHDAY":
                        byMonthDay = value;
                        break;
                    case "UNTIL":
                        // UTC — переводим в местную дату; дата без времени и время без Z — уже местные.
                        if (value.EndsWith('Z') && DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var untilUtc))
                        {
                            until = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(untilUtc, zone));
                        }
                        else if (!value.EndsWith('Z') && DateTime.TryParseExact(value, ["yyyyMMdd", "yyyyMMdd'T'HHmmss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var untilLocal))
                        {
                            until = DateOnly.FromDateTime(untilLocal);
                        }
                        else
                        {
                            supported = false;
                        }

                        break;
                    case "INTERVAL" when value == "1":
                    case "WKST":
                        break;
                    default:
                        supported = false;
                        break;
                }
            }

            // BYMONTHDAY понятен только у MONTHLY и только если равен числу старта (иначе число месяца у Chrono другое).
            if (byMonthDay is not null && (freq != "MONTHLY" || byMonthDay != start.Day.ToString(CultureInfo.InvariantCulture)))
            {
                supported = false;
            }

            endsOn = until;
            switch (freq)
            {
                case "MONTHLY" when byDay is null:
                    repeat = RepeatKind.Monthly;
                    break;
                case "DAILY" when byDay is null:
                    repeat = RepeatKind.Daily;
                    break;
                case "YEARLY" when byDay is null:
                    repeat = RepeatKind.Yearly;
                    break;
                case "WEEKLY":
                    repeat = RepeatKind.Weekly;
                    // Без BYDAY Google повторяет в день недели начала; Days = None в Chrono означало бы «каждый день».
                    days = byDay is null ? (WeekDays)(1 << (int)start.DayOfWeek) : WeekDays.None;
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
            endsOn = null;
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

        // ToEvent сдвигает DTSTART Weekly на первый день маски; если он совпал с событием, StartsOn записи (он мог быть днём вне маски) не трогаем.
        var startsOn = DateOnly.FromDateTime(start);
        if (repeat == RepeatKind.Weekly && days != WeekDays.None && existing?.StartsOn is { } kept)
        {
            var first = kept;
            while (!days.HasFlag((WeekDays)(1 << (int)first.DayOfWeek)))
            {
                first = first.AddDays(1);
            }

            startsOn = first == startsOn ? kept : startsOn;
        }

        var alarm = new Alarm
        {
            Id = Guid.TryParse(props?.GetValueOrDefault(ChronoIdKey), out var chronoId) ? chronoId : existing?.Id ?? Guid.NewGuid(),
            At = start,
            // Эхо нашей отправки: подстановка для пустой подписи не должна стать подписью.
            Label = ev.Summary == DefaultSummary && existing is { Label.Length: 0 } ? "" : ev.Summary ?? "",
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
            StartsOn = startsOn,
            EndsOn = endsOn,
            GoogleEventId = ev.Id,
            // useDefault считается «без напоминаний»: у календаря «Chrono» нет напоминаний по умолчанию; email-напоминания — не звонок.
            ReminderMinutes = ev.Reminders is { UseDefault: false, Overrides: { } overrides } ? string.Join(',', overrides.Where(o => o.Method == "popup").Select(o => o.Minutes)) : "",
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
