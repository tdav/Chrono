using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class AlarmServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-service-");
    private readonly FakeScheduler scheduler = new();
    private readonly FixedTime time = new(Now);
    private readonly AlarmStore store;
    private readonly AlarmService service;

    public AlarmServiceTests()
    {
        this.store = new AlarmStore(this.directory.FullName);
        this.service = new AlarmService(this.store, this.scheduler, this.time);
    }

    public void Dispose() => this.directory.Delete(recursive: true);

    [Fact]
    public void Save_PastTime_ReturnsFalseAndStoresNothing()
    {
        var saved = this.service.Save(new Alarm { At = Now.AddMinutes(-1) });

        Assert.False(saved);
        Assert.Empty(this.store.Load().Alarms);
        Assert.Empty(this.scheduler.Synced);
    }

    [Fact]
    public void Save_ExactlyNow_IsRejectedAsPast()
    {
        Assert.False(this.service.Save(new Alarm { At = Now }));
    }

    [Fact]
    public void Save_Future_StoresAndSyncsSchedulerWithIt()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Label = "Встреча", StartsOn = DateOnly.FromDateTime(Now) };

        var saved = this.service.Save(alarm);

        Assert.True(saved);
        Assert.Equal([alarm], this.store.Load().Alarms);
        Assert.Equal([alarm], Assert.Single(this.scheduler.Synced));
    }

    [Fact]
    public void Save_LocalKind_IsStoredAsWallClockWithoutOffset()
    {
        // DateTime.Today + TimeSpan даёт Kind=Local — так формирует время редактор.
        var at = DateTime.SpecifyKind(Now.AddHours(2), DateTimeKind.Local);

        this.service.Save(new Alarm { At = at });

        var json = File.ReadAllText(Path.Combine(this.directory.FullName, "alarms.json"));
        Assert.Contains("\"At\": \"2026-10-06T14:00:00\"", json);
        Assert.Equal(DateTimeKind.Unspecified, this.store.Load().Alarms.Single().At.Kind);
    }

    [Fact]
    public void Save_ExistingId_ReplacesInsteadOfDuplicating()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Label = "Старое" };
        this.service.Save(alarm);

        this.service.Save(alarm with { Label = "Новое", At = Now.AddHours(2) });

        var stored = Assert.Single(this.store.Load().Alarms);
        Assert.Equal("Новое", stored.Label);
        Assert.Equal(Now.AddHours(2), stored.At);
    }

    [Fact]
    public void GetAll_DisablesFiredAlarmsAndPersistsIt()
    {
        var fired = new Alarm { At = Now.AddMinutes(5) };
        var future = new Alarm { At = Now.AddHours(5) };
        this.service.Save(fired);
        this.service.Save(future);
        this.time.Now = Now.AddMinutes(5);

        var all = this.service.GetAll();

        Assert.False(all.Single(a => a.Id == fired.Id).IsEnabled);
        Assert.True(all.Single(a => a.Id == future.Id).IsEnabled);
        Assert.False(this.store.Load().Alarms.Single(a => a.Id == fired.Id).IsEnabled);
    }

    [Fact]
    public void GetAll_ReturnsAlarmsOrderedByTime()
    {
        var late = new Alarm { At = Now.AddHours(3) };
        var early = new Alarm { At = Now.AddHours(1) };
        this.service.Save(late);
        this.service.Save(early);

        Assert.Equal([early.Id, late.Id], this.service.GetAll().Select(a => a.Id));
    }

    [Fact]
    public void Toggle_EnablePastAlarm_ReturnsFalseAndKeepsItDisabled()
    {
        var alarm = new Alarm { At = Now.AddMinutes(5) };
        this.service.Save(alarm);
        this.time.Now = Now.AddMinutes(10);

        var toggled = this.service.Toggle(alarm.Id, enabled: true);

        Assert.False(toggled);
        Assert.False(this.store.Load().Alarms.Single().IsEnabled);
    }

    [Fact]
    public void Toggle_Disable_StoresAndSyncsDisabledAlarm()
    {
        var alarm = new Alarm { At = Now.AddHours(1) };
        this.service.Save(alarm);

        var toggled = this.service.Toggle(alarm.Id, enabled: false);

        Assert.True(toggled);
        Assert.False(this.store.Load().Alarms.Single().IsEnabled);
        Assert.False(this.scheduler.Synced[^1].Single().IsEnabled);
    }

    [Fact]
    public void Toggle_UnknownId_ReturnsFalse()
    {
        Assert.False(this.service.Toggle(Guid.NewGuid(), enabled: true));
    }

    [Fact]
    public void Delete_CancelsAndRemoves()
    {
        var keep = new Alarm { At = Now.AddHours(1), StartsOn = DateOnly.FromDateTime(Now) };
        var remove = new Alarm { At = Now.AddHours(2), StartsOn = DateOnly.FromDateTime(Now) };
        this.service.Save(keep);
        this.service.Save(remove);

        this.service.Delete(remove.Id);

        Assert.Equal([remove.Id], this.scheduler.Cancelled);
        Assert.Equal([keep], this.store.Load().Alarms);
        Assert.Equal([keep], this.scheduler.Synced[^1]);
    }

    [Fact]
    public void Save_SchedulerThrows_AlarmIsStillSavedAndErrorExposed()
    {
        this.scheduler.ThrowOnSync = true;
        var alarm = new Alarm { At = Now.AddHours(1), StartsOn = DateOnly.FromDateTime(Now) };

        var saved = this.service.Save(alarm);

        Assert.True(saved);
        Assert.Equal([alarm], this.store.Load().Alarms);
        Assert.IsType<InvalidOperationException>(this.service.SchedulingError);
    }

    [Fact]
    public void SchedulingError_ClearsAfterSuccessfulSync()
    {
        this.scheduler.ThrowOnSync = true;
        this.service.Save(new Alarm { At = Now.AddHours(1) });
        this.scheduler.ThrowOnSync = false;

        this.service.RescheduleAll();

        Assert.Null(this.service.SchedulingError);
    }

    [Fact]
    public void GetAll_CorruptFile_ReportsRecovery()
    {
        File.WriteAllText(Path.Combine(this.directory.FullName, "alarms.json"), "[[[");

        var all = this.service.GetAll();

        Assert.Empty(all);
        Assert.True(this.service.RecoveredFromCorruptFile);
    }

    [Fact]
    public void GetAll_LegacyJsonWithoutNewFields_IsRingWithStartDate()
    {
        var id = Guid.NewGuid();
        File.WriteAllText(
            Path.Combine(this.directory.FullName, "alarms.json"),
            $$"""[{"Id":"{{id}}","At":"2026-10-07T07:00:00","Label":"","SoundEnabled":true,"SoundId":"{{Sounds.Default}}","VibrationEnabled":true,"IsEnabled":true,"Repeat":1,"Days":0}]""");

        var alarm = Assert.Single(this.service.GetAll());

        Assert.Equal(AlertKind.Ring, alarm.Alert);
        Assert.Equal(0, alarm.DurationMinutes);
        Assert.Equal("bell", alarm.Icon);
        Assert.Equal(new DateOnly(2026, 10, 6), alarm.StartsOn);
        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
        Assert.Equal(new DateOnly(2026, 10, 6), this.store.Load().Alarms.Single().StartsOn);
    }

    [Fact]
    public void GetAll_LegacyRepeatingWithAtMovedToTomorrow_StartsOnToday()
    {
        // Старая ежедневная запись: At уже передвинут на завтра, но сегодня она срабатывала — день начала не позже сегодняшнего.
        File.WriteAllText(
            Path.Combine(this.directory.FullName, "alarms.json"),
            $$"""[{"Id":"{{Guid.NewGuid()}}","At":"2026-10-07T07:00:00","Label":"","SoundEnabled":true,"SoundId":"{{Sounds.Default}}","VibrationEnabled":true,"IsEnabled":true,"Repeat":1,"Days":0}]""");

        var alarm = Assert.Single(this.service.GetAll());

        Assert.Equal(DateOnly.FromDateTime(Now), alarm.StartsOn);
    }

    [Fact]
    public void GetAll_LegacyOneShot_StartsOnDateOfAt()
    {
        File.WriteAllText(
            Path.Combine(this.directory.FullName, "alarms.json"),
            $$"""[{"Id":"{{Guid.NewGuid()}}","At":"2026-10-07T07:00:00","Label":"","SoundEnabled":true,"SoundId":"{{Sounds.Default}}","VibrationEnabled":true,"IsEnabled":true,"Repeat":0,"Days":0}]""");

        Assert.Equal(new DateOnly(2026, 10, 7), Assert.Single(this.service.GetAll()).StartsOn);
    }

    [Fact]
    public void GetAll_FiredOnceThenDelete_BeforeExpiry_StaysEnabledAndUnmoved()
    {
        var at = Now.AddMinutes(-10);
        this.store.Save([new Alarm { At = at, Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Notify, DurationMinutes = 30 }]);

        var alarm = Assert.Single(this.service.GetAll());

        Assert.Equal(at, alarm.At);
        Assert.True(alarm.IsEnabled);
    }

    [Fact]
    public void GetAll_OnceThenDeleteAfterExpiry_IsRemoved()
    {
        this.store.Save([new Alarm { At = Now.AddMinutes(-31), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Notify, DurationMinutes = 30 }]);

        Assert.Empty(this.service.GetAll());
        Assert.Empty(this.store.Load().Alarms);
    }

    [Fact]
    public void GetAll_OnceThenDeleteDisabledAfterExpiry_IsRemoved()
    {
        this.store.Save([new Alarm { At = Now.AddMinutes(-31), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Notify, DurationMinutes = 30, IsEnabled = false }]);

        Assert.Empty(this.service.GetAll());
    }

    [Fact]
    public void GetAll_RingOnceThenDelete_KeptForRingTimeout()
    {
        var kept = new Alarm { At = Now.AddMinutes(-9), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Ring };
        var gone = new Alarm { At = Now.AddMinutes(-10), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Ring };
        this.store.Save([kept, gone]);

        Assert.Equal([kept.Id], this.service.GetAll().Select(a => a.Id));
    }

    [Fact]
    public void Save_WithoutStartsOn_SetsDateOfAt()
    {
        this.service.Save(new Alarm { At = Now.AddDays(3) });

        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(3)), this.store.Load().Alarms.Single().StartsOn);
    }

    [Fact]
    public void Save_WithStartsOn_KeepsIt()
    {
        var start = DateOnly.FromDateTime(Now.AddDays(1));
        this.service.Save(new Alarm { At = Now.AddDays(3), Repeat = RepeatKind.Daily, StartsOn = start });

        Assert.Equal(start, this.store.Load().Alarms.Single().StartsOn);
    }

    [Fact]
    public void Save_RepeatingAtBeforeStart_NormalizesAtToStartDay()
    {
        var startsOn = DateOnly.FromDateTime(Now).AddDays(6);

        var saved = this.service.Save(new Alarm { At = Now.AddHours(1), Repeat = RepeatKind.Daily, StartsOn = startsOn });

        Assert.True(saved);
        Assert.Equal(startsOn.ToDateTime(TimeOnly.FromDateTime(Now.AddHours(1))), this.store.Load().Alarms.Single().At);
    }

    [Fact]
    public void Toggle_EnableDailyWithFutureStart_MovesAtToStartDay()
    {
        var startsOn = DateOnly.FromDateTime(Now).AddDays(6);
        var alarm = new Alarm
        {
            At = new DateTime(2026, 10, 6, 7, 15, 0),
            Repeat = RepeatKind.Daily,
            IsEnabled = false,
            StartsOn = startsOn,
        };
        this.store.Save([alarm]);

        var toggled = this.service.Toggle(alarm.Id, enabled: true);

        Assert.True(toggled);
        Assert.Equal(startsOn.ToDateTime(new TimeOnly(7, 15)), this.store.Load().Alarms.Single().At);
    }

    [Fact]
    public void Toggle_EnablePassedOnceThenDelete_Rejected()
    {
        var alarm = new Alarm { At = Now.AddMinutes(5), Repeat = RepeatKind.OnceThenDelete, Alert = AlertKind.Notify, DurationMinutes = 30 };
        this.service.Save(alarm);
        this.service.Toggle(alarm.Id, enabled: false);
        this.time.Now = Now.AddMinutes(10);

        Assert.False(this.service.Toggle(alarm.Id, enabled: true));
        Assert.False(this.store.Load().Alarms.Single().IsEnabled);
    }

    [Fact]
    public void SetDone_PersistsAndDoesNotCallScheduler()
    {
        var id = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 6);

        this.service.SetDone(id, day, true);

        Assert.Contains(day, this.service.GetDone()[id]);
        Assert.Empty(this.scheduler.Synced);

        this.service.SetDone(id, day, false);

        Assert.DoesNotContain(day, this.service.GetDone()[id]);
        Assert.Empty(this.scheduler.Synced);
    }
}
