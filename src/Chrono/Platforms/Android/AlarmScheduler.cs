using Android.App;
using Android.Content;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Планирование через AlarmManager.SetAlarmClock: точный, не ограничивается Doze.</summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var now = DateTime.Now;

        foreach (var alarm in alarms)
        {
            var operation = AlarmReceiver.CreatePendingIntent(context, alarm.Id);
            if (alarm.IsEnabled && alarm.At > now)
            {
                // Локальное «настенное» время → абсолютное по текущему поясу.
                var triggerAtMs = new DateTimeOffset(DateTime.SpecifyKind(alarm.At, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                var showIntent = PendingIntent.GetActivity(
                    context, 0, new Intent(context, typeof(MainActivity)), PendingIntentFlags.Immutable);
                manager.SetAlarmClock(new AlarmManager.AlarmClockInfo(triggerAtMs, showIntent), operation);
            }
            else
            {
                manager.Cancel(operation);
            }
        }
    }

    public void Cancel(Guid id)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id));
    }

    public void StopRinging(Guid id)
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(AlarmRingService)));

        // Экран сигнала больше не должен показываться поверх блокировки.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity && OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            activity.SetShowWhenLocked(false);
            activity.SetTurnScreenOn(false);
        }
    }
}
