using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class IosNotificationPlanTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0);

    [Fact]
    public void Build_OneAlarm_GivesFourNotificationsAt0_30_60_90Seconds()
    {
        var alarm = new Alarm { At = Now.AddHours(1) };

        var plan = IosNotificationPlan.Build([alarm], Now);

        Assert.Equal(
            [alarm.At, alarm.At.AddSeconds(30), alarm.At.AddSeconds(60), alarm.At.AddSeconds(90)],
            plan.Select(p => p.FireAt));
        Assert.Equal(IosNotificationPlan.ChainIds(alarm.Id), plan.Select(p => p.Id));
    }

    [Fact]
    public void Build_SkipsDisabledAndPastAlarms()
    {
        var disabled = new Alarm { At = Now.AddHours(1), IsEnabled = false };
        var past = new Alarm { At = Now.AddMinutes(-1) };
        var atNow = new Alarm { At = Now };
        var future = new Alarm { At = Now.AddHours(2) };

        var plan = IosNotificationPlan.Build([disabled, past, atNow, future], Now);

        Assert.All(plan, p => Assert.Equal(future.Id, p.Alarm.Id));
    }

    [Fact]
    public void Build_KeepsOnlySixteenNearestAlarms_SixtyFourNotifications()
    {
        // Перемешанный порядок: план обязан выбрать ближайшие, а не первые по списку.
        var alarms = Enumerable.Range(1, 20).Reverse().Select(h => new Alarm { At = Now.AddHours(h) }).ToList();

        var plan = IosNotificationPlan.Build(alarms, Now);

        Assert.Equal(64, plan.Count);
        Assert.Equal(Now.AddHours(16).AddSeconds(90), plan.Max(p => p.FireAt));
    }
}
