using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>Планирование уведомлений (IosNotificationPlan): цепочка time-sensitive для Ring, одно обычное для Notify и Beep.</summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    public const string AlarmIdKey = "alarm_id";

    /// <summary>День (yyyy-MM-dd), который открывает нажатие на уведомление Notify; признак уведомления распорядка.</summary>
    public const string OpenDayKey = "open_day";

    private readonly ISoundPlayer soundPlayer;

    public AlarmScheduler(ISoundPlayer soundPlayer)
    {
        this.soundPlayer = soundPlayer;
    }

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var center = UNUserNotificationCenter.Current;
        center.RemoveAllPendingNotificationRequests();

        foreach (var planned in IosNotificationPlan.Build(alarms, DateTime.Now))
        {
            // Notify и Beep — одно обычное уведомление (короткий сигнал): открывают распорядок и идут баннером.
            var isNotify = planned.Alarm.Alert is AlertKind.Notify or AlertKind.Beep;
            // Начало срабатывания: у напоминания FireAt раньше него на ReminderMinutes.
            var occurrence = planned.FireAt.AddMinutes(planned.ReminderMinutes);
            var content = new UNMutableNotificationContent
            {
                Title = string.IsNullOrWhiteSpace(planned.Alarm.Label) ? AppResources.DefaultLabel : planned.Alarm.Label,
                Body = (isNotify ? occurrence : planned.Alarm.At).ToString("HH:mm"),
                // Notify и Beep — обычное уведомление без прорыва через фокус: Active2, а не Active
                // (устаревший Active имеет значение 0 = Passive на стороне iOS).
                // Ring — TimeSensitive2, а не TimeSensitive: устаревший член имеет значение 3 = Critical на стороне iOS.
                InterruptionLevel = isNotify ? UNNotificationInterruptionLevel.Active2 : UNNotificationInterruptionLevel.TimeSensitive2,
                UserInfo = isNotify
                    ? NSDictionary.FromObjectsAndKeys(
                        [new NSString(planned.Alarm.Id.ToString()), new NSString(DateOnly.FromDateTime(occurrence).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))],
                        [new NSString(AlarmIdKey), new NSString(OpenDayKey)])
                    : NSDictionary.FromObjectAndKey(new NSString(planned.Alarm.Id.ToString()), new NSString(AlarmIdKey)),
            };
            if (isNotify)
            {
                // Beep — собственный звук beep.wav из бандла; Notify — системный звук по умолчанию.
                content.Sound = planned.Alarm.Alert == AlertKind.Beep
                    ? UNNotificationSound.GetSound("beep.wav")
                    : UNNotificationSound.Default;
            }
            else if (planned.Alarm.SoundEnabled)
            {
                content.Sound = UNNotificationSound.GetSound($"{planned.Alarm.SoundId}.wav");
            }

            var fireAt = planned.FireAt;
            var components = new NSDateComponents
            {
                Year = fireAt.Year,
                Month = fireAt.Month,
                Day = fireAt.Day,
                Hour = fireAt.Hour,
                Minute = fireAt.Minute,
                Second = fireAt.Second,
            };
            var request = UNNotificationRequest.FromIdentifier(
                planned.Id, content, UNCalendarNotificationTrigger.CreateTrigger(components, false));
            center.AddNotificationRequest(request, error =>
            {
                if (error is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"Chrono: уведомление {planned.Id} не запланировано: {error}");
                }
            });
        }
    }

    public void Cancel(Guid id)
    {
        var ids = IosNotificationPlan.ChainIds(id).ToArray();
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(ids);
    }

    public void StopRinging(Guid id)
    {
        this.soundPlayer.Stop();
        var ids = IosNotificationPlan.ChainIds(id).ToArray();
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(ids);
    }
}
