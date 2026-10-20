using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using Chrono.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Platform;

/// <summary>
/// Периодическая синхронизация с Google Календарём в фоне (раз в 15 мин, только при сети). Ставится после входа
/// и после перезагрузки (BootReceiver), снимается при выходе. Имя Java-класса задано явно: WorkManager хранит его
/// в своей базе, и сгенерированное имя с хэшем сломало бы сохранённую работу при переименовании типа или сборки.
/// </summary>
[Register("com.chrono.alarm.SyncWorker")]
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
        var failed = false;
        try
        {
            // SyncStore и AlarmStore не потокобезопасны, а CalendarSync рассчитывает на продолжения на главном потоке,
            // поэтому всё, включая чтение состояния, идёт на главном потоке, а поток WorkManager ждёт синхронно.
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Через SyncLauncher проход виден выходу (SignOutAsync отменяет его и ждёт конца), а в обход — дописал бы
                // SyncToken и CalendarId уже после очистки. Статус SyncLauncher не бросает, а превращает в SyncStatus.
                if (IPlatformApplication.Current?.Services.GetService<SyncLauncher>() is { } launcher)
                {
                    await launcher.RunNowAsync();
                    failed = launcher.Status is SyncStatus.NoNetwork or SyncStatus.Failed;
                    return;
                }

                var store = new SyncStore(FileSystem.AppDataDirectory);
                if (store.Load().Email is null)
                {
                    // Выход мог не успеть снять работу.
                    return;
                }

                // Контейнера нет (как в BootReceiver): ручная сборка; выход такой проход не отменяет.
                var sync = new CalendarSync(
                    new GoogleCalendarClient(new HttpClient(), new MemoryCache(new MemoryCacheOptions()), new GoogleAuth()),
                    new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System, store),
                    store,
                    TimeProvider.System);
                await sync.SyncAsync(CancellationToken.None);
            }).GetAwaiter().GetResult();
            // Нет сети или сбой сервера: следующая попытка по расписанию.
            return failed ? Result.InvokeRetry()! : Result.InvokeSuccess()!;
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
