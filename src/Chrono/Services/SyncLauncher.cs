using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Services;

/// <summary>Итог последнего запуска синхронизации для экрана «Настройки».</summary>
public enum SyncStatus
{
    Idle,
    Ok,
    NoNetwork,
    SignInRequired,
    Failed,
}

/// <summary>
/// Запуск CalendarSync без MAUI: отложенный (слияние правок в окне 3 с) и немедленный. Без входа (SyncState.Email == null)
/// не делает ничего. Исключения синхронизации не выходят наружу, а превращаются в SyncStatus (отмена вызывающим — выходит).
/// run сам переносит работу на главный поток (SyncStore и AlarmStore не потокобезопасны); сам SyncLauncher
/// читает SyncStore только в RequestSoon, RunNowAsync и SignOutAsync, которые вызываются с главного потока.
/// </summary>
public sealed class SyncLauncher
{
    private const string TokenKey = "google-access-token";

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Func<CancellationToken, Task<SyncResult>> run;
    private readonly SyncStore store;
    private readonly TimeProvider time;
    private readonly object gate = new();
    private readonly List<Task> active = [];
    private CancellationTokenSource stop = new();
    private ITimer? timer;

    public SyncLauncher(Func<CancellationToken, Task<SyncResult>> run, SyncStore store, TimeProvider time)
    {
        this.run = run;
        this.store = store;
        this.time = time;
    }

    public SyncStatus Status { get; private set; }

    /// <summary>Вызывается с любого потока.</summary>
    public event EventHandler? StatusChanged;

    public static SyncStatus StatusOf(Exception exception) => exception switch
    {
        SignInRequiredException => SyncStatus.SignInRequired,
        HttpRequestException or OperationCanceledException => SyncStatus.NoNetwork,
        _ => SyncStatus.Failed,
    };

    /// <summary>Повторные вызовы, пока таймер не сработал, сливаются в один запуск.</summary>
    public void RequestSoon()
    {
        if (this.store.Load().Email is null)
        {
            return;
        }

        lock (this.gate)
        {
            this.timer ??= this.time.CreateTimer(
                _ =>
                {
                    lock (this.gate)
                    {
                        this.timer?.Dispose();
                        this.timer = null;
                    }

                    // Вход уже проверен в RequestSoon, а ResetAsync гасит таймер, поэтому проверка Email здесь не нужна
                    // (и читать SyncStore с потока таймера нельзя).
                    _ = this.RunAsync(CancellationToken.None);
                },
                null,
                Window,
                Timeout.InfiniteTimeSpan);
        }
    }

    public async Task RunNowAsync(CancellationToken ct = default)
    {
        if (this.store.Load().Email is null)
        {
            return;
        }

        await this.RunAsync(ct);
    }

    /// <summary>
    /// Снимает ожидающий запуск, отменяет идущие проходы и ждёт их конца (иначе проход дописал бы в SyncState токен
    /// и id календаря уже после выхода), затем очищает статус.
    /// </summary>
    public async Task ResetAsync()
    {
        Task[] running;
        lock (this.gate)
        {
            this.timer?.Dispose();
            this.timer = null;
            this.stop.Cancel();
            running = [.. this.active];
        }

        await Task.WhenAll(running);

        lock (this.gate)
        {
            // Отменённый источник больше не годится: новые проходы после повторного входа получают свежий.
            this.stop.Dispose();
            this.stop = new CancellationTokenSource();
        }

        this.SetStatus(SyncStatus.Idle);
    }

    /// <summary>
    /// Выход из аккаунта после успешного SignOutAsync платформы. Порядок важен: сначала конец идущего прохода,
    /// потом воркер и токен, потом очистка состояния, потом отвязка записей. Так проход не воскресит очищенное,
    /// а при входе под другим аккаунтом очередь и id событий прежнего не попадут в новый календарь.
    /// Записи остаются на телефоне; первый проход нового аккаунта отправит их как несвязанные (в тот же аккаунт —
    /// свяжет по chronoId).
    /// </summary>
    public async Task SignOutAsync(AlarmService alarms, Action cancelWorker, IMemoryCache cache)
    {
        await this.ResetAsync();
        cancelWorker();
        cache.Remove(TokenKey);
        this.store.Save(this.store.Load() with
        {
            Email = null,
            SyncToken = null,
            CalendarId = null,
            LastSyncUtc = null,
            Pending = [],
            IgnoredEventIds = [],
        });

        // RequestSoon, вызванный во время ResetAsync или cancelWorker (Email тогда ещё был), успел завести таймер:
        // гасим его под замком уже после очистки, иначе он сработал бы в очищенном состоянии.
        lock (this.gate)
        {
            this.timer?.Dispose();
            this.timer = null;
        }

        foreach (var alarm in alarms.GetAll().Where(a => a.GoogleEventId is not null))
        {
            // false — время записи уже прошло, Save её не принял: id останется, это не ошибка.
            // ForeignSeries сбрасывается вместе с id: иначе запись чужой серии не ушла бы и в новый аккаунт.
            alarms.Save(alarm with { GoogleEventId = null, ForeignSeries = false }, fromSync: true);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        CancellationTokenSource linked;
        var done = new TaskCompletionSource();
        lock (this.gate)
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(ct, this.stop.Token);
            this.active.Add(done.Task);
        }

        try
        {
            await this.run(linked.Token);
            this.SetStatus(SyncStatus.Ok);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Отменён выходом (ResetAsync): статус не трогаем, его очистит сам выход.
        }
        catch (Exception ex)
        {
            this.SetStatus(StatusOf(ex));
        }
        finally
        {
            lock (this.gate)
            {
                this.active.Remove(done.Task);
            }

            linked.Dispose();
            done.SetResult();
        }
    }

    private void SetStatus(SyncStatus status)
    {
        this.Status = status;
        this.StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
