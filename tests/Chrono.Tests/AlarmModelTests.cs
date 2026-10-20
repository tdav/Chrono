using Chrono.Models;

namespace Chrono.Tests;

public sealed class AlarmModelTests
{
    [Fact]
    public void NextAfter_DailyWithFutureStart_FirstFiresOnStartDay()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 20, 7, 15, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 26) };

        Assert.Equal(new DateTime(2026, 10, 26, 7, 15, 0), a.NextAfter(new DateTime(2026, 10, 20, 12, 0, 0)));
    }

    [Fact]
    public void NextAfter_StartInPast_IsIgnored()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 1, 7, 15, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 1) };

        Assert.Equal(new DateTime(2026, 10, 21, 7, 15, 0), a.NextAfter(new DateTime(2026, 10, 20, 12, 0, 0)));
    }

    [Fact]
    public void NextAfter_WeeklyWithFutureStart_FirstMatchingDayOnOrAfterStart()
    {
        // 26.10.2026 — понедельник; начало во вторник 27.10 пропускает понедельник, первым подходит среда 28.10.
        var moment = new DateTime(2026, 10, 20, 12, 0, 0);
        var a = new Alarm { At = new DateTime(2026, 10, 26, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = WeekDays.Monday | WeekDays.Wednesday, StartsOn = new DateOnly(2026, 10, 27) };

        Assert.Equal(new DateTime(2026, 10, 28, 7, 15, 0), a.NextAfter(moment));
        // Начало в подходящий день (среда 28.10) — этот же день и срабатывает.
        Assert.Equal(new DateTime(2026, 10, 28, 7, 15, 0), (a with { StartsOn = new DateOnly(2026, 10, 28) }).NextAfter(moment));
    }

    [Fact]
    public void NextAfter_YearlyWithFutureStart_SkipsYearBeforeStart()
    {
        // Без StartsOn ближайшим было бы 15.03.2027; начало 01.04.2027 пропускает его, остаётся 15.03.2028.
        var a = new Alarm { At = new DateTime(2026, 3, 15, 7, 15, 0), Repeat = RepeatKind.Yearly, StartsOn = new DateOnly(2027, 4, 1) };

        Assert.Equal(new DateTime(2028, 3, 15, 7, 15, 0), a.NextAfter(new DateTime(2026, 10, 20, 12, 0, 0)));
    }

    [Fact]
    public void OccursOn_DailyBeforeStart_False()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 20, 7, 15, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 26) };

        Assert.False(a.OccursOn(new DateOnly(2026, 10, 25)));
        Assert.True(a.OccursOn(new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public void OccursOn_WeeklyMonWed_OnlyThoseDays()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 19, 7, 15, 0), Repeat = RepeatKind.Weekly, Days = WeekDays.Monday | WeekDays.Wednesday };

        Assert.True(a.OccursOn(new DateOnly(2026, 10, 26)));
        Assert.True(a.OccursOn(new DateOnly(2026, 10, 28)));
        Assert.False(a.OccursOn(new DateOnly(2026, 10, 27)));
    }

    [Fact]
    public void OccursOn_WeeklyNoDays_EveryDay()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 19, 7, 15, 0), Repeat = RepeatKind.Weekly };

        Assert.True(a.OccursOn(new DateOnly(2026, 10, 27)));
        Assert.True(a.OccursOn(new DateOnly(2026, 10, 25)));
    }

    [Fact]
    public void OccursOn_YearlyFeb29_NonLeapYearFalse()
    {
        var a = new Alarm { At = new DateTime(2028, 2, 29, 7, 15, 0), Repeat = RepeatKind.Yearly };

        Assert.False(a.OccursOn(new DateOnly(2027, 2, 28)));
        Assert.True(a.OccursOn(new DateOnly(2028, 2, 29)));
    }

    [Theory]
    [InlineData(RepeatKind.None)]
    [InlineData(RepeatKind.OnceThenDelete)]
    public void OccursOn_OneShot_OnlyAtDate(RepeatKind repeat)
    {
        var a = new Alarm { At = new DateTime(2026, 10, 20, 7, 15, 0), Repeat = repeat };

        Assert.True(a.OccursOn(new DateOnly(2026, 10, 20)));
        Assert.False(a.OccursOn(new DateOnly(2026, 10, 21)));
        Assert.False(a.OccursOn(new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void IsOneShot_NoneAndOnceThenDelete_True()
    {
        Assert.True(new Alarm { Repeat = RepeatKind.None }.IsOneShot());
        Assert.True(new Alarm { Repeat = RepeatKind.OnceThenDelete }.IsOneShot());
        Assert.False(new Alarm { Repeat = RepeatKind.Daily }.IsOneShot());
        Assert.False(new Alarm { Repeat = RepeatKind.Weekly }.IsOneShot());
        Assert.False(new Alarm { Repeat = RepeatKind.Yearly }.IsOneShot());
    }

    [Fact]
    public void ExpiresAt_NotifyWithDuration_IsEnd()
    {
        var a = new Alarm { At = new DateTime(2026, 10, 20, 7, 15, 0), Alert = AlertKind.Notify, DurationMinutes = 30 };

        Assert.Equal(new DateTime(2026, 10, 20, 7, 45, 0), a.ExpiresAt());
    }

    [Fact]
    public void ExpiresAt_RingShorterThanTimeout_UsesTimeout()
    {
        var at = new DateTime(2026, 10, 20, 7, 15, 0);

        Assert.Equal(at.AddMinutes(10), new Alarm { At = at, Alert = AlertKind.Ring, DurationMinutes = 0 }.ExpiresAt());
        Assert.Equal(at.AddMinutes(45), new Alarm { At = at, Alert = AlertKind.Ring, DurationMinutes = 45 }.ExpiresAt());
    }

    [Fact]
    public void RepeatKind_ExistingValues_KeepNumbers()
    {
        Assert.Equal(0, (int)RepeatKind.None);
        Assert.Equal(1, (int)RepeatKind.Daily);
        Assert.Equal(2, (int)RepeatKind.Weekly);
        Assert.Equal(3, (int)RepeatKind.Yearly);
        Assert.Equal(4, (int)RepeatKind.OnceThenDelete);
    }
}
