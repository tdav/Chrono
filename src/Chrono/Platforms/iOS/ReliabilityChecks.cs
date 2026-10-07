using Chrono.Services;
using UserNotifications;

namespace Chrono.Platform;

public sealed class ReliabilityChecks : IReliabilityChecks
{
    public async Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync()
    {
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        return
        [
            new(ReliabilityItem.Notifications, settings.AuthorizationStatus == UNAuthorizationStatus.Authorized),
            new(ReliabilityItem.TimeSensitive, settings.TimeSensitiveSetting == UNNotificationSetting.Enabled),
        ];
    }

    public async Task RequestPermissionsAsync()
    {
        await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
    }

    public void Fix(ReliabilityItem item)
    {
        AppInfo.Current.ShowSettingsUI();
    }
}
