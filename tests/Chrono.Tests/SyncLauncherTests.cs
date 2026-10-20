using System.Net;
using Chrono.Services;

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
    private Func<Task<SyncResult>> action = () => Task.FromResult(new SyncResult(0, 0, 0, false));
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
            _ =>
            {
                this.runs++;
                return this.action();
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
    [InlineData(typeof(HttpRequestException), SyncStatus.NoNetwork)]
    [InlineData(typeof(TaskCanceledException), SyncStatus.NoNetwork)]
    [InlineData(typeof(InvalidOperationException), SyncStatus.Failed)]
    public async Task RunNow_Exception_BecomesStatusNotThrow(Type exceptionType, SyncStatus expected)
    {
        var launcher = this.Create();
        this.action = () => throw (Exception)Activator.CreateInstance(exceptionType)!;

        await launcher.RunNowAsync();

        Assert.Equal(expected, launcher.Status);
    }

    [Fact]
    public async Task RunNow_CancelledByCaller_IsRethrown()
    {
        var launcher = this.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        this.action = () => throw new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launcher.RunNowAsync(cts.Token));
        Assert.Equal(SyncStatus.Idle, launcher.Status);
    }

    [Fact]
    public async Task Reset_CancelsPendingTimerAndClearsStatus()
    {
        var launcher = this.Create();
        this.action = () => throw new HttpRequestException("нет сети", null, HttpStatusCode.ServiceUnavailable);
        await launcher.RunNowAsync();
        launcher.RequestSoon();

        launcher.Reset();

        Assert.True(Assert.Single(this.time.Timers).Disposed);
        Assert.Equal(SyncStatus.Idle, launcher.Status);

        // После сброса таймер заводится заново, а не считается «уже ожидающим».
        launcher.RequestSoon();
        Assert.Equal(2, this.time.Timers.Count);
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
