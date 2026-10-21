using System.Globalization;
using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>
/// Нажатие на уведомление и уведомление при открытом приложении → RingPage;
/// уведомление Notify или Beep (есть open_day) показывается баннером и открывает «Распорядок» на нужном дне.
/// Назначается в FinishedLaunching, чтобы ловить и запуск приложения нажатием.
/// </summary>
public sealed class NotificationDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        // Ключ open_day есть — это уведомление Notify; нечитаемый день не превращает его в сигнал (путь Ring).
        var openDayText = notification.Request.Content.UserInfo[AlarmScheduler.OpenDayKey]?.ToString();
        if (openDayText is not null)
        {
            completionHandler(DateOnly.TryParseExact(openDayText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.List
                : UNNotificationPresentationOptions.None);
            return;
        }

        // Приложение на экране: баннер не показываем, сразу экран сигнала с мелодией в цикле.
        if (Guid.TryParse(notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var services = IPlatformApplication.Current!.Services;
            var alarm = services.GetRequiredService<AlarmStore>().Load().Alarms.FirstOrDefault(a => a.Id == alarmId);
            // Напоминание снимает только свою цепочку: цепочка начала ещё должна сработать.
            var identifier = notification.Request.Identifier;
            var ids = (IosNotificationPlan.IsReminder(identifier) ? IosNotificationPlan.ReminderChainIds(identifier) : IosNotificationPlan.ChainIds(alarmId)).ToArray();
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
        var openDayText = response.Notification.Request.Content.UserInfo[AlarmScheduler.OpenDayKey]?.ToString();
        if (openDayText is not null)
        {
            if (DateOnly.TryParseExact(openDayText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var openDay))
            {
                ScheduleLauncher.Request(openDay);
            }

            completionHandler();
            return;
        }

        if (Guid.TryParse(response.Notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var identifier = response.Notification.Request.Identifier;
            var ids = (IosNotificationPlan.IsReminder(identifier) ? IosNotificationPlan.ReminderChainIds(identifier) : IosNotificationPlan.ChainIds(alarmId)).ToArray();
            center.RemovePendingNotificationRequests(ids);
            center.RemoveDeliveredNotifications(ids);
            RingLauncher.Request(alarmId);
        }

        completionHandler();
    }
}
