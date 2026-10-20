using System.Net;
using Chrono.Models;
using Chrono.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Tests;

/// <summary>Часы, у которых таймеры не тикают сами: тест вызывает Fire().</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    public List<ManualTimer> Timers { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, dueTime);
        this.Timers.Add(timer);
        return timer;
    }
}

public sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
{
    public TimeSpan DueTime { get; } = dueTime;

    public bool Disposed { get; private set; }

    public void Fire() => callback(state);

    public bool Change(TimeSpan dueTime, TimeSpan period) => true;

    public void Dispose() => this.Disposed = true;

    public ValueTask DisposeAsync()
    {
        this.Disposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class SyncLauncherTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-launcher-");
    private readonly SyncStore store;
    private readonly ManualTimeProvider time = new();
    private Func<CancellationToken, Task<SyncResult>> action = _ => Task.FromResult(new SyncResult(0, 0, 0, false));
    private int runs;

    public SyncLauncherTests() => this.store = new SyncStore(this.directory.FullName);

    public void Dispose() => this.directory.Delete(recursive: true);

    private SyncLauncher Create(bool signedIn = true)
    {
        if (signedIn)
        {
            this.store.Save(this.store.Load() with { Email = "a@b.c" });
        }

        return new SyncLauncher(
            ct =>
            {
                this.runs++;
                return this.action(ct);
            },
            this.store,
            this.time);
    }

    [Fact]
    public async Task RequestSoon_RepeatedCallsInWindow_MergeIntoOneRun()
    {
        var launcher = this.Create();

        launcher.RequestSoon();
        launcher.RequestSoon();
        launcher.RequestSoon();

        var timer = Assert.Single(this.time.Timers);
        Assert.Equal(TimeSpan.FromSeconds(3), timer.DueTime);
        Assert.Equal(0, this.runs);

        timer.Fire();
        await WaitAsync(() => this.runs == 1);

        // После срабатывания окно закрыто: следующая правка снова заводит таймер.
        launcher.RequestSoon();
        Assert.Equal(2, this.time.Timers.Count);
        Assert.Equal(1, this.runs);
    }

    [Fact]
    public async Task NotSignedIn_DoesNothing()
    {
        var launcher = this.Create(signedIn: false);

        launcher.RequestSoon();
        await launcher.RunNowAsync();

        Assert.Empty(this.time.Timers);
        Assert.Equal(0, this.runs);
        Assert.Equal(SyncStatus.Idle, launcher.Status);
    }

    [Fact]
    public async Task RunNow_Success_SetsOkAndRaisesChanged()
    {
        var launcher = this.Create();
        var raised = 0;
        launcher.StatusChanged += (_, _) => raised++;

        await launcher.RunNowAsync();

        Assert.Equal(SyncStatus.Ok, launcher.Status);
        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(typeof(SignInRequiredException), SyncStatus.SignInRequired)]
    [InlineData(typeof(SyncScopesMissingException), SyncStatus.SignInRequired)]
    [InlineData(typeof(HttpRequestException), SyncStatus.NoNetwork)]
    [InlineData(typeof(TaskCanceledException), SyncStatus.NoNetwork)]
    [InlineData(typeof(InvalidOperationException), SyncStatus.Failed)]
    public async Task RunNow_Exception_BecomesStatusNotThrow(Type exceptionType, SyncStatus expected)
    {
        var launcher = this.Create();
        this.action = _ => throw (Exception)Activator.CreateInstance(exceptionType)!;

        await launcher.RunNowAsync();

        Assert.Equal(expected, launcher.Status);
    }

    [Fact]
    public async Task RunNow_CancelledByCaller_IsRethrown()
    {
        var launcher = this.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        this.action = _ => throw new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launcher.RunNowAsync(cts.Token));
        Assert.Equal(SyncStatus.Idle, launcher.Status);
    }

    [Fact]
    public async Task ResetAsync_CancelsPendingTimerAndClearsStatus()
    {
        var launcher = this.Create();
        this.action = _ => throw new HttpRequestException("нет сети", null, HttpStatusCode.ServiceUnavailable);
        await launcher.RunNowAsync();
        launcher.RequestSoon();

        await launcher.ResetAsync();

        Assert.True(Assert.Single(this.time.Timers).Disposed);
        Assert.Equal(SyncStatus.Idle, launcher.Status);

        // После сброса таймер заводится заново, а не считается «уже ожидающим».
        launcher.RequestSoon();
        Assert.Equal(2, this.time.Timers.Count);
    }

    [Fact]
    public async Task ResetAsync_CancelsRunningSyncWaitsForItAndLeavesIdle()
    {
        var launcher = this.Create();
        var started = new TaskCompletionSource();
        var finished = false;
        this.action = async ct =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                // Проход «дописывает» состояние уже после отмены, как настоящий CalendarSync.
                await Task.Delay(50);
                finished = true;
                throw;
            }

            return new SyncResult(0, 0, 0, false);
        };
        var seen = new List<SyncStatus>();
        launcher.StatusChanged += (_, _) => seen.Add(launcher.Status);
        var running = launcher.RunNowAsync();
        await started.Task;

        await launcher.ResetAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(finished);
        await running;
        Assert.Equal([SyncStatus.Idle], seen);
        Assert.Equal(SyncStatus.Idle, launcher.Status);

        // После сброса новые проходы идут с неотменённым токеном.
        this.action = ct => Task.FromResult(ct.IsCancellationRequested ? throw new InvalidOperationException() : new SyncResult(0, 0, 0, false));
        await launcher.RunNowAsync();
        Assert.Equal(SyncStatus.Ok, launcher.Status);
    }

    [Fact]
    public async Task SignOutAsync_WaitsForRunThenClearsStateTokenAndEventIds()
    {
        var launcher = this.Create();
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarmStore = new AlarmStore(this.directory.FullName);
        var alarms = new AlarmService(alarmStore, new FakeScheduler(), new FixedTime(now), this.store);
        var linked = new Alarm { At = now.AddHours(1), GoogleEventId = "ev1" };
        var plain = new Alarm { At = now.AddHours(2) };
        var past = new Alarm { At = now.AddHours(-1), GoogleEventId = "ev-past", StartsOn = DateOnly.FromDateTime(now) };
        alarmStore.Save([linked, plain, past]);
        this.store.Save(this.store.Load() with
        {
            CalendarId = "cal",
            SyncToken = "tok",
            LastSyncUtc = now,
            Pending = [new PendingChange(linked.Id, SyncOp.Upsert, "ev1", now)],
            IgnoredEventIds = ["ev2"],
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("google-access-token", "secret");
        var events = new List<string>();
        var started = new TaskCompletionSource();
        this.action = async ct =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                // Опоздавшая запись состояния прежнего аккаунта.
                await Task.Delay(50);
                this.store.Save(this.store.Load() with { SyncToken = "late", CalendarId = "late" });
                events.Add("late-write");
                throw;
            }

            return new SyncResult(0, 0, 0, false);
        };
        var seen = new List<SyncStatus>();
        launcher.StatusChanged += (_, _) => seen.Add(launcher.Status);
        var running = launcher.RunNowAsync();
        await started.Task;

        await launcher.SignOutAsync(alarms, () => events.Add("worker"), cache).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["late-write", "worker"], events);
        var state = this.store.Load();
        Assert.Equal((null, null, null, null), (state.Email, state.SyncToken, state.CalendarId, state.LastSyncUtc));
        Assert.Empty(state.Pending);
        Assert.Empty(state.IgnoredEventIds);
        Assert.False(cache.TryGetValue("google-access-token", out _));
        var saved = alarmStore.Load().Alarms.ToDictionary(a => a.Id);
        Assert.Null(saved[linked.Id].GoogleEventId);
        Assert.Null(saved[plain.Id].GoogleEventId);
        Assert.Equal("ev-past", saved[past.Id].GoogleEventId);
        Assert.Equal(3, saved.Count);
        Assert.Equal([SyncStatus.Idle], seen);
        Assert.Equal(SyncStatus.Idle, launcher.Status);
        await running;
    }

    [Fact]
    public async Task SignOutAsync_TimerArmedDuringSignOut_IsDisposed()
    {
        var launcher = this.Create();
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarms = new AlarmService(new AlarmStore(this.directory.FullName), new FakeScheduler(), new FixedTime(now), this.store);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        // Правка пользователя в момент выхода: Email ещё не очищен, поэтому RequestSoon заводит таймер уже после ResetAsync.
        await launcher.SignOutAsync(alarms, launcher.RequestSoon, cache);

        Assert.True(Assert.Single(this.time.Timers).Disposed);
        Assert.Equal(0, this.runs);
    }

    [Fact]
    public async Task SignOutAsync_ResetsForeignSeriesTogetherWithEventId()
    {
        var launcher = this.Create();
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarmStore = new AlarmStore(this.directory.FullName);
        var alarms = new AlarmService(alarmStore, new FakeScheduler(), new FixedTime(now), this.store);
        var foreign = new Alarm { At = now.AddHours(1), GoogleEventId = "ev1", ForeignSeries = true };
        alarmStore.Save([foreign]);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await launcher.SignOutAsync(alarms, () => { }, cache);

        var saved = Assert.Single(alarmStore.Load().Alarms);
        Assert.Null(saved.GoogleEventId);
        Assert.False(saved.ForeignSeries);
    }

    [Fact]
    public async Task SignOutAsync_RemovesQuarantinedSyncFile()
    {
        var launcher = this.Create();
        var bad = Path.Combine(this.directory.FullName, "sync.json.bad");
        File.WriteAllText(bad, "{ старый email и очередь");
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarms = new AlarmService(new AlarmStore(this.directory.FullName), new FakeScheduler(), new FixedTime(now), this.store);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await launcher.SignOutAsync(alarms, () => { }, cache);

        Assert.False(File.Exists(bad));
    }

    private (AlarmService Alarms, AlarmStore AlarmStore, Alarm Linked, MemoryCache Cache) SeedLinkedState()
    {
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarmStore = new AlarmStore(this.directory.FullName);
        var alarms = new AlarmService(alarmStore, new FakeScheduler(), new FixedTime(now), this.store);
        var linked = new Alarm { At = now.AddHours(1), GoogleEventId = "ev1" };
        alarmStore.Save([linked]);
        this.store.Save(this.store.Load() with
        {
            CalendarId = "cal",
            SyncToken = "tok",
            Pending = [new PendingChange(linked.Id, SyncOp.Upsert, "ev1", now)],
            IgnoredEventIds = ["ev2"],
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(GoogleCalendarClient.TokenKey, "secret");
        return (alarms, alarmStore, linked, cache);
    }

    [Fact]
    public async Task CompleteSignInAsync_SameAccountAfterSignInRequired_KeepsQueueAndLinks()
    {
        var launcher = this.Create();
        var (alarms, alarmStore, linked, cache) = this.SeedLinkedState();
        using var disposeCache = cache;
        this.action = _ => throw new SignInRequiredException();
        await launcher.RunNowAsync();
        Assert.Equal(SyncStatus.SignInRequired, launcher.Status);
        var cancelled = false;

        await launcher.CompleteSignInAsync("A@B.C", alarms, () => cancelled = true, cache);

        var state = this.store.Load();
        Assert.Equal(("a@b.c", "cal", "tok"), (state.Email, state.CalendarId, state.SyncToken));
        Assert.Single(state.Pending);
        Assert.Equal(["ev2"], state.IgnoredEventIds);
        Assert.Equal("ev1", alarmStore.Load().Alarms.Single(a => a.Id == linked.Id).GoogleEventId);
        Assert.False(cancelled);
        Assert.Equal(SyncStatus.Idle, launcher.Status);
    }

    [Fact]
    public async Task CompleteSignInAsync_OtherAccount_ClearsStateAndLinksAndSavesNewEmail()
    {
        var launcher = this.Create();
        var (alarms, alarmStore, linked, cache) = this.SeedLinkedState();
        using var disposeCache = cache;
        var cancelled = false;

        await launcher.CompleteSignInAsync("new@b.c", alarms, () => cancelled = true, cache);

        var state = this.store.Load();
        Assert.Equal(("new@b.c", null, null), (state.Email, state.CalendarId, state.SyncToken));
        Assert.Empty(state.Pending);
        Assert.Empty(state.IgnoredEventIds);
        Assert.Null(alarmStore.Load().Alarms.Single(a => a.Id == linked.Id).GoogleEventId);
        Assert.False(cache.TryGetValue(GoogleCalendarClient.TokenKey, out _));
        Assert.True(cancelled);
    }

    [Fact]
    public async Task CompleteSignInAsync_FirstSignIn_SavesEmail()
    {
        var launcher = this.Create(signedIn: false);
        var now = new DateTime(2026, 10, 6, 12, 0, 0);
        var alarms = new AlarmService(new AlarmStore(this.directory.FullName), new FakeScheduler(), new FixedTime(now), this.store);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await launcher.CompleteSignInAsync("a@b.c", alarms, () => { }, cache);

        Assert.Equal("a@b.c", this.store.Load().Email);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
