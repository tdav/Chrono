using Chrono.Services;

namespace Chrono.Tests;

public sealed class DurationStepsTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 10)]
    [InlineData(30, 35)]
    [InlineData(32, 35)]
    [InlineData(34, 35)]
    [InlineData(715, 720)]
    [InlineData(718, 720)]
    [InlineData(720, 720)]
    public void Increase_GoesToNextMultipleAndStopsAtMax(int minutes, int expected)
    {
        Assert.Equal(expected, DurationSteps.Increase(minutes));
    }

    [Theory]
    [InlineData(720, 715)]
    [InlineData(36, 35)]
    [InlineData(35, 30)]
    [InlineData(32, 30)]
    [InlineData(31, 30)]
    [InlineData(10, 5)]
    [InlineData(6, 5)]
    [InlineData(5, 5)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void Decrease_GoesToPreviousMultipleAndStopsAtMin(int minutes, int expected)
    {
        Assert.Equal(expected, DurationSteps.Decrease(minutes));
    }

    [Fact]
    public void Bounds_ButtonsInactiveAtTheEdgesOfTheRange()
    {
        Assert.False(DurationSteps.CanDecrease(DurationSteps.Min));
        Assert.True(DurationSteps.CanDecrease(DurationSteps.Min + DurationSteps.Step));
        Assert.True(DurationSteps.CanIncrease(DurationSteps.Max - DurationSteps.Step));
        Assert.False(DurationSteps.CanIncrease(DurationSteps.Max));

        // «Нет» (0) — вне диапазона степпера: «−» неактивна, «+» начинает с 5.
        Assert.False(DurationSteps.CanDecrease(0));
        Assert.True(DurationSteps.CanIncrease(0));
    }

    [Fact]
    public void Steps_FromEveryValueStayInRangeOnTheGridAndMove()
    {
        for (var minutes = 0; minutes <= DurationSteps.Max; minutes++)
        {
            var up = DurationSteps.Increase(minutes);
            Assert.InRange(up, DurationSteps.Min, DurationSteps.Max);
            Assert.Equal(0, up % DurationSteps.Step);
            Assert.True(up > minutes || up == DurationSteps.Max);

            if (minutes >= DurationSteps.Min)
            {
                var down = DurationSteps.Decrease(minutes);
                Assert.InRange(down, DurationSteps.Min, DurationSteps.Max);
                Assert.Equal(0, down % DurationSteps.Step);
                Assert.True(down < minutes || minutes == DurationSteps.Min);
            }
        }
    }
}
