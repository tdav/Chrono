using Chrono.Services;

namespace Chrono.Tests;

public class AlarmScheduleTests
{
    [Fact]
    public void ResolveNearest_TimeAhead_IsToday()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal(new DateTime(2026, 10, 16, 21, 0, 0), AlarmSchedule.ResolveNearest(new TimeOnly(21, 0), now));
    }

    [Theory]
    [InlineData(19, 46)] // ровно сейчас — уже не «сегодня»
    [InlineData(6, 30)]
    public void ResolveNearest_TimeNotAhead_IsTomorrow(int hour, int minute)
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal(new DateTime(2026, 10, 17, hour, minute, 0), AlarmSchedule.ResolveNearest(new TimeOnly(hour, minute), now));
    }

    [Fact]
    public void ResolveNearest_SecondsAfterMinute_TreatsMinuteAsPassed()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 30);

        Assert.Equal(new DateTime(2026, 10, 17, 19, 46, 0), AlarmSchedule.ResolveNearest(new TimeOnly(19, 46), now));
    }

    [Fact]
    public void ResolveNearest_CrossesYearEnd()
    {
        var now = new DateTime(2026, 12, 31, 23, 50, 0);

        Assert.Equal(new DateTime(2027, 1, 1, 7, 0, 0), AlarmSchedule.ResolveNearest(new TimeOnly(7, 0), now));
    }

    [Fact]
    public void Until_HoursAndMinutes()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal((0, 20, 10), AlarmSchedule.Until(new DateTime(2026, 10, 17, 15, 56, 0), now));
    }

    [Fact]
    public void Until_DaysHoursMinutes()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal((2, 3, 4), AlarmSchedule.Until(new DateTime(2026, 10, 18, 22, 50, 0), now));
    }

    [Fact]
    public void Until_PartialMinute_RoundsUp()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 40);

        Assert.Equal((0, 0, 1), AlarmSchedule.Until(new DateTime(2026, 10, 16, 19, 47, 0), now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    public void Until_NowOrPast_IsNull(int secondsFromNow)
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Null(AlarmSchedule.Until(now.AddSeconds(secondsFromNow), now));
    }

    [Fact]
    public void Month_StartingSunday_HasSixLeadingBlanks()
    {
        // 1 ноября 2026 — воскресенье.
        var cells = AlarmSchedule.Month(2026, 11, new DateOnly(2026, 10, 16), allowPast: false);

        Assert.Equal(6, cells.TakeWhile(c => c.Date is null).Count());
        Assert.Equal(new DateOnly(2026, 11, 1), cells[6].Date);
        Assert.Equal(36, cells.Count);
    }

    [Fact]
    public void Month_StartingMonday_HasNoLeadingBlanks()
    {
        // 1 июня 2026 — понедельник.
        var cells = AlarmSchedule.Month(2026, 6, new DateOnly(2026, 1, 1), allowPast: false);

        Assert.Equal(new DateOnly(2026, 6, 1), cells[0].Date);
        Assert.Equal(30, cells.Count);
    }

    [Theory]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 2, 28)]
    [InlineData(2026, 10, 31)]
    public void Month_ContainsEveryDayOfMonth(int year, int month, int days)
    {
        var cells = AlarmSchedule.Month(year, month, new DateOnly(2026, 1, 1), allowPast: false);

        Assert.Equal(Enumerable.Range(1, days), cells.Where(c => c.Date is not null).Select(c => c.Date!.Value.Day));
    }

    [Fact]
    public void Month_FlagsPastTodayAndWeekend()
    {
        var today = new DateOnly(2026, 10, 16);

        var cells = AlarmSchedule.Month(2026, 10, today, allowPast: false).Where(c => c.Date is not null).ToDictionary(c => c.Date!.Value.Day);

        Assert.True(cells[15].IsPast);
        Assert.False(cells[15].IsEnabled);
        Assert.True(cells[16].IsToday);
        Assert.False(cells[16].IsPast);
        Assert.True(cells[16].IsEnabled);
        Assert.True(cells[17].IsWeekend); // суббота
        Assert.True(cells[18].IsWeekend); // воскресенье
        Assert.False(cells[19].IsWeekend);
    }

    [Fact]
    public void Month_AllowPast_EnablesPastDays()
    {
        var cells = AlarmSchedule.Month(2026, 10, new DateOnly(2026, 10, 16), allowPast: true);

        var first = cells.First(c => c.Date is not null);
        Assert.True(first.IsPast);
        Assert.True(first.IsEnabled);
    }
}
