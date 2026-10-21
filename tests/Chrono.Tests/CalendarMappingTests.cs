using System.Text.Json;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class CalendarMappingTests
{
    // Пояс телефона — без летнего времени (+05:00); пояс с переходом — Берлин.
    private static readonly TimeZoneInfo Tashkent = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0);

    private static GoogleEvent Timed(string start, string? end = null, string[]? recurrence = null, Dictionary<string, string>? props = null) => new()
    {
        Id = "evt1",
        Summary = "Зарядка",
        Start = new GoogleEventTime { DateTime = start },
        End = new GoogleEventTime { DateTime = end ?? start },
        Recurrence = recurrence,
        ExtendedProperties = props is null ? null : new GoogleExtendedProperties { Private = props },
    };

    [Fact]
    public void Deserialize_GoogleDocumentationEvent_ReadsAllFields()
    {
        // Событие в формате ресурса Events (Calendar API v3), с полями, которых в DTO нет.
        const string json = """
            {
              "kind": "calendar#event",
              "etag": "\"3181161784712000\"",
              "id": "abc123def456",
              "status": "confirmed",
              "htmlLink": "https://www.google.com/calendar/event?eid=abc",
              "created": "2026-10-18T09:00:00.000Z",
              "updated": "2026-10-19T08:30:15.356Z",
              "summary": "Зарядка",
              "creator": { "email": "me@example.com" },
              "start": { "dateTime": "2026-10-21T10:00:00+03:00", "timeZone": "Europe/Moscow" },
              "end": { "dateTime": "2026-10-21T10:30:00+03:00", "timeZone": "Europe/Moscow" },
              "recurrence": [ "RRULE:FREQ=WEEKLY;BYDAY=MO,WE" ],
              "reminders": { "useDefault": false, "overrides": [ { "method": "popup", "minutes": 30 }, { "method": "email", "minutes": 60 } ] },
              "extendedProperties": { "private": { "chronoId": "6f1d2c3a-0000-4000-8000-000000000001", "alert": "Ring" } },
              "allDay": { "date": "2026-10-22" }
            }
            """;

        var ev = JsonSerializer.Deserialize<GoogleEvent>(json)!;

        Assert.Equal("abc123def456", ev.Id);
        Assert.Equal("confirmed", ev.Status);
        Assert.Equal("Зарядка", ev.Summary);
        Assert.Equal("2026-10-21T10:00:00+03:00", ev.Start!.DateTime);
        Assert.Equal("Europe/Moscow", ev.Start.TimeZone);
        Assert.Null(ev.Start.Date);
        Assert.Equal("2026-10-21T10:30:00+03:00", ev.End!.DateTime);
        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=MO,WE"], ev.Recurrence!);
        Assert.False(ev.Reminders!.UseDefault);
        Assert.Equal([new GoogleReminder("popup", 30), new GoogleReminder("email", 60)], ev.Reminders.Overrides!);
        Assert.Equal("6f1d2c3a-0000-4000-8000-000000000001", ev.ExtendedProperties!.Private!["chronoId"]);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 8, 30, 15, 356, TimeSpan.Zero), ev.Updated);
    }

    [Fact]
    public void Deserialize_AllDayEvent_ReadsDate()
    {
        var ev = JsonSerializer.Deserialize<GoogleEvent>("""{"id":"x","start":{"date":"2026-10-22"},"end":{"date":"2026-10-23"}}""")!;

        Assert.Equal("2026-10-22", ev.Start!.Date);
        Assert.Null(ev.Start.DateTime);
    }

    [Fact]
    public void Serialize_ToEvent_UsesApiFieldNamesAndOmitsNulls()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), StartsOn = new DateOnly(2026, 10, 26), Repeat = RepeatKind.Daily };

        var json = JsonSerializer.Serialize(CalendarMapping.ToEvent(alarm, Tashkent));

        // Читаем как сырой JSON: так видны настоящие имена полей (а "+" в строке STJ экранирует).
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2026-10-26T07:15:00+05:00", root.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("Asia/Tashkent", root.GetProperty("start").GetProperty("timeZone").GetString());
        Assert.False(root.GetProperty("start").TryGetProperty("date", out _));
        Assert.Equal("RRULE:FREQ=DAILY", root.GetProperty("recurrence")[0].GetString());
        Assert.False(root.GetProperty("reminders").GetProperty("useDefault").GetBoolean());
        Assert.False(root.TryGetProperty("id", out _));
        Assert.False(root.TryGetProperty("status", out _));
        Assert.DoesNotContain("null", json);
    }

    [Fact]
    public void ToEvent_Daily_RruleAndStartFromStartsOn()
    {
        var alarm = new Alarm
        {
            At = new DateTime(2026, 10, 20, 7, 15, 0),
            StartsOn = new DateOnly(2026, 10, 26),
            Repeat = RepeatKind.Daily,
            DurationMinutes = 30,
            Label = "Зарядка",
        };

        var ev = CalendarMapping.ToEvent(alarm, Tashkent);

        Assert.Equal("2026-10-26T07:15:00+05:00", ev.Start!.DateTime);
        Assert.Equal("2026-10-26T07:45:00+05:00", ev.End!.DateTime);
        Assert.Equal("Asia/Tashkent", ev.Start.TimeZone);
        Assert.Equal(["RRULE:FREQ=DAILY"], ev.Recurrence!);
        Assert.Equal("Зарядка", ev.Summary);
    }

    [Fact]
    public void ToEvent_ZeroDuration_EndEqualsStart()
    {
        var ev = CalendarMapping.ToEvent(new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0) }, Tashkent);

        Assert.Equal(ev.Start!.DateTime, ev.End!.DateTime);
    }

    [Fact]
    public void ToEvent_EmptyLabel_UsesAlarmWord()
    {
        var ev = CalendarMapping.ToEvent(new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0) }, Tashkent);

        Assert.Equal("Будильник", ev.Summary);
    }

    [Fact]
    public void FromEvent_EchoOfDefaultSummary_KeepsEmptyLabel()
    {
        // Пустая подпись уходит в Google как «Будильник»; при приёме эха она не должна туда записаться.
        var existing = new Alarm { Id = Guid.NewGuid(), At = new DateTime(2026, 10, 26, 7, 15, 0), GoogleEventId = "evt1" };
        var ev = CalendarMapping.ToEvent(existing, Tashkent) with { Id = "evt1" };

        var echo = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, existing));
        Assert.Equal("", echo.Alarm.Label);

        // Запись с непустой подписью или новая запись принимают summary как есть.
        Assert.Equal("Будильник", Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, existing with { Label = "Зал" })).Alarm.Label);
        Assert.Equal("Будильник", Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm.Label);
    }

    [Fact]
    public void FromEvent_WeeklyEcho_KeepsStartsOnWhileFirstMaskDayMatchesStart()
    {
        // StartsOn — вторник, маска Ср+Пт: ToEvent кладёт DTSTART на среду, но StartsOn записи не должен уехать на среду.
        var existing = new Alarm
        {
            Id = Guid.NewGuid(), At = new DateTime(2026, 10, 28, 7, 15, 0), StartsOn = new DateOnly(2026, 10, 27),
            Repeat = RepeatKind.Weekly, Days = WeekDays.Wednesday | WeekDays.Friday, GoogleEventId = "evt1",
        };
        var ev = CalendarMapping.ToEvent(existing, Tashkent) with { Id = "evt1" };

        Assert.Equal(new DateOnly(2026, 10, 27), Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, existing)).Alarm.StartsOn);

        // Событие изменили в Google: его начало теперь не совпадает с первым днём маски от старого StartsOn, StartsOn берётся из события.
        var moved = existing with { StartsOn = new DateOnly(2026, 10, 29) };
        Assert.Equal(new DateOnly(2026, 10, 28), Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, moved)).Alarm.StartsOn);
    }

    [Fact]
    public void ToEvent_WeeklyMonWed_ByDay()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = WeekDays.Wednesday | WeekDays.Monday };

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=MO,WE"], CalendarMapping.ToEvent(alarm, Tashkent).Recurrence!);
    }

    [Fact]
    public void ToEvent_Weekly_StartIsFirstMatchingDayNotEarlierThanStartsOn()
    {
        // 2026-10-26 — понедельник, а запись только по средам и пятницам: DTSTART — среда 28-го, иначе Google покажет лишнее событие в понедельник.
        var alarm = new Alarm
        {
            At = new DateTime(2026, 10, 26, 7, 15, 0), StartsOn = new DateOnly(2026, 10, 26), DurationMinutes = 30,
            Repeat = RepeatKind.Weekly, Days = WeekDays.Friday | WeekDays.Wednesday,
        };

        var ev = CalendarMapping.ToEvent(alarm, Tashkent);

        Assert.Equal("2026-10-28T07:15:00+05:00", ev.Start!.DateTime);
        Assert.Equal("2026-10-28T07:45:00+05:00", ev.End!.DateTime);
    }

    [Fact]
    public void ToEvent_WeeklyMaskOfUndefinedBitsOnly_DoesNotLoopAndKeepsStartDay()
    {
        // Повреждённый alarms.json: в маске нет ни одного из семи дней.
        var alarm = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = (WeekDays)128 };

        var ev = CalendarMapping.ToEvent(alarm, Tashkent);

        Assert.Equal("2026-10-26T07:15:00+05:00", ev.Start!.DateTime);
    }

    [Fact]
    public void ToEvent_WeeklySunday_ByDayOrderedFromMonday()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 25, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = WeekDays.Sunday | WeekDays.Saturday | WeekDays.Tuesday };

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=TU,SA,SU"], CalendarMapping.ToEvent(alarm, Tashkent).Recurrence!);
    }

    [Fact]
    public void ToEvent_WeeklyNoDays_Daily()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = WeekDays.None };

        Assert.Equal(["RRULE:FREQ=DAILY"], CalendarMapping.ToEvent(alarm, Tashkent).Recurrence!);
    }

    [Fact]
    public void ToEvent_Yearly()
    {
        var alarm = new Alarm { At = new DateTime(2027, 3, 8, 9, 0, 0), Repeat = RepeatKind.Yearly };

        Assert.Equal(["RRULE:FREQ=YEARLY"], CalendarMapping.ToEvent(alarm, Tashkent).Recurrence!);
    }

    [Fact]
    public void ToEvent_OnceThenDelete_NoRecurrenceAndRepeatProperty()
    {
        var ev = CalendarMapping.ToEvent(new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), Repeat = RepeatKind.OnceThenDelete }, Tashkent);

        Assert.Null(ev.Recurrence);
        Assert.Equal("OnceThenDelete", ev.ExtendedProperties!.Private!["repeat"]);
    }

    [Fact]
    public void ToEvent_KeepsReminderMinutes()
    {
        var ev = CalendarMapping.ToEvent(new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), ReminderMinutes = "30" }, Tashkent);

        Assert.False(ev.Reminders!.UseDefault);
        Assert.Equal([new GoogleReminder("popup", 30)], ev.Reminders.Overrides!);
    }

    [Fact]
    public void ToEvent_Disabled_EnabledFalseProperty()
    {
        var ev = CalendarMapping.ToEvent(new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), IsEnabled = false }, Tashkent);

        Assert.Equal("false", ev.ExtendedProperties!.Private!["enabled"]);
    }

    [Fact]
    public void ToEvent_PutsChronoIdAndSettingsIntoPrivateProperties()
    {
        var id = Guid.NewGuid();
        var alarm = new Alarm { Id = id, At = new DateTime(2026, 10, 26, 7, 15, 0), Alert = AlertKind.Notify, Icon = "yoga", SoundId = "chime", SoundEnabled = false, VibrationEnabled = false };

        var p = CalendarMapping.ToEvent(alarm, Tashkent).ExtendedProperties!.Private!;

        Assert.Equal(id.ToString(), p[CalendarMapping.ChronoIdKey]);
        Assert.Equal("Notify", p["alert"]);
        Assert.Equal("yoga", p["icon"]);
        Assert.Equal("chime", p["soundId"]);
        Assert.Equal("false", p["soundEnabled"]);
        Assert.Equal("false", p["vibration"]);
        Assert.Equal("true", p["enabled"]);
    }

    [Fact]
    public void ToEvent_ZoneWithDst_OffsetTakenOnEventDate()
    {
        // Берлин: до 25.10.2026 — +02:00, после — +01:00. Сегодня (20.10) лето, а первое повторение — зимой.
        var repeating = new Alarm { At = new DateTime(2026, 10, 20, 7, 0, 0), StartsOn = new DateOnly(2026, 10, 26), Repeat = RepeatKind.Daily };
        var summerOnce = new Alarm { At = new DateTime(2026, 7, 15, 8, 0, 0) };
        var winterOnce = new Alarm { At = new DateTime(2026, 12, 15, 8, 0, 0) };

        Assert.Equal("2026-10-26T07:00:00+01:00", CalendarMapping.ToEvent(repeating, Berlin).Start!.DateTime);
        Assert.Equal("2026-07-15T08:00:00+02:00", CalendarMapping.ToEvent(summerOnce, Berlin).Start!.DateTime);
        Assert.Equal("2026-12-15T08:00:00+01:00", CalendarMapping.ToEvent(winterOnce, Berlin).Start!.DateTime);
    }

    [Fact]
    public void ToEvent_DstZone_EndOffsetTakenOnEndDate()
    {
        // Начало 25.10 00:30 (+02:00), конец через 4 часа — уже после перехода (+01:00).
        var alarm = new Alarm { At = new DateTime(2026, 10, 25, 0, 30, 0), DurationMinutes = 240 };

        var ev = CalendarMapping.ToEvent(alarm, Berlin);

        Assert.Equal("2026-10-25T00:30:00+02:00", ev.Start!.DateTime);
        Assert.Equal("2026-10-25T04:30:00+01:00", ev.End!.DateTime);
    }

    [Fact]
    public void FromEvent_GoogleCreated_RingBellAndReminders()
    {
        var ev = Timed("2026-10-21T10:00:00+05:00") with { Reminders = new GoogleReminders { Overrides = [new GoogleReminder("popup", 30)] } };

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(AlertKind.Ring, alarm.Alert);
        Assert.Equal("bell", alarm.Icon);
        Assert.Equal(Sounds.Default, alarm.SoundId);
        Assert.Equal("30", alarm.ReminderMinutes);
        Assert.Equal("evt1", alarm.GoogleEventId);
        Assert.Equal("Зарядка", alarm.Label);
        Assert.Equal(RepeatKind.None, alarm.Repeat);
        Assert.Equal(new DateOnly(2026, 10, 21), alarm.StartsOn);
    }

    [Fact]
    public void FromEvent_OtherZone_ConvertsToLocalWallClock()
    {
        var ev = Timed("2026-10-21T10:00:00+03:00");

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(new DateTime(2026, 10, 21, 12, 0, 0), alarm.At);
        Assert.Equal(DateTimeKind.Unspecified, alarm.At.Kind);
    }

    [Fact]
    public void FromEvent_DstZone_UsesOffsetOfEventDate()
    {
        var ev = Timed("2026-12-01T08:00:00+01:00");

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Berlin, Now, null)).Alarm;

        Assert.Equal(new DateTime(2026, 12, 1, 8, 0, 0), alarm.At);
    }

    [Fact]
    public void FromEvent_PastOneShot_Skip()
    {
        Assert.IsType<ImportSkip>(CalendarMapping.FromEvent(Timed("2026-10-19T10:00:00+05:00"), Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_InProgressOneShot_Skip()
    {
        // Началось в 11:00 по Ташкенту, закончится в 13:00; now — 12:00.
        var ev = Timed("2026-10-20T11:00:00+05:00", "2026-10-20T13:00:00+05:00");

        Assert.IsType<ImportSkip>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_AllDay_NoneAtMidnight()
    {
        var ev = new GoogleEvent { Id = "evt1", Summary = "Отпуск", Start = new GoogleEventTime { Date = "2026-10-22" }, End = new GoogleEventTime { Date = "2026-10-23" } };

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(new DateTime(2026, 10, 22, 0, 0, 0), alarm.At);
        Assert.Equal(AlertKind.None, alarm.Alert);
        Assert.Equal(0, alarm.DurationMinutes);
    }

    [Fact]
    public void FromEvent_NoStart_Skip()
    {
        var ev = new GoogleEvent { Id = "evt1", Summary = "Битое" };

        Assert.IsType<ImportSkip>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_UnparsableStart_Skip()
    {
        Assert.IsType<ImportSkip>(CalendarMapping.FromEvent(Timed("не дата"), Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_Cancelled_DeleteWhenExisting_SkipOtherwise()
    {
        var ev = new GoogleEvent { Id = "evt1", Status = "cancelled" };
        var existing = new Alarm { GoogleEventId = "evt1" };

        Assert.Equal(existing.Id, Assert.IsType<ImportDelete>(CalendarMapping.FromEvent(ev, Tashkent, Now, existing)).AlarmId);
        Assert.IsType<ImportSkip>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_NoChronoId_TakesExistingId()
    {
        var existing = new Alarm { GoogleEventId = "evt1" };

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(Timed("2026-10-21T10:00:00+05:00"), Tashkent, Now, existing)).Alarm;

        Assert.Equal(existing.Id, alarm.Id);
    }

    [Fact]
    public void FromEvent_ChronoIdProperty_KeepsIdAndFields()
    {
        var alarms = new[]
        {
            new Alarm
            {
                Id = Guid.NewGuid(), At = new DateTime(2026, 10, 26, 7, 15, 0), Label = "Зарядка", Alert = AlertKind.Notify, Icon = "yoga",
                SoundId = "chime", SoundEnabled = false, VibrationEnabled = false, IsEnabled = false, Repeat = RepeatKind.Daily,
                DurationMinutes = 30, StartsOn = new DateOnly(2026, 10, 26), ReminderMinutes = "30,10",
            },
            new Alarm
            {
                Id = Guid.NewGuid(), At = new DateTime(2026, 10, 26, 6, 0, 0), Label = "Зал", Repeat = RepeatKind.Weekly,
                Days = WeekDays.Monday | WeekDays.Wednesday, StartsOn = new DateOnly(2026, 10, 26),
            },
            new Alarm { Id = Guid.NewGuid(), At = new DateTime(2027, 3, 8, 9, 0, 0), Label = "Праздник", Repeat = RepeatKind.Yearly, StartsOn = new DateOnly(2027, 3, 8) },
            new Alarm { Id = Guid.NewGuid(), At = new DateTime(2026, 10, 22, 8, 0, 0), Label = "Разовая", DurationMinutes = 45, StartsOn = new DateOnly(2026, 10, 22) },
            new Alarm { Id = Guid.NewGuid(), At = new DateTime(2026, 10, 22, 8, 0, 0), Label = "Один раз", Repeat = RepeatKind.OnceThenDelete, StartsOn = new DateOnly(2026, 10, 22) },
        };

        foreach (var a in alarms)
        {
            var ev = CalendarMapping.ToEvent(a, Tashkent) with { Id = "evt1" };

            var back = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

            Assert.Equal(a with { GoogleEventId = "evt1" }, back);
        }
    }

    [Fact]
    public void FromEvent_RepeatingStartedInPast_AtIsNextOccurrence()
    {
        var ev = Timed("2026-10-01T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
        Assert.Equal(new DateOnly(2026, 10, 1), alarm.StartsOn);
        Assert.Equal(new DateTime(2026, 10, 21, 7, 15, 0), alarm.At);
    }

    [Fact]
    public void FromEvent_WeeklyByDay_ReadsDays()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=WEEKLY;BYDAY=MO,WE;WKST=MO"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Weekly, alarm.Repeat);
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, alarm.Days);
    }

    [Theory]
    [InlineData("RRULE:FREQ=MONTHLY;BYDAY=1MO")]
    [InlineData("RRULE:FREQ=WEEKLY;INTERVAL=2")]
    [InlineData("RRULE:FREQ=DAILY;COUNT=5")]
    [InlineData("RRULE:FREQ=MONTHLY;COUNT=5")]
    [InlineData("RRULE:FREQ=YEARLY;BYMONTHDAY=8")]
    [InlineData("RRULE:FREQ=WEEKLY;BYDAY=1MO")]
    [InlineData("RRULE:FREQ=DAILY;BYDAY=MO")]
    [InlineData("RRULE:FREQ=MONTHLY;BYMONTHDAY=3")]
    [InlineData("RRULE:FREQ=DAILY;UNTIL=garbage")]
    public void FromEvent_UnsupportedRule_NeedsInstance(string rule)
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", "2026-10-26T07:45:00+05:00", [rule]);

        var result = CalendarMapping.FromEvent(ev, Tashkent, Now, null);

        Assert.Equal("evt1", Assert.IsType<ImportNeedsInstance>(result).EventId);

        var second = CalendarMapping.FromEvent(ev, Tashkent, Now, null, new DateTimeOffset(2026, 11, 2, 7, 15, 0, TimeSpan.FromHours(5)));
        var alarm = Assert.IsType<ImportUpsert>(second).Alarm;
        Assert.True(alarm.ForeignSeries);
        Assert.Equal(RepeatKind.None, alarm.Repeat);
        Assert.Equal(new DateTime(2026, 11, 2, 7, 15, 0), alarm.At);
        Assert.Equal(30, alarm.DurationMinutes);
        Assert.Equal("evt1", alarm.GoogleEventId);
    }

    [Fact]
    public void FromEvent_UnsupportedRuleInstanceInPast_Skip()
    {
        var ev = Timed("2026-09-01T07:15:00+05:00", recurrence: ["RRULE:FREQ=MONTHLY;BYDAY=1MO"]);

        var result = CalendarMapping.FromEvent(ev, Tashkent, Now, null, new DateTimeOffset(2026, 10, 1, 7, 15, 0, TimeSpan.FromHours(5)));

        Assert.IsType<ImportSkip>(result);
    }

    [Fact]
    public void FromEvent_WeeklyWithoutByDay_StartWeekday()
    {
        // 28.10.2026 — среда: в Google «WEEKLY» без BYDAY значит «по средам», а не «каждый день».
        var ev = Timed("2026-10-28T07:15:00+05:00", recurrence: ["RRULE:FREQ=WEEKLY"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Weekly, alarm.Repeat);
        Assert.Equal(WeekDays.Wednesday, alarm.Days);
    }

    [Fact]
    public void FromEvent_WeeklyWithoutByDay_WeekdayInLocalZone()
    {
        // 23:30 в воскресенье по +03:00 — уже понедельник 01:30 в Ташкенте.
        var ev = Timed("2026-10-25T23:30:00+03:00", recurrence: ["RRULE:FREQ=WEEKLY"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(WeekDays.Monday, alarm.Days);
    }

    [Fact]
    public void ToEvent_Yearly_StartsOnDiffersFromAt_UsesMonthDayOfAt()
    {
        var alarm = new Alarm { At = new DateTime(2027, 3, 8, 7, 0, 0), StartsOn = new DateOnly(2026, 10, 20), Repeat = RepeatKind.Yearly };

        Assert.Equal("2027-03-08T07:00:00+05:00", CalendarMapping.ToEvent(alarm, Tashkent).Start!.DateTime);
    }

    [Fact]
    public void ToEvent_Yearly_DateLaterInStartsOnYear_UsesThatYear()
    {
        var alarm = new Alarm { At = new DateTime(2027, 12, 31, 7, 0, 0), StartsOn = new DateOnly(2026, 10, 20), Repeat = RepeatKind.Yearly };

        Assert.Equal("2026-12-31T07:00:00+05:00", CalendarMapping.ToEvent(alarm, Tashkent).Start!.DateTime);
    }

    [Fact]
    public void ToEvent_Yearly_Feb29_NearestLeapYear()
    {
        var alarm = new Alarm { At = new DateTime(2028, 2, 29, 7, 0, 0), StartsOn = new DateOnly(2026, 10, 20), Repeat = RepeatKind.Yearly };

        Assert.Equal("2028-02-29T07:00:00+05:00", CalendarMapping.ToEvent(alarm, Tashkent).Start!.DateTime);
    }

    [Fact]
    public void FromEvent_RdateOnly_NeedsInstance()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RDATE;VALUE=DATE-TIME:20261101T020000Z"]);

        Assert.IsType<ImportNeedsInstance>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_RruleWithRdate_NeedsInstance()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY", "RDATE;VALUE=DATE:20261101"]);

        Assert.IsType<ImportNeedsInstance>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_ExruleWithRrule_NeedsInstance()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY", "EXRULE:FREQ=WEEKLY;BYDAY=SA"]);

        Assert.IsType<ImportNeedsInstance>(CalendarMapping.FromEvent(ev, Tashkent, Now, null));
    }

    [Fact]
    public void FromEvent_RruleWithExdate_IgnoresExdate()
    {
        // Закрепляем текущее поведение: исключения серии Chrono не отображает (спецификация 6.6).
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY", "EXDATE:20261101T020000Z"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
        Assert.False(alarm.ForeignSeries);
    }

    [Fact]
    public void FromEvent_EmailReminder_Ignored()
    {
        var ev = Timed("2026-10-21T10:00:00+05:00") with
        {
            Reminders = new GoogleReminders { Overrides = [new GoogleReminder("email", 60), new GoogleReminder("popup", 30)] },
        };

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal("30", alarm.ReminderMinutes);
    }

    [Fact]
    public void FromEvent_IntervalOne_Supported()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY;INTERVAL=1"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
    }

    [Fact]
    public void FromEvent_UseDefaultReminders_None()
    {
        var ev = Timed("2026-10-21T10:00:00+05:00") with { Reminders = new GoogleReminders { UseDefault = true, Overrides = [new GoogleReminder("popup", 30)] } };

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal("", alarm.ReminderMinutes);
    }

    [Fact]
    public void ToEvent_Monthly_FreqMonthlyAndStartOnDayOfMonth()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 15, 8, 0, 0), StartsOn = new DateOnly(2026, 10, 20), Repeat = RepeatKind.Monthly };

        var ev = CalendarMapping.ToEvent(alarm, Tashkent);

        Assert.Equal(["RRULE:FREQ=MONTHLY"], ev.Recurrence!);
        Assert.Equal("2026-11-15T08:00:00+05:00", ev.Start!.DateTime);
    }

    [Fact]
    public void ToEvent_EndsOn_UntilIsEndOfLocalDayInUtc()
    {
        var alarm = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), StartsOn = new DateOnly(2026, 10, 26), Repeat = RepeatKind.Daily, EndsOn = new DateOnly(2026, 11, 9) };

        var ev = CalendarMapping.ToEvent(alarm, Tashkent);

        Assert.Equal(["RRULE:FREQ=DAILY;UNTIL=20261109T185959Z"], ev.Recurrence!);
    }

    [Fact]
    public void FromEvent_Monthly_Supported()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=MONTHLY"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Monthly, alarm.Repeat);
        Assert.False(alarm.ForeignSeries);
    }

    [Fact]
    public void FromEvent_MonthlyByMonthDayEqualToStartDay_Supported()
    {
        var ev = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=MONTHLY;BYMONTHDAY=26"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.Equal(RepeatKind.Monthly, alarm.Repeat);
    }

    [Fact]
    public void FromEvent_UntilUtc_EndsOnLocalDate()
    {
        var utc = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY;UNTIL=20261109T185959Z"]);
        var date = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY;UNTIL=20261109"]);
        var local = Timed("2026-10-26T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY;UNTIL=20261109T235959"]);

        Assert.Equal(new DateOnly(2026, 11, 9), Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(utc, Tashkent, Now, null)).Alarm.EndsOn);
        Assert.Equal(new DateOnly(2026, 11, 9), Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(date, Tashkent, Now, null)).Alarm.EndsOn);
        Assert.Equal(new DateOnly(2026, 11, 9), Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(local, Tashkent, Now, null)).Alarm.EndsOn);
    }

    [Fact]
    public void FromEvent_UntilInPast_ImportedEnabledWithoutChange()
    {
        var ev = Timed("2026-09-01T07:15:00+05:00", recurrence: ["RRULE:FREQ=DAILY;UNTIL=20261001T000000Z"]);

        var alarm = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(ev, Tashkent, Now, null)).Alarm;

        Assert.True(alarm.IsEnabled);
        Assert.Equal(new DateOnly(2026, 10, 1), alarm.EndsOn);
    }

    [Fact]
    public void RoundTrip_MonthlyWithEndsOn_Equal()
    {
        var seed = new Alarm
        {
            Id = Guid.NewGuid(), At = new DateTime(2026, 10, 15, 8, 0, 0), StartsOn = new DateOnly(2026, 10, 15),
            Repeat = RepeatKind.Monthly, EndsOn = new DateOnly(2027, 3, 9), GoogleEventId = "evt1",
        };
        var a = seed with { At = seed.NextAfter(Now) };

        var back = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(CalendarMapping.ToEvent(a, Tashkent), Tashkent, Now, a)).Alarm;

        Assert.Equal(a, back);
    }

    [Fact]
    public void RoundTrip_WeeklyWithEndsOn_Equal()
    {
        var seed = new Alarm
        {
            Id = Guid.NewGuid(), At = new DateTime(2026, 10, 26, 7, 15, 0), StartsOn = new DateOnly(2026, 10, 26),
            Repeat = RepeatKind.Weekly, Days = WeekDays.Monday | WeekDays.Wednesday, EndsOn = new DateOnly(2026, 12, 31), GoogleEventId = "evt1",
        };
        var a = seed with { At = seed.NextAfter(Now) };

        var back = Assert.IsType<ImportUpsert>(CalendarMapping.FromEvent(CalendarMapping.ToEvent(a, Tashkent), Tashkent, Now, a)).Alarm;

        Assert.Equal(a, back);
    }
}
