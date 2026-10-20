using Android.App;
using Android.Content;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Переустанавливает будильники после перезагрузки, обновления приложения и смены времени/пояса.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged, Intent.ActionMyPackageReplaced])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        // .NET кэширует TimeZoneInfo.Local: без сброса после смены пояса время пересчиталось бы по старому.
        TimeZoneInfo.ClearCachedData();
        var service = new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System);
        service.RescheduleAll();

        // WorkManager сохраняет работы и после перезагрузки, но после обновления или очистки данных перестраховываемся.
        if (context is not null && new SyncStore(FileSystem.AppDataDirectory).Load().Email is not null)
        {
            SyncWorker.Schedule(context);
        }
    }
}
