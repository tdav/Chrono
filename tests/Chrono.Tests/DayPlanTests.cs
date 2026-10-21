using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class DayPlanTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 7, 30, 0);
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly IReadOnlyDictionary<Guid, HashSet<DateOnly>> NoDone = new Dictionary<Guid, HashSet<DateOnly>>();

    private static Alarm Daily(int hour, int minute, int duration = 0, string label = "") => new()
    {
        At = new DateTime(2026, 10, 20, hour, minute, 0),
        Repeat = RepeatKind.Daily,
        DurationMinutes = duration,
        Label = label,
    };

    [Fact]
    public void For_DailyYoga_IsCurrentWithEnd()
    {
        var entry = Assert.Single(DayPlan.For([Daily(7, 15, 30)], NoDone, Today, Now));

        Assert.Equal(new DateTime(2026, 10, 20, 7, 45, 0), entry.End);
        Assert.True(entry.IsCurrent);
    }

    [Fact]
    public void For_ZeroDuration_NeverCurrent()
    {
        var entry = Assert.Single(DayPlan.For([Daily(7, 30)], NoDone, Today, Now));

        Assert.False(entry.IsCurrent);
    }

    [Fact]
    public void For_NowEqualsEnd_NotCurrent()
    {
        // 07:00 + 30 мин = 07:30: конец дела не входит в «идёт сейчас».
        var entry = Assert.Single(DayPlan.For([Daily(7, 0, 30)], NoDone, Today, Now));

        Assert.False(entry.IsCurrent);
    }

    [Fact]
    public void For_SortedByStartThenLabel()
    {
        var labels = DayPlan.For([Daily(8, 0, label: "Б"), Daily(7, 0, label: "Я"), Daily(8, 0, label: "А")], NoDone, Today, Now)
            .Select(e => e.Alarm.Label);

        Assert.Equal(["Я", "А", "Б"], labels);
    }

    [Fact]
    public void For_DoneOnlyForThatDay()
    {
        var alarm = Daily(9, 0);
        var done = new Dictionary<Guid, HashSet<DateOnly>> { [alarm.Id] = [Today] };

        Assert.True(Assert.Single(DayPlan.For([alarm], done, Today, Now)).IsDone);
        Assert.False(Assert.Single(DayPlan.For([alarm], done, Today.AddDays(1), Now)).IsDone);
    }

    [Fact]
    public void For_DisabledFutureOccurrence_Muted_PastOneNot()
    {
        var future = Daily(9, 0) with { IsEnabled = false };
        var past = new Alarm { At = new DateTime(2026, 10, 20, 7, 0, 0), IsEnabled = false };

        Assert.True(Assert.Single(DayPlan.For([future], NoDone, Today, Now)).IsMuted);
        Assert.False(Assert.Single(DayPlan.For([past], NoDone, Today, Now)).IsMuted);
    }

    [Fact]
    public void For_EnabledFuture_NotMuted()
    {
        Assert.False(Assert.Single(DayPlan.For([Daily(9, 0)], NoDone, Today, Now)).IsMuted);
    }

    [Fact]
    public void For_AcrossMidnight_OnlyStartDay()
    {
        var late = new Alarm { At = new DateTime(2026, 10, 20, 23, 30, 0), DurationMinutes = 60 };

        Assert.Single(DayPlan.For([late], NoDone, Today, Now));
        Assert.Empty(DayPlan.For([late], NoDone, Today.AddDays(1), Now));

        // После полуночи дело всё ещё идёт — в списке дня начала.
        var afterMidnight = DayPlan.For([late], NoDone, Today, new DateTime(2026, 10, 21, 0, 15, 0));
        Assert.True(Assert.Single(afterMidnight).IsCurrent);
    }

    [Fact]
    public void For_BeforeStartsOn_Excluded()
    {
        var alarm = Daily(9, 0) with { StartsOn = new DateOnly(2026, 10, 26) };

        Assert.Empty(DayPlan.For([alarm], NoDone, Today, Now));
    }

    [Fact]
    public void Week_Sunday_ReturnsPrecedingMonday()
    {
        var week = DayPlan.Week(new DateOnly(2026, 10, 25));

        Assert.Equal(7, week.Count);
        Assert.Equal(new DateOnly(2026, 10, 19), week[0]);
        Assert.Equal(new DateOnly(2026, 10, 25), week[6]);
    }

    [Fact]
    public void Week_Monday_StartsOnItself()
    {
        Assert.Equal(new DateOnly(2026, 10, 19), DayPlan.Week(new DateOnly(2026, 10, 19))[0]);
    }
}
