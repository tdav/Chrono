using Android.App;
using Android.Content;
using Android.OS;

namespace Chrono.Platform;

/// <summary>
/// Android 14+: Health Connect ссылается сюда из своих настроек («как приложение использует данные»).
/// Активити защищена системным разрешением, поэтому отдельная от MainActivity; она лишь открывает
/// экран настроек, где описано, какие данные часов читаются и зачем.
/// </summary>
[Activity(Exported = true, NoHistory = true, Permission = "android.permission.START_VIEW_PERMISSION_USAGE", Theme = "@style/Maui.SplashTheme")]
[IntentFilter(["android.intent.action.VIEW_PERMISSION_USAGE"], Categories = ["android.intent.category.HEALTH_PERMISSIONS"])]
public sealed class HealthPermissionsUsageActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var main = new Intent(this, typeof(MainActivity))
            .SetAction(MainActivity.ActionShowHealthRationale)!
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        this.StartActivity(main);
        this.Finish();
    }
}
