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
/// читает SyncStore только в RequestSoon и RunNowAsync, которые вызываются с главного потока.
/// </summary>
public sealed class SyncLauncher
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Func<CancellationToken, Task<SyncResult>> run;
    private readonly SyncStore store;
    private readonly TimeProvider time;
    private readonly object gate = new();
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

                    // Вход уже проверен в RequestSoon, а Reset гасит таймер, поэтому проверка Email здесь не нужна
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

    /// <summary>Выход из аккаунта: снимает ожидающий запуск и очищает состояние.</summary>
    public void Reset()
    {
        lock (this.gate)
        {
            this.timer?.Dispose();
            this.timer = null;
        }

        this.SetStatus(SyncStatus.Idle);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await this.run(ct);
            this.SetStatus(SyncStatus.Ok);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.SetStatus(StatusOf(ex));
        }
    }

    private void SetStatus(SyncStatus status)
    {
        this.Status = status;
        this.StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
