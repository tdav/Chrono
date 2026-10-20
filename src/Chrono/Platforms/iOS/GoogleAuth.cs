using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Вход в Google на iOS: OAuth 2.0 для установленных приложений через <see cref="WebAuthenticator"/>,
/// PKCE (S256), refresh token в Keychain (<see cref="SecureStorage"/>). Секрета у iOS-клиента нет.
/// </summary>
public sealed class GoogleAuth : IGoogleAuth
{
    // Идентификатор клиента — не секрет (iOS-клиент проекта Google Cloud).
    private const string ClientId = "928605107126-kc4pdk4iekr0hig3rikve46jf68ncjf5.apps.googleusercontent.com";

    // Схема — обратная запись client id; она же в CFBundleURLTypes в Info.plist. Без неё
    // приложение не получает перенаправление, и WebAuthenticator молча не возвращается.
    private const string RedirectUri = "com.googleusercontent.apps.928605107126-kc4pdk4iekr0hig3rikve46jf68ncjf5:/oauth2redirect";

    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RefreshTokenKey = "google-refresh-token";

    private const string Scopes =
        "https://www.googleapis.com/auth/calendar.app.created " +
        "https://www.googleapis.com/auth/calendar.calendarlist.readonly openid email";

    private readonly HttpClient http;

    public GoogleAuth(HttpClient http)
    {
        this.http = http;
    }

    public async Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct)
    {
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        var usedRefresh = !string.IsNullOrEmpty(refreshToken);
        Dictionary<string, string>? form = usedRefresh
            ? new()
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken!,
            }
            : null;

        if (form is null && !interactive)
        {
            return null;
        }

        while (true)
        {
            if (form is null)
            {
                // code_verifier: 32 случайных байта -> 43 символа base64url (допустимо 43..128).
                var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
                // code_challenge = BASE64URL(SHA256(ASCII(verifier))); Base64Url.EncodeToString паддинг «=» не добавляет.
                var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

                var query = await new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["redirect_uri"] = RedirectUri,
                    ["response_type"] = "code",
                    ["scope"] = Scopes,
                    ["code_challenge"] = challenge,
                    ["code_challenge_method"] = "S256",
                    ["state"] = state,
                    // Для установленных приложений refresh token выдаётся всегда; access_type=offline и
                    // prompt=consent страхуют повторный вход после выхода: без consent Google не повторяет refresh token.
                    ["access_type"] = "offline",
                    ["prompt"] = "consent",
                }).ReadAsStringAsync(ct);

                WebAuthenticatorResult auth;
                try
                {
                    auth = await MainThread.InvokeOnMainThreadAsync(() => WebAuthenticator.Default.AuthenticateAsync(
                        new WebAuthenticatorOptions
                        {
                            Url = new Uri($"{AuthEndpoint}?{query}"),
                            CallbackUrl = new Uri(RedirectUri),
                        }));
                }
                catch (TaskCanceledException)
                {
                    // Пользователь закрыл окно входа.
                    return null;
                }

                // Отказ в доступе приходит параметром error без code.
                if (auth.Properties.ContainsKey("error"))
                {
                    return null;
                }

                // Защита от подмены ответа: state должен совпасть с отправленным.
                if (!auth.Properties.TryGetValue("state", out var returnedState)
                    || !string.Equals(returnedState, state, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Ответ Google с чужим state.");
                }

                if (!auth.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
                {
                    throw new InvalidOperationException("Ответ Google без code.");
                }

                form = new()
                {
                    ["client_id"] = ClientId,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["code_verifier"] = verifier,
                    ["redirect_uri"] = RedirectUri,
                };
            }

            using var response = await this.http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                if (error == "invalid_grant" && usedRefresh)
                {
                    // Refresh token отозван или просрочен: забываем его и просим войти заново.
                    SecureStorage.Default.Remove(RefreshTokenKey);
                    if (!interactive)
                    {
                        return null;
                    }

                    usedRefresh = false;
                    form = null;
                    continue;
                }

                throw new HttpRequestException($"Google token: {(int)response.StatusCode} {error}");
            }

            // refresh_token приходит не в каждом ответе (при обмене refresh token его нет) — старый не затираем.
            if (root.TryGetProperty("refresh_token", out var rt) && !string.IsNullOrEmpty(rt.GetString()))
            {
                await SecureStorage.Default.SetAsync(RefreshTokenKey, rt.GetString()!);
            }

            // expires_in — секунды.
            return new GoogleToken(
                root.GetProperty("access_token").GetString()!,
                TimeSpan.FromSeconds(root.GetProperty("expires_in").GetInt32()));
        }
    }

    public Task SignOutAsync()
    {
        // ponytail: токен в Google не отзываем (oauth2.googleapis.com/revoke), только забываем локально.
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }
}
