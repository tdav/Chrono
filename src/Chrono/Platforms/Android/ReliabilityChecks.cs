using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Chrono.Services;

namespace Chrono.Platform;

public sealed class ReliabilityChecks : IReliabilityChecks
{
    public Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync()
    {
        var context = global::Android.App.Application.Context;
        var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var power = (PowerManager)context.GetSystemService(Context.PowerService)!;

        IReadOnlyList<ReliabilityStatus> result =
        [
            new(ReliabilityItem.Notifications, notifications.AreNotificationsEnabled()),
            new(ReliabilityItem.ExactAlarms, !OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms()),
            new(ReliabilityItem.FullScreen, !OperatingSystem.IsAndroidVersionAtLeast(34) || notifications.CanUseFullScreenIntent()),
            new(ReliabilityItem.Battery, power.IsIgnoringBatteryOptimizations(context.PackageName)),
        ];
        return Task.FromResult(result);
    }

    public async Task RequestPermissionsAsync()
    {
        await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    public void Fix(ReliabilityItem item)
    {
        var context = global::Android.App.Application.Context;
        var packageUri = global::Android.Net.Uri.Parse($"package:{context.PackageName}");
        var intent = item switch
        {
            ReliabilityItem.Notifications => new Intent(Settings.ActionAppNotificationSettings)
                .PutExtra(Settings.ExtraAppPackage, context.PackageName)!,
            ReliabilityItem.ExactAlarms when OperatingSystem.IsAndroidVersionAtLeast(31) =>
                new Intent(Settings.ActionRequestScheduleExactAlarm, packageUri),
            ReliabilityItem.FullScreen when OperatingSystem.IsAndroidVersionAtLeast(34) =>
                new Intent(Settings.ActionManageAppUseFullScreenIntent, packageUri),
            ReliabilityItem.Battery => new Intent(Settings.ActionIgnoreBatteryOptimizationSettings),
            _ => new Intent(Settings.ActionApplicationDetailsSettings, packageUri),
        };
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
}
