using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Services;

/// <summary>
/// HTTP-клиент Google Calendar API v3 (без MAUI). Токен берётся из IMemoryCache; на 401 токен удаляется
/// и запрос повторяется один раз. Любой другой неуспех — HttpRequestException с кодом и текстом ответа
/// (в частности 403 на calendarList.list для области calendar.app.created).
/// </summary>
public sealed class GoogleCalendarClient
{
    private const string TokenKey = "google-access-token";
    private const string UserInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string CalendarName = "Chrono";

    private static readonly TimeSpan TokenMargin = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http;
    private readonly IMemoryCache cache;
    private readonly IGoogleAuth auth;

    public GoogleCalendarClient(HttpClient http, IMemoryCache cache, IGoogleAuth auth)
    {
        this.http = http;
        this.cache = cache;
        this.auth = auth;
        this.http.BaseAddress = new Uri("https://www.googleapis.com/calendar/v3/");
        this.http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>calendarList.list с отбором summary == "Chrono" и accessRole == "owner" (чужой подписанный календарь не берём), иначе calendars.insert.</summary>
    public async Task<string> FindOrCreateCalendarAsync(CancellationToken ct)
    {
        string? pageToken = null;
        do
        {
            var url = "users/me/calendarList" + (pageToken is null ? "" : "?pageToken=" + Uri.EscapeDataString(pageToken));
            CalendarListPage? page = null;
            for (var attempt = 0; page is null; attempt++)
            {
                if (!this.cache.TryGetValue(TokenKey, out string? token))
                {
                    var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                    token = fresh.AccessToken;
                    if (fresh.ExpiresIn > TokenMargin)
                    {
                        this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                    }
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await this.http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    this.cache.Remove(TokenKey);
                    if (attempt == 0)
                    {
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Google API {(int)response.StatusCode} GET {url}: {await response.Content.ReadAsStringAsync(ct)}",
                        null,
                        response.StatusCode);
                }

                page = await response.Content.ReadFromJsonAsync<CalendarListPage>(Json, ct) ?? new CalendarListPage();
            }

            var found = page.Items?.FirstOrDefault(c => c.Summary == CalendarName && c.AccessRole == "owner" && c.Id is not null);
            if (found is not null)
            {
                return found.Id!;
            }

            pageToken = page.NextPageToken;
        }
        while (pageToken is not null);

        for (var attempt = 0; ; attempt++)
        {
            if (!this.cache.TryGetValue(TokenKey, out string? token))
            {
                var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                token = fresh.AccessToken;
                if (fresh.ExpiresIn > TokenMargin)
                {
                    this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "calendars")
            {
                Content = JsonContent.Create(new NewCalendar(CalendarName, TimeZoneInfo.Local.Id), options: Json),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await this.http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.cache.Remove(TokenKey);
                if (attempt == 0)
                {
                    continue;
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google API {(int)response.StatusCode} POST calendars: {await response.Content.ReadAsStringAsync(ct)}",
                    null,
                    response.StatusCode);
            }

            var created = await response.Content.ReadFromJsonAsync<CalendarListEntry>(Json, ct);
            return created?.Id ?? throw new InvalidOperationException("calendars.insert не вернул id календаря.");
        }
    }

    /// <summary>
    /// events.list с showDeleted=true. syncToken == null — полная загрузка (без timeMin: прошлое отбрасывает CalendarMapping).
    /// nextSyncToken приходит только на последней странице. 410 — SyncTokenExpiredException.
    /// </summary>
    public async Task<(IReadOnlyList<GoogleEvent> Events, string NextSyncToken)> ListChangesAsync(
        string calendarId, string? syncToken, CancellationToken ct)
    {
        var events = new List<GoogleEvent>();
        string? pageToken = null;
        while (true)
        {
            var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events?showDeleted=true"
                + (syncToken is null ? "" : "&syncToken=" + Uri.EscapeDataString(syncToken))
                + (pageToken is null ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken));
            EventsPage? page = null;
            for (var attempt = 0; page is null; attempt++)
            {
                if (!this.cache.TryGetValue(TokenKey, out string? token))
                {
                    var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                    token = fresh.AccessToken;
                    if (fresh.ExpiresIn > TokenMargin)
                    {
                        this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                    }
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await this.http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Gone)
                {
                    throw new SyncTokenExpiredException();
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    this.cache.Remove(TokenKey);
                    if (attempt == 0)
                    {
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Google API {(int)response.StatusCode} GET {url}: {await response.Content.ReadAsStringAsync(ct)}",
                        null,
                        response.StatusCode);
                }

                page = await response.Content.ReadFromJsonAsync<EventsPage>(Json, ct) ?? new EventsPage();
            }

            events.AddRange(page.Items ?? []);
            if (page.NextPageToken is not null)
            {
                pageToken = page.NextPageToken;
                continue;
            }

            return (events, page.NextSyncToken
                ?? throw new InvalidOperationException("Последняя страница events.list без nextSyncToken."));
        }
    }

    public async Task<GoogleEvent> InsertAsync(string calendarId, GoogleEvent ev, CancellationToken ct)
    {
        var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events";
        for (var attempt = 0; ; attempt++)
        {
            if (!this.cache.TryGetValue(TokenKey, out string? token))
            {
                var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                token = fresh.AccessToken;
                if (fresh.ExpiresIn > TokenMargin)
                {
                    this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(ev, options: Json) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await this.http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.cache.Remove(TokenKey);
                if (attempt == 0)
                {
                    continue;
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google API {(int)response.StatusCode} POST {url}: {await response.Content.ReadAsStringAsync(ct)}",
                    null,
                    response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<GoogleEvent>(Json, ct)
                ?? throw new InvalidOperationException("events.insert вернул пустой ответ.");
        }
    }

    /// <summary>events.update — PUT events/{id}; ev.Id обязателен.</summary>
    public async Task<GoogleEvent> UpdateAsync(string calendarId, GoogleEvent ev, CancellationToken ct)
    {
        var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(ev.Id ?? throw new ArgumentException("У события нет Id.", nameof(ev)))}";
        for (var attempt = 0; ; attempt++)
        {
            if (!this.cache.TryGetValue(TokenKey, out string? token))
            {
                var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                token = fresh.AccessToken;
                if (fresh.ExpiresIn > TokenMargin)
                {
                    this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(ev, options: Json) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await this.http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.cache.Remove(TokenKey);
                if (attempt == 0)
                {
                    continue;
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google API {(int)response.StatusCode} PUT {url}: {await response.Content.ReadAsStringAsync(ct)}",
                    null,
                    response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<GoogleEvent>(Json, ct)
                ?? throw new InvalidOperationException("events.update вернул пустой ответ.");
        }
    }

    /// <summary>events.delete; 404 и 410 (события уже нет) — не ошибка.</summary>
    public async Task DeleteAsync(string calendarId, string eventId, CancellationToken ct)
    {
        var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
        for (var attempt = 0; ; attempt++)
        {
            if (!this.cache.TryGetValue(TokenKey, out string? token))
            {
                var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                token = fresh.AccessToken;
                if (fresh.ExpiresIn > TokenMargin)
                {
                    this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await this.http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.cache.Remove(TokenKey);
                if (attempt == 0)
                {
                    continue;
                }
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google API {(int)response.StatusCode} DELETE {url}: {await response.Content.ReadAsStringAsync(ct)}",
                    null,
                    response.StatusCode);
            }

            return;
        }
    }

    /// <summary>
    /// events.instances: ближайшее повторение серии, начало которого позже <paramref name="after"/>, null — если нет.
    /// timeMin отбирает по концу события, поэтому первым может прийти идущий сейчас экземпляр; берём два и пропускаем начатые.
    /// Если начались оба (длительность больше периода), запрос повторяется от конца последнего полученного (не более 32 раз).
    /// </summary>
    public async Task<GoogleEvent?> NextInstanceAsync(string calendarId, string eventId, DateTimeOffset after, CancellationToken ct)
    {
        // Событие на весь день — полночь по смещению after (точность до часов достаточна: сравнение только с after).
        DateTimeOffset? Parse(GoogleEventTime? t) =>
            t?.DateTime is { } dateTime && DateTimeOffset.TryParse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateOnly.TryParseExact(t?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), after.Offset)
                    : null;

        var from = after;
        for (var step = 0; step < 32; step++)
        {
            var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}/instances"
                + "?maxResults=2&timeMin="
                + Uri.EscapeDataString(from.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
            EventsPage? page = null;
            for (var attempt = 0; page is null; attempt++)
            {
                if (!this.cache.TryGetValue(TokenKey, out string? token))
                {
                    var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                    token = fresh.AccessToken;
                    if (fresh.ExpiresIn > TokenMargin)
                    {
                        this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                    }
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await this.http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    this.cache.Remove(TokenKey);
                    if (attempt == 0)
                    {
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Google API {(int)response.StatusCode} GET {url}: {await response.Content.ReadAsStringAsync(ct)}",
                        null,
                        response.StatusCode);
                }

                page = await response.Content.ReadFromJsonAsync<EventsPage>(Json, ct) ?? new EventsPage();
            }

            var items = page.Items ?? [];
            if (items.FirstOrDefault(e => Parse(e.Start) > after) is { } next)
            {
                return next;
            }

            // Меньше двух — экземпляров больше нет; иначе продолжаем с конца последнего (он строго дальше from, иначе выходим).
            if (items.Count < 2 || Parse(items[^1].End) is not { } end || end <= from)
            {
                return null;
            }

            from = end;
        }

        return null;
    }

    /// <summary>Адрес почты аккаунта из userinfo (области openid и email).</summary>
    public async Task<string?> GetEmailAsync(CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!this.cache.TryGetValue(TokenKey, out string? token))
            {
                var fresh = await this.auth.GetTokenAsync(false, ct) ?? throw new SignInRequiredException();
                token = fresh.AccessToken;
                if (fresh.ExpiresIn > TokenMargin)
                {
                    this.cache.Set(TokenKey, token, fresh.ExpiresIn - TokenMargin);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await this.http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.cache.Remove(TokenKey);
                if (attempt == 0)
                {
                    continue;
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google API {(int)response.StatusCode} GET userinfo: {await response.Content.ReadAsStringAsync(ct)}",
                    null,
                    response.StatusCode);
            }

            var info = await response.Content.ReadFromJsonAsync<UserInfo>(Json, ct);
            return info?.Email;
        }
    }

    private sealed record NewCalendar(
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("timeZone")] string TimeZone);

    private sealed record CalendarListEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("accessRole")] string? AccessRole = null);

    private sealed record CalendarListPage
    {
        [JsonPropertyName("items")]
        public List<CalendarListEntry>? Items { get; init; }

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }
    }

    private sealed record EventsPage
    {
        [JsonPropertyName("items")]
        public List<GoogleEvent>? Items { get; init; }

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("nextSyncToken")]
        public string? NextSyncToken { get; init; }
    }

    private sealed record UserInfo([property: JsonPropertyName("email")] string? Email);
}
