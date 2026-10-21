using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class DialLayoutTests
{
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static DayEntry E(string start, int minutes)
    {
        var time = TimeOnly.Parse(start);
        var at = Day.ToDateTime(time);
        var alarm = new Alarm { At = at, DurationMinutes = minutes };
        return new DayEntry(alarm, at, at.AddMinutes(minutes), false, false, false);
    }

    private static DateTime At(int hour, int minute) => Day.ToDateTime(new TimeOnly(hour, minute));

    [Fact]
    public void AngleOf_Values()
    {
        Assert.Equal(0, DialLayout.AngleOf(new TimeOnly(0, 0)));
        Assert.Equal(90, DialLayout.AngleOf(new TimeOnly(3, 0)));
        Assert.Equal(0, DialLayout.AngleOf(new TimeOnly(12, 0)));
        Assert.Equal(320, DialLayout.AngleOf(new TimeOnly(10, 40)));
    }

    [Fact]
    public void HalfOf_NoonIsEvening()
    {
        Assert.Equal(DialHalf.Morning, DialLayout.HalfOf(At(11, 59)));
        Assert.Equal(DialHalf.Evening, DialLayout.HalfOf(At(12, 0)));
    }

    [Fact]
    public void Slices_CrossingNoon_ClippedIntoBothHalves()
    {
        var entries = new[] { E("11:00", 120) };

        var morning = Assert.Single(DialLayout.Slices(entries, Day, DialHalf.Morning));
        var evening = Assert.Single(DialLayout.Slices(entries, Day, DialHalf.Evening));

        Assert.Equal(330, morning.StartAngle);
        Assert.Equal(30, morning.SweepAngle);
        Assert.Equal(0, evening.StartAngle);
        Assert.Equal(30, evening.SweepAngle);
    }

    [Fact]
    public void Slices_TickAtNoon_OnlyInEvening()
    {
        var entries = new[] { E("12:00", 0) };

        Assert.Empty(DialLayout.Slices(entries, Day, DialHalf.Morning));
        var tick = Assert.Single(DialLayout.Slices(entries, Day, DialHalf.Evening));
        Assert.True(tick.IsTick);
        Assert.Equal(0, tick.StartAngle);
    }

    [Fact]
    public void Slices_CrossingMidnight_ClippedAtEndOfDay()
    {
        var slice = Assert.Single(DialLayout.Slices([E("23:00", 90)], Day, DialHalf.Evening));

        Assert.Equal(330, slice.StartAngle);
        Assert.Equal(30, slice.SweepAngle);
    }

    [Fact]
    public void Slices_OrderLongFirstShortLast()
    {
        var long1 = E("09:00", 210);
        var short1 = E("10:00", 60);

        var slices = DialLayout.Slices([short1, long1], Day, DialHalf.Morning);

        Assert.Equal(2, slices.Count);
        Assert.Equal(short1.Alarm.Id, slices[^1].Entry.Alarm.Id);
        Assert.Equal(long1.Alarm.Id, slices[0].Entry.Alarm.Id);
    }

    [Fact]
    public void Slices_TickIsAlwaysOnTop()
    {
        var slices = DialLayout.Slices([E("10:30", 0), E("10:00", 60)], Day, DialHalf.Morning);

        Assert.True(slices[^1].IsTick);
    }

    [Fact]
    public void HitTest_Overlap_PicksShortest()
    {
        var long1 = E("09:00", 210);
        var short1 = E("10:00", 60);
        var slices = DialLayout.Slices([long1, short1], Day, DialHalf.Morning);

        var hit = DialLayout.HitTest(slices, 315, 0.8);

        Assert.Equal(short1.Alarm.Id, hit!.Value.Alarm.Id);
    }

    [Fact]
    public void HitTest_SectorAcrossTwelve_WrapsAngle()
    {
        // Slices обрезает дело 09:00/210 до 12:00 (270°…360°), поэтому сектор через 360° собираем вручную.
        var entry = E("09:00", 210);
        DialSlice[] slices = [new(entry, 270, 105, false, 0)];

        Assert.Equal(entry.Alarm.Id, DialLayout.HitTest(slices, 5, 0.8)!.Value.Alarm.Id);
        Assert.Null(DialLayout.HitTest(slices, 20, 0.8));
    }

    [Fact]
    public void HitTest_EmptyArea_ReturnsNull()
    {
        var slices = DialLayout.Slices([E("09:00", 60)], Day, DialHalf.Morning);

        Assert.Null(DialLayout.HitTest(slices, 90, 0.8));
        Assert.Null(DialLayout.HitTest([], 90, 0.8));
    }

    [Fact]
    public void HitTest_InsideHoleOrOutside_ReturnsNull()
    {
        var slices = DialLayout.Slices([E("09:00", 60)], Day, DialHalf.Morning);

        Assert.NotNull(DialLayout.HitTest(slices, 280, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 280, 0.3));
        Assert.Null(DialLayout.HitTest(slices, 280, 1.2));
    }

    [Fact]
    public void HitTest_Tick_WithinFourDegrees()
    {
        var entry = E("07:00", 0);
        var slices = DialLayout.Slices([entry], Day, DialHalf.Morning);

        Assert.Equal(entry.Alarm.Id, DialLayout.HitTest(slices, 213, 0.8)!.Value.Alarm.Id);
        Assert.Equal(entry.Alarm.Id, DialLayout.HitTest(slices, 207, 0.8)!.Value.Alarm.Id);
        Assert.Null(DialLayout.HitTest(slices, 215, 0.8));
    }

    [Fact]
    public void Current_TwoRunning_PicksShortest()
    {
        var long1 = E("09:00", 210);
        var short1 = E("10:00", 60);

        Assert.Equal(short1.Alarm.Id, DialLayout.Current([long1, short1], At(10, 30))!.Value.Alarm.Id);
        Assert.Null(DialLayout.Current([long1, short1], At(8, 0)));
    }

    [Fact]
    public void Current_EqualDuration_PicksTopmostLikeHitTest()
    {
        // Два идущих дела одной длины: «текущим» должно быть то же, что выбирает касание, — верхнее (последнее в порядке отрисовки).
        var first = E("10:00", 60);
        var second = E("10:15", 60);
        var slices = DialLayout.Slices([first, second], Day, DialHalf.Morning);

        var current = DialLayout.Current([first, second], At(10, 30))!.Value;
        var touched = DialLayout.HitTest(slices, DialLayout.AngleOf(new TimeOnly(10, 30)), 0.8)!.Value;

        Assert.Equal(touched.Alarm.Id, current.Alarm.Id);
    }

    [Fact]
    public void Next_FirstStartAfterNow()
    {
        var next = E("09:00", 30);

        Assert.Equal(next.Alarm.Id, DialLayout.Next([E("07:00", 0), next], At(8, 0))!.Value.Alarm.Id);
    }

    [Fact]
    public void FreeSlot_SkipsBusyBoundaries()
    {
        var entries = new[] { E("10:00", 60), E("11:00", 30), E("11:30", 0) };

        Assert.Equal(At(11, 30), DialLayout.FreeSlot(entries, At(9, 50)));
    }

    [Fact]
    public void FreeSlot_OnBoundaryNow_UsesNow()
    {
        Assert.Equal(At(14, 0), DialLayout.FreeSlot([], At(14, 0)));
    }

    [Fact]
    public void FreeSlot_LateEvening_RollsToTomorrow()
    {
        Assert.Equal(new DateTime(2026, 10, 22, 0, 0, 0), DialLayout.FreeSlot([], At(23, 40)));
    }

    [Fact]
    public void FreeSlot_WindowMustFit()
    {
        Assert.Equal(At(11, 0), DialLayout.FreeSlot([E("10:15", 30)], At(9, 50)));
    }

    [Fact]
    public void ColorIndex_StableAndInRange()
    {
        for (var i = 0; i < 50; i++)
        {
            var id = Guid.NewGuid();
            var index = DialLayout.ColorIndex(id);
            Assert.InRange(index, 0, 11);
            Assert.Equal(index, DialLayout.ColorIndex(id));
        }

        var fixedId = Guid.Parse("27b5a2fb-0874-4db1-98aa-c02174d89ebf");
        // Литерал: сумма байт Guid.ToByteArray() = 2239, 2239 % 12 = 7. Смена свёртки должна краснить тест.
        Assert.Equal(7, DialLayout.ColorIndex(fixedId));
    }

    [Fact]
    public void HitTest_ShortestByFullDuration_NotByClippedSlice()
    {
        // A 11:00-13:00 в Morning режется до 30°, B 10:30-11:45 занимает 37,5°; по полной длительности короче B.
        var a = E("11:00", 120);
        var b = E("10:30", 75);
        var entries = new[] { a, b };
        var slices = DialLayout.Slices(entries, Day, DialHalf.Morning);

        Assert.Equal(b.Alarm.Id, slices[^1].Entry.Alarm.Id);
        Assert.Equal(b.Alarm.Id, DialLayout.HitTest(slices, 345, 0.8)!.Value.Alarm.Id);
        Assert.Equal(b.Alarm.Id, DialLayout.Current(entries, At(11, 30))!.Value.Alarm.Id);
    }

    [Fact]
    public void HitTest_EqualDuration_PicksTopmost()
    {
        var first = E("10:00", 60);
        var second = E("10:00", 60);
        var slices = DialLayout.Slices([first, second], Day, DialHalf.Morning);

        Assert.Equal(second.Alarm.Id, DialLayout.HitTest(slices, 315, 0.8)!.Value.Alarm.Id);
    }

    [Fact]
    public void HitTest_NormalizesNegativeAndOverFullTurn()
    {
        var near350 = E("11:40", 10);
        var near10 = E("00:10", 30);
        var slices = DialLayout.Slices([near350, near10], Day, DialHalf.Morning);

        Assert.Equal(near350.Alarm.Id, DialLayout.HitTest(slices, -10, 0.8)!.Value.Alarm.Id);
        Assert.Equal(near10.Alarm.Id, DialLayout.HitTest(slices, 370, 0.8)!.Value.Alarm.Id);
    }

    [Fact]
    public void HitTest_Tick_NormalizesAngleBeforeComparing()
    {
        var tick = E("07:00", 0);
        var slices = DialLayout.Slices([tick], Day, DialHalf.Morning);

        // 570 = 210, -150 = 210: те же засечка; 730 = 10 и -170 = 190 засечку не задевают.
        Assert.NotNull(DialLayout.HitTest(slices, 570, 0.8));
        Assert.NotNull(DialLayout.HitTest(slices, -150, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 730, 0.8));
        Assert.Null(DialLayout.HitTest(slices, -170, 0.8));
    }

    [Fact]
    public void HitTest_TickAtMidnight_WrapsAcrossZero()
    {
        var tick = E("00:00", 0);
        var slices = DialLayout.Slices([tick], Day, DialHalf.Morning);

        Assert.Equal(tick.Alarm.Id, DialLayout.HitTest(slices, 358, 0.8)!.Value.Alarm.Id);
        Assert.Equal(tick.Alarm.Id, DialLayout.HitTest(slices, 3, 0.8)!.Value.Alarm.Id);
        Assert.Null(DialLayout.HitTest(slices, 355, 0.8));
    }

    [Fact]
    public void HitTest_Tick_ExactlyFourDegreesHits()
    {
        var tick = E("07:00", 0);
        var slices = DialLayout.Slices([tick], Day, DialHalf.Morning);

        Assert.NotNull(DialLayout.HitTest(slices, 214, 0.8));
        Assert.NotNull(DialLayout.HitTest(slices, 206, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 214.1, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 205.9, 0.8));
    }

    [Fact]
    public void HitTest_SectorEnd_IsHalfOpen()
    {
        var slices = DialLayout.Slices([E("09:00", 60)], Day, DialHalf.Morning);

        Assert.NotNull(DialLayout.HitTest(slices, 270, 0.8));
        Assert.NotNull(DialLayout.HitTest(slices, 299.9, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 300, 0.8));
        Assert.Null(DialLayout.HitTest(slices, 269.9, 0.8));
    }

    [Fact]
    public void HitTest_TickOverCoveringSector_PicksTick()
    {
        var tick = E("10:15", 0);
        var slices = DialLayout.Slices([E("10:00", 60), tick], Day, DialHalf.Morning);

        Assert.Equal(tick.Alarm.Id, DialLayout.HitTest(slices, 307.5, 0.8)!.Value.Alarm.Id);
    }

    [Fact]
    public void FreeSlot_NoFreeBoundaryToday_FallsBackToNextBoundary()
    {
        // По §6.7 фолбэк — ближайшая граница после now в её собственном дне, даже если она занята.
        Assert.Equal(At(23, 30), DialLayout.FreeSlot([E("23:30", 30)], At(23, 10)));
        Assert.Equal(At(15, 30), DialLayout.FreeSlot([E("14:00", 600)], At(15, 10)));
    }

    [Fact]
    public void FreeSlot_KeepsKindOfNow()
    {
        var now = DateTime.SpecifyKind(At(9, 50), DateTimeKind.Local);

        Assert.Equal(DateTimeKind.Local, DialLayout.FreeSlot([], now).Kind);
    }

    [Fact]
    public void Palette_TwelveColorsWithoutClockRed()
    {
        Assert.Equal(12, DialLayout.Palette.Count);
        Assert.DoesNotContain("#B5482F", DialLayout.Palette);
        Assert.Equal(12, DialLayout.Palette.Distinct().Count());
    }
}
