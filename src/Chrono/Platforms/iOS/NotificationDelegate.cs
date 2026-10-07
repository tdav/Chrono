using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>
/// Нажатие на уведомление и уведомление при открытом приложении → RingPage.
/// Назначается в FinishedLaunching, чтобы ловить и запуск приложения нажатием.
/// </summary>
public sealed class NotificationDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        // Приложение на экране: баннер не показываем, сразу экран сигнала с мелодией в цикле.
        if (Guid.TryParse(notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var services = IPlatformApplication.Current!.Services;
            var alarm = services.GetRequiredService<AlarmStore>().Load().Alarms.FirstOrDefault(a => a.Id == alarmId);
            var ids = IosNotificationPlan.ChainIds(alarmId).ToArray();
            center.RemovePendingNotificationRequests(ids);
            center.RemoveDeliveredNotifications(ids);

            if (alarm is { SoundEnabled: true })
            {
                services.GetRequiredService<ISoundPlayer>().Play(alarm.SoundId, loop: true);
            }

            if (alarm is { VibrationEnabled: true })
            {
                Vibration.Default.Vibrate(TimeSpan.FromSeconds(1));
            }

            RingLauncher.Request(alarmId);
        }

        completionHandler(UNNotificationPresentationOptions.None);
    }

    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        if (Guid.TryParse(response.Notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var ids = IosNotificationPlan.ChainIds(alarmId).ToArray();
            center.RemovePendingNotificationRequests(ids);
            center.RemoveDeliveredNotifications(ids);
            RingLauncher.Request(alarmId);
        }

        completionHandler();
    }
}
