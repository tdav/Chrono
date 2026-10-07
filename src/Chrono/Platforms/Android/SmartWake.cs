using Android.Content;
using AndroidX.Health.Connect.Client.Contracts;
using Chrono.Services;

namespace Chrono.Platform;

public sealed class SmartWake : ISmartWake
{
    // Android 14+: Health Connect встроен в систему, контракт отдаёт псевдо-intent AndroidX,
    // а сами health-разрешения — обычные runtime-разрешения.
    private const string ActionRequestPermissions = "androidx.activity.result.contract.action.REQUEST_PERMISSIONS";

    public const int PermissionRequestCode = 4711;

    private static TaskCompletionSource<bool>? activityResult;

    public bool IsSupported => true;

    /// <summary>Вызывается из MainActivity.OnActivityResult (экран разрешений Health Connect на Android 13 и ниже).</summary>
    public static void OnActivityResult(int requestCode)
    {
        if (requestCode == PermissionRequestCode)
        {
            activityResult?.TrySetResult(true);
        }
    }

    public async Task<SmartWakeAccess> GetAccessAsync()
    {
        var context = global::Android.App.Application.Context;
        if (!HealthConnect.IsAvailable(context))
        {
            return SmartWakeAccess.HealthConnectMissing;
        }

        // Без чтения в фоне данные часов недоступны в окне при выключенном экране — доступ неполный.
        var granted = await HealthConnect.GetGrantedAsync(context);
        return HealthConnect.Permissions.All(granted.Contains) ? SmartWakeAccess.Granted : SmartWakeAccess.NoPermission;
    }

    public async Task<SmartWakeAccess> RequestAccessAsync()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity!;
        if (!HealthConnect.IsAvailable(activity))
        {
            // Android 13 и ниже: Health Connect — отдельное приложение из Google Play.
            var market = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse($"market://details?id={HealthConnect.ProviderPackage}"));
            market.AddFlags(ActivityFlags.NewTask);
            activity.StartActivity(market);
            return SmartWakeAccess.HealthConnectMissing;
        }

        var access = await this.GetAccessAsync();
        if (access == SmartWakeAccess.Granted)
        {
            return access;
        }

        // Типизированный CreateIntentImpl: обобщённый CreateIntent(Context, Object) в биндинге
        // не принимает Java.Util.HashSet (проверено на эмуляторе: ArgumentNullException внутри).
        var intent = new HealthPermissionsRequestContract().CreateIntentImpl(activity, HealthConnect.Permissions.ToList());
        if (intent.Action == ActionRequestPermissions)
        {
            await Permissions.RequestAsync<HealthConnectPermission>();
        }
        else
        {
            activityResult = new TaskCompletionSource<bool>();
            activity.StartActivityForResult(intent, PermissionRequestCode);
            await activityResult.Task;
        }

        return await this.GetAccessAsync();
    }

    /// <summary>Health-разрешения Android 14+ через стандартный механизм запроса MAUI.</summary>
    private sealed class HealthConnectPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            HealthConnect.Permissions.Select(p => (p, true)).ToArray();
    }
}
