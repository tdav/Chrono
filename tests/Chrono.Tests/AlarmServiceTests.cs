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
        var alarm = new Alarm { At = Now.AddHours(1), Label = "Встреча" };

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
        var keep = new Alarm { At = Now.AddHours(1) };
        var remove = new Alarm { At = Now.AddHours(2) };
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
        var alarm = new Alarm { At = Now.AddHours(1) };

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
}
