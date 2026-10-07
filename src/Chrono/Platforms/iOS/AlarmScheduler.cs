using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>Планирование цепочкой time-sensitive уведомлений (IosNotificationPlan).</summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    public const string AlarmIdKey = "alarm_id";

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
            var content = new UNMutableNotificationContent
            {
                Title = string.IsNullOrWhiteSpace(planned.Alarm.Label) ? AppResources.DefaultLabel : planned.Alarm.Label,
                Body = planned.Alarm.At.ToString("HH:mm"),
                // TimeSensitive2, а не TimeSensitive: устаревший член имеет значение 3 = Critical на стороне iOS.
                InterruptionLevel = UNNotificationInterruptionLevel.TimeSensitive2,
                UserInfo = NSDictionary.FromObjectAndKey(new NSString(planned.Alarm.Id.ToString()), new NSString(AlarmIdKey)),
            };
            if (planned.Alarm.SoundEnabled)
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
