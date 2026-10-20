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

    [Fact]
    public void Build_NoneAlert_NotPlanned()
    {
        var none = new Alarm { At = Now.AddHours(1), Alert = AlertKind.None };

        Assert.Empty(IosNotificationPlan.Build([none], Now));
    }

    [Fact]
    public void Build_Notify_SingleNotification()
    {
        var notify = new Alarm { At = Now.AddHours(1), Alert = AlertKind.Notify };

        var plan = IosNotificationPlan.Build([notify], Now);

        var single = Assert.Single(plan);
        Assert.Equal(notify.At, single.FireAt);
        Assert.Equal(IosNotificationPlan.ChainIds(notify.Id)[0], single.Id);
    }

    [Fact]
    public void Build_OnceThenDelete_SingleOccurrence()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Ring };

        var plan = IosNotificationPlan.Build([alarm], Now);

        Assert.Equal(4, plan.Count);
        Assert.Equal(IosNotificationPlan.ChainIds(alarm.Id), plan.Select(p => p.Id));
    }

    [Fact]
    public void Build_ChainIdsUnchangedForRing()
    {
        var daily = new Alarm { At = Now.AddHours(1), Repeat = RepeatKind.Daily, Alert = AlertKind.Ring };

        var plan = IosNotificationPlan.Build([daily], Now);

        Assert.Equal(IosNotificationPlan.ChainIds(daily.Id), plan.Take(4).Select(p => p.Id));
        Assert.Equal($"{daily.Id}-4", plan[4].Id);
    }

    [Fact]
    public void Build_NotifyDaily_UsesSharedSixteenLimitWithStableIds()
    {
        var notify = new Alarm { At = Now.AddHours(1), Repeat = RepeatKind.Daily, Alert = AlertKind.Notify };

        var plan = IosNotificationPlan.Build([notify], Now);

        Assert.Equal(16, plan.Count);
        Assert.Equal($"{notify.Id}-4", plan[1].Id);
    }
    [Fact]
    public void Build_Reminder_AddsEarlierNotification()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Alert = AlertKind.Ring, ReminderMinutes = "30" };

        var plan = IosNotificationPlan.Build([alarm], Now);

        Assert.Equal(8, plan.Count);
        // Напоминание идёт первым по времени: своя цепочка из четырёх, затем цепочка начала.
        Assert.Equal(
            [Now.AddMinutes(30), Now.AddMinutes(30).AddSeconds(30), Now.AddMinutes(30).AddSeconds(60), Now.AddMinutes(30).AddSeconds(90)],
            plan.Take(4).Select(p => p.FireAt));
        Assert.Equal([$"{alarm.Id}-r30-0", $"{alarm.Id}-r30-1", $"{alarm.Id}-r30-2", $"{alarm.Id}-r30-3"], plan.Take(4).Select(p => p.Id));
        Assert.Equal(alarm.At, plan[4].FireAt);
        // Цепочка начала не изменилась: именно её ChainIds снимают при остановке звонка.
        Assert.Equal(IosNotificationPlan.ChainIds(alarm.Id), plan.Skip(4).Select(p => p.Id));
    }

    [Fact]
    public void Build_NotifyReminder_SingleEach()
    {
        var notify = new Alarm { At = Now.AddHours(1), Alert = AlertKind.Notify, ReminderMinutes = "30,10" };

        var plan = IosNotificationPlan.Build([notify], Now);

        Assert.Equal([notify.At.AddMinutes(-30), notify.At.AddMinutes(-10), notify.At], plan.Select(p => p.FireAt));
        Assert.Equal([$"{notify.Id}-r30-0", $"{notify.Id}-r10-0", $"{notify.Id}-0"], plan.Select(p => p.Id));
    }

    [Fact]
    public void Build_PastReminder_Skipped()
    {
        // Напоминание за 30 минут до начала через 20 минут уже в прошлом: остаётся только начало.
        var alarm = new Alarm { At = Now.AddMinutes(20), Alert = AlertKind.Notify, ReminderMinutes = "30" };

        var plan = IosNotificationPlan.Build([alarm], Now);

        Assert.Equal(alarm.At, Assert.Single(plan).FireAt);
    }

    [Fact]
    public void Build_RemindersShareLimit()
    {
        // Ежедневный Notify с напоминанием: два момента в сутки, лимит 16 моментов — 8 суток.
        var daily = new Alarm { At = Now.AddHours(1), Repeat = RepeatKind.Daily, Alert = AlertKind.Notify, ReminderMinutes = "30" };

        var plan = IosNotificationPlan.Build([daily], Now);

        Assert.Equal(IosNotificationPlan.MaxAlarms, plan.Count);
        Assert.Equal(daily.At.AddDays(7), plan.Max(p => p.FireAt));
        Assert.Equal(plan.Count, plan.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void ReminderChainIds_ReturnsOwnChainOnly()
    {
        var id = Guid.NewGuid();

        Assert.True(IosNotificationPlan.IsReminder($"{id}-r30-5"));
        Assert.False(IosNotificationPlan.IsReminder($"{id}-5"));
        Assert.Equal(
            [$"{id}-r30-4", $"{id}-r30-5", $"{id}-r30-6", $"{id}-r30-7"],
            IosNotificationPlan.ReminderChainIds($"{id}-r30-5"));
    }
}
