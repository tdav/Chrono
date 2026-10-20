using Android.Content;
using AndroidX.Work;
using Chrono.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Platform;

/// <summary>
/// Периодическая синхронизация с Google Календарём в фоне (раз в 15 мин, только при сети). Ставится после входа
/// и после перезагрузки (BootReceiver), снимается при выходе.
/// </summary>
public sealed class SyncWorker : Worker
{
    private const string UniqueName = "chrono-sync";

    public SyncWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    /// <summary>Keep: повторный вызов (BootReceiver, повторный вход) не сбрасывает уже идущий отсчёт периода.</summary>
    public static void Schedule(Context context)
    {
        var request = new PeriodicWorkRequest.Builder(Java.Lang.Class.FromType(typeof(SyncWorker)), 15, Java.Util.Concurrent.TimeUnit.Minutes!)
            .SetConstraints(new Constraints.Builder().SetRequiredNetworkType(NetworkType.Connected!).Build())
            .Build();
        WorkManager.GetInstance(context).EnqueueUniquePeriodicWork(UniqueName, ExistingPeriodicWorkPolicy.Keep!, request);
    }

    public static void Cancel(Context context) => WorkManager.GetInstance(context).CancelUniqueWork(UniqueName);

    public override Result DoWork()
    {
        try
        {
            // SyncStore и AlarmStore не потокобезопасны, а CalendarSync рассчитывает на продолжения на главном потоке,
            // поэтому всё, включая чтение состояния, идёт на главном потоке, а поток WorkManager ждёт синхронно.
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var store = new SyncStore(FileSystem.AppDataDirectory);
                if (store.Load().Email is null)
                {
                    // Выход мог не успеть снять работу.
                    return;
                }

                // Процесс приложения уже поднят MauiApplication.OnCreate — берём его зависимости. Ручная сборка — на случай,
                // если контейнера нет (как в BootReceiver).
                var sync = IPlatformApplication.Current?.Services.GetService<CalendarSync>()
                    ?? new CalendarSync(
                        new GoogleCalendarClient(new HttpClient(), new MemoryCache(new MemoryCacheOptions()), new GoogleAuth()),
                        new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System, store),
                        store,
                        TimeProvider.System);
                await sync.SyncAsync(CancellationToken.None);
            }).GetAwaiter().GetResult();
            return Result.InvokeSuccess()!;
        }
        catch (SignInRequiredException)
        {
            // Повторять бессмысленно: войти заново может только пользователь.
            return Result.InvokeSuccess()!;
        }
        catch (Exception)
        {
            // Нет сети, сбой сервера: следующая попытка по расписанию.
            return Result.InvokeRetry()!;
        }
    }
}
