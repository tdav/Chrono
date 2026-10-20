using Android.Accounts;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common.Apis;
using Android.Gms.Extensions;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Вход в Google на Android через Google Identity Services (<c>AuthorizationClient</c>) без offline-доступа.
/// Клиент OAuth определяется по пакету и подписи приложения, client id в коде не нужен.
/// </summary>
public sealed class GoogleAuth : IGoogleAuth
{
    /// <summary>Не пересекается с <see cref="SmartWake.PermissionRequestCode"/> (4711).</summary>
    public const int AuthorizeRequestCode = 4712;

    private const string EmailKey = "google-account-email";

    // ponytail: AuthorizationResult не сообщает срок жизни токена (в нём нет expires_in); токены Google живут около
    // часа, а Play Services может отдать токен из своего кэша с уже израсходованной частью срока. 50 мин — консервативно;
    // если токен всё же умер раньше, GoogleCalendarClient получит 401, сбросит кэш и запросит токен заново.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);

    private static readonly string[] ScopeNames =
    [
        "https://www.googleapis.com/auth/calendar.app.created",
        "https://www.googleapis.com/auth/calendar.calendarlist.readonly",
        "openid",
        "email",
    ];

    private static TaskCompletionSource<Intent?>? pending;

    /// <summary>Вызывается из MainActivity.OnActivityResult. Завершает ожидание и при отмене пользователем.</summary>
    public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == AuthorizeRequestCode)
        {
            // Canceled (закрыл окно согласия) — тоже результат: иначе вход повис бы навсегда.
            pending?.TrySetResult(resultCode == Result.Ok ? data : null);
        }
    }

    public async Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct)
    {
        // Application.Context, а не активность: из фона (WorkManager) активности нет.
        var client = Identity.GetAuthorizationClient(global::Android.App.Application.Context);
        var request = AuthorizationRequest.InvokeBuilder()
            .SetRequestedScopes(ScopeNames.Select(s => new Scope(s)).ToList())
            .Build();

        var result = (AuthorizationResult)(await client.Authorize(request).AsAsync<AuthorizationResult>())!;
        if (result.HasResolution)
        {
            // Нужно окно согласия: из фона его не показать, и без активности тоже.
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (!interactive || activity is null)
            {
                return null;
            }

            var waiting = new TaskCompletionSource<Intent?>();
            pending?.TrySetResult(null);
            pending = waiting;
            using var registration = ct.Register(() => waiting.TrySetCanceled(ct));

            activity.StartIntentSenderForResult(result.PendingIntent!.IntentSender, AuthorizeRequestCode, null, 0, 0, 0);
            var data = await waiting.Task;
            if (data is null)
            {
                return null;
            }

            result = client.GetAuthorizationResultFromIntent(data);
        }

        if (result.AccessToken is not { Length: > 0 } token)
        {
            return null;
        }

        // Email нужен для RevokeAccess при выходе; хранится, потому что выход возможен после перезапуска приложения.
#pragma warning disable CS0618 // ToGoogleSignInAccount помечен deprecated, но другого способа узнать аккаунт у результата нет
        if (result.ToGoogleSignInAccount()?.Email is { Length: > 0 } email)
#pragma warning restore CS0618
        {
            Preferences.Default.Set(EmailKey, email);
        }

        return new GoogleToken(token, TokenLifetime);
    }

    public async Task SignOutAsync()
    {
        // Без отзыва доступа неинтерактивный Authorize продолжил бы молча выдавать токен.
        var email = Preferences.Default.Get<string?>(EmailKey, null);
        Preferences.Default.Remove(EmailKey);
        if (email is null)
        {
            return;
        }

        var client = Identity.GetAuthorizationClient(global::Android.App.Application.Context);
        var request = RevokeAccessRequest.InvokeBuilder()
            .SetAccount(new Account(email, "com.google"))
            .SetScopes(ScopeNames.Select(s => new Scope(s)).ToList())
            .Build();
        await client.RevokeAccess(request).AsAsync();
    }
}
