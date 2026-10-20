using System.Net;
using System.Text;
using Chrono.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Tests;

public sealed class GoogleCalendarClientTests : IDisposable
{
    private readonly FakeHandler handler = new();
    private readonly FakeAuth auth = new();
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly HttpClient http;
    private readonly GoogleCalendarClient client;

    public GoogleCalendarClientTests()
    {
        this.http = new HttpClient(this.handler);
        this.client = new GoogleCalendarClient(this.http, this.cache, this.auth);
    }

    public void Dispose()
    {
        this.http.Dispose();
        this.cache.Dispose();
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    [Fact]
    public async Task Token_CachedUntilExpiry()
    {
        this.handler.Respond = _ => Json("{\"email\":\"a@b.c\"}");

        await this.client.GetEmailAsync(CancellationToken.None);
        await this.client.GetEmailAsync(CancellationToken.None);

        Assert.Equal(1, this.auth.Calls);
        Assert.Equal(2, this.handler.Requests.Count);
        Assert.True(this.cache.TryGetValue("google-access-token", out string? cached));
        Assert.Equal("token-1", cached);
        Assert.All(this.handler.Requests, r => Assert.Equal("Bearer token-1", r.Authorization));
    }

    [Fact]
    public async Task Token_ExpiresInUnderMargin_NotCached()
    {
        this.auth.ExpiresIn = TimeSpan.FromMinutes(4);
        this.handler.Respond = _ => Json("{\"email\":\"a@b.c\"}");

        await this.client.GetEmailAsync(CancellationToken.None);
        await this.client.GetEmailAsync(CancellationToken.None);

        Assert.Equal(2, this.auth.Calls);
    }

    [Fact]
    public async Task Unauthorized_RemovesTokenAndRetriesOnce()
    {
        this.handler.Respond = r => r.Authorization == "Bearer token-1"
            ? Status(HttpStatusCode.Unauthorized)
            : Json("{\"email\":\"a@b.c\"}");

        var email = await this.client.GetEmailAsync(CancellationToken.None);

        Assert.Equal("a@b.c", email);
        Assert.Equal(2, this.auth.Calls);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], this.handler.Requests.Select(r => r.Authorization));
        Assert.True(this.cache.TryGetValue("google-access-token", out string? cached));
        Assert.Equal("token-2", cached);
    }

    [Fact]
    public async Task Unauthorized_Twice_ThrowsAfterSingleRetry()
    {
        this.handler.Respond = _ => Status(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => this.client.GetEmailAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(2, this.handler.Requests.Count);
        Assert.False(this.cache.TryGetValue("google-access-token", out _));
    }

    // Логика токена и повтора записана в теле каждого метода, поэтому 401 проверяется для всех.
    [Theory]
    [InlineData("FindOrCreate")]
    [InlineData("ListChanges")]
    [InlineData("Insert")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("NextInstance")]
    [InlineData("GetEmail")]
    public async Task Unauthorized_EveryMethod_RetriesWithFreshToken(string method)
    {
        this.handler.Respond = r => r.Authorization == "Bearer token-1"
            ? Status(HttpStatusCode.Unauthorized)
            : Json("{\"id\":\"x\",\"summary\":\"Chrono\",\"items\":[{\"id\":\"x\",\"summary\":\"Chrono\"}],\"nextSyncToken\":\"n\",\"email\":\"e\"}");
        var ct = CancellationToken.None;

        await (method switch
        {
            "FindOrCreate" => this.client.FindOrCreateCalendarAsync(ct),
            "ListChanges" => this.client.ListChangesAsync("c", null, ct),
            "Insert" => this.client.InsertAsync("c", new GoogleEvent(), ct),
            "Update" => this.client.UpdateAsync("c", new GoogleEvent { Id = "e" }, ct),
            "Delete" => this.client.DeleteAsync("c", "e", ct),
            "NextInstance" => this.client.NextInstanceAsync("c", "e", DateTimeOffset.Now, ct),
            _ => this.client.GetEmailAsync(ct),
        });

        Assert.Equal(2, this.auth.Calls);
        Assert.Equal("Bearer token-2", this.handler.Requests[^1].Authorization);
    }

    [Fact]
    public async Task NoToken_SignInRequired()
    {
        this.auth.NoToken = true;

        await Assert.ThrowsAsync<SignInRequiredException>(() => this.client.GetEmailAsync(CancellationToken.None));

        Assert.False(this.auth.LastInteractive);
        Assert.Empty(this.handler.Requests);
    }

    [Fact]
    public async Task ListChanges_FollowsPagesAndReturnsSyncToken()
    {
        this.handler.Respond = r => r.Uri.Query.Contains("pageToken=p2")
            ? Json("{\"items\":[{\"id\":\"e3\"}],\"nextSyncToken\":\"sync-final\"}")
            : Json("{\"items\":[{\"id\":\"e1\"},{\"id\":\"e2\",\"status\":\"cancelled\"}],\"nextPageToken\":\"p2\"}");

        var (events, next) = await this.client.ListChangesAsync("cal@group.calendar.google.com", null, CancellationToken.None);

        Assert.Equal(["e1", "e2", "e3"], events.Select(e => e.Id));
        Assert.Equal("cancelled", events[1].Status);
        Assert.Equal("sync-final", next);
        Assert.Equal(2, this.handler.Requests.Count);
        Assert.All(this.handler.Requests, r => Assert.Contains("showDeleted=true", r.Uri.Query));
        Assert.All(this.handler.Requests, r => Assert.DoesNotContain("syncToken", r.Uri.Query));
        Assert.All(this.handler.Requests, r => Assert.DoesNotContain("timeMin", r.Uri.Query));
    }

    [Fact]
    public async Task ListChanges_EncodesCalendarIdAndSyncToken()
    {
        this.handler.Respond = _ => Json("{\"items\":[],\"nextSyncToken\":\"n\"}");

        await this.client.ListChangesAsync("cal@group.calendar.google.com", "a+b/c=d&e", CancellationToken.None);

        var uri = this.handler.Requests[0].Uri;
        Assert.StartsWith("https://www.googleapis.com/calendar/v3/calendars/cal%40group.calendar.google.com/events?", uri.AbsoluteUri);
        Assert.Contains("syncToken=a%2Bb%2Fc%3Dd%26e", uri.Query);
        Assert.Contains("showDeleted=true", uri.Query);
    }

    [Fact]
    public async Task ListChanges_410_SyncTokenExpired()
    {
        this.handler.Respond = _ => Json("{\"error\":{\"code\":410}}", HttpStatusCode.Gone);

        await Assert.ThrowsAsync<SyncTokenExpiredException>(
            () => this.client.ListChangesAsync("cal", "old-token", CancellationToken.None));
    }

    [Fact]
    public async Task ListChanges_LastPageWithoutSyncToken_Throws()
    {
        this.handler.Respond = _ => Json("{\"items\":[]}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.client.ListChangesAsync("cal", null, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_404_IsNotError()
    {
        this.handler.Respond = _ => Status(HttpStatusCode.NotFound);
        await this.client.DeleteAsync("cal", "ev1", CancellationToken.None);

        this.handler.Respond = _ => Status(HttpStatusCode.Gone);
        await this.client.DeleteAsync("cal", "ev1", CancellationToken.None);

        this.handler.Respond = _ => Status(HttpStatusCode.NoContent);
        await this.client.DeleteAsync("cal", "ev1", CancellationToken.None);

        this.handler.Respond = _ => Status(HttpStatusCode.Forbidden);
        await Assert.ThrowsAsync<HttpRequestException>(() => this.client.DeleteAsync("cal", "ev1", CancellationToken.None));

        Assert.All(this.handler.Requests, r => Assert.Equal(HttpMethod.Delete, r.Method));
    }

    [Fact]
    public async Task FindOrCreate_FindsExistingByName_ElseCreates()
    {
        this.handler.Respond = r => r.Uri.Query.Contains("pageToken=p2")
            ? Json("{\"items\":[{\"id\":\"chrono-id\",\"summary\":\"Chrono\",\"accessRole\":\"owner\"}]}")
            : Json("{\"items\":[{\"id\":\"other\",\"summary\":\"Work\"}],\"nextPageToken\":\"p2\"}");

        Assert.Equal("chrono-id", await this.client.FindOrCreateCalendarAsync(CancellationToken.None));
        Assert.All(this.handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));

        this.handler.Requests.Clear();
        this.handler.Respond = r => r.Method == HttpMethod.Get
            ? Json("{\"items\":[{\"id\":\"other\",\"summary\":\"Work\"}]}")
            : Json("{\"id\":\"new-id\",\"summary\":\"Chrono\"}");

        Assert.Equal("new-id", await this.client.FindOrCreateCalendarAsync(CancellationToken.None));

        var create = this.handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.EndsWith("/calendars", create.Uri.AbsolutePath);
        Assert.Contains("\"summary\":\"Chrono\"", create.Body);
        Assert.Contains("\"timeZone\":", create.Body);
    }

    [Fact]
    public async Task FindOrCreate_IgnoresSubscribedCalendarWithSameName()
    {
        // Чужой календарь «Chrono», на который пользователь подписан (не owner), брать нельзя.
        this.handler.Respond = r => r.Method == HttpMethod.Get
            ? Json("{\"items\":[{\"id\":\"foreign\",\"summary\":\"Chrono\",\"accessRole\":\"reader\"},{\"id\":\"mine\",\"summary\":\"Chrono\",\"accessRole\":\"owner\"}]}")
            : Json("{\"id\":\"new-id\",\"summary\":\"Chrono\"}");
        Assert.Equal("mine", await this.client.FindOrCreateCalendarAsync(CancellationToken.None));

        this.handler.Respond = r => r.Method == HttpMethod.Get
            ? Json("{\"items\":[{\"id\":\"foreign\",\"summary\":\"Chrono\",\"accessRole\":\"writer\"}]}")
            : Json("{\"id\":\"new-id\",\"summary\":\"Chrono\"}");
        Assert.Equal("new-id", await this.client.FindOrCreateCalendarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FindOrCreate_Forbidden_ThrowsWithResponseText()
    {
        this.handler.Respond = _ => Json("{\"error\":{\"message\":\"Insufficient Permission\"}}", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => this.client.FindOrCreateCalendarAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains("Insufficient Permission", ex.Message);
        Assert.DoesNotContain(this.handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task InsertAndUpdate_UseExpectedVerbsAndPaths()
    {
        this.handler.Respond = r => Json("{\"id\":\"srv-id\",\"status\":\"confirmed\"}");

        var inserted = await this.client.InsertAsync("cal@x", new GoogleEvent { Status = "confirmed" }, CancellationToken.None);
        var updated = await this.client.UpdateAsync("cal@x", new GoogleEvent { Id = "srv-id", Status = "confirmed" }, CancellationToken.None);

        Assert.Equal("srv-id", inserted.Id);
        Assert.Equal("srv-id", updated.Id);
        Assert.Equal(HttpMethod.Post, this.handler.Requests[0].Method);
        Assert.EndsWith("/events", this.handler.Requests[0].Uri.AbsolutePath);
        Assert.DoesNotContain("\"id\"", this.handler.Requests[0].Body);
        Assert.Equal(HttpMethod.Put, this.handler.Requests[1].Method);
        Assert.EndsWith("/events/srv-id", this.handler.Requests[1].Uri.AbsolutePath);
        Assert.Contains("\"id\":\"srv-id\"", this.handler.Requests[1].Body);
    }

    [Fact]
    public async Task NextInstance_SkipsInstanceThatAlreadyStarted()
    {
        // timeMin отбирает по концу события: первым приходит идущий сейчас экземпляр, нужен следующий.
        var after = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        this.handler.Respond = _ => Json("""
            {"items":[
              {"id":"running","start":{"dateTime":"2026-10-20T11:50:00+00:00"},"end":{"dateTime":"2026-10-20T12:20:00+00:00"}},
              {"id":"next","start":{"dateTime":"2026-10-21T11:50:00+00:00"},"end":{"dateTime":"2026-10-21T12:20:00+00:00"}}]}
            """);

        var instance = await this.client.NextInstanceAsync("cal", "ev", after, CancellationToken.None);

        Assert.Equal("next", instance?.Id);

        this.handler.Respond = _ => Json("""
            {"items":[{"id":"running","start":{"dateTime":"2026-10-20T11:50:00+00:00"},"end":{"dateTime":"2026-10-20T12:20:00+00:00"}}]}
            """);
        Assert.Null(await this.client.NextInstanceAsync("cal", "ev", after, CancellationToken.None));
    }

    [Fact]
    public async Task NextInstance_AsksSingleInstanceAfterTime_NullWhenNone()
    {
        this.handler.Respond = _ => Json("{\"items\":[{\"id\":\"ev_20261021\",\"start\":{\"dateTime\":\"2026-10-21T10:00:00+05:00\"}}]}");
        var after = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(5));

        var instance = await this.client.NextInstanceAsync("cal", "ev", after, CancellationToken.None);

        Assert.Equal("ev_20261021", instance?.Id);
        var uri = this.handler.Requests[0].Uri;
        Assert.EndsWith("/events/ev/instances", uri.AbsolutePath);
        Assert.Contains("maxResults=2", uri.Query);
        Assert.Contains("timeMin=2026-10-20T12%3A00%3A00%2B05%3A00", uri.Query);

        this.handler.Respond = _ => Json("{\"items\":[]}");
        Assert.Null(await this.client.NextInstanceAsync("cal", "ev", after, CancellationToken.None));
    }

    [Fact]
    public async Task GetEmail_UsesUserinfoEndpoint()
    {
        this.handler.Respond = _ => Json("{\"email\":\"me@gmail.com\",\"sub\":\"1\"}");

        Assert.Equal("me@gmail.com", await this.client.GetEmailAsync(CancellationToken.None));

        Assert.Equal("https://openidconnect.googleapis.com/v1/userinfo", this.handler.Requests[0].Uri.ToString());
    }

    private sealed record Seen(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];

        public Func<Seen, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var seen = new Seen(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body);
            this.Requests.Add(seen);
            return this.Respond(seen);
        }
    }

    private sealed class FakeAuth : IGoogleAuth
    {
        public int Calls { get; private set; }

        public bool NoToken { get; set; }

        public bool LastInteractive { get; private set; } = true;

        public TimeSpan ExpiresIn { get; set; } = TimeSpan.FromHours(1);

        public Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct)
        {
            this.LastInteractive = interactive;
            if (this.NoToken)
            {
                return Task.FromResult<GoogleToken?>(null);
            }

            this.Calls++;
            return Task.FromResult<GoogleToken?>(new GoogleToken($"token-{this.Calls}", this.ExpiresIn));
        }

        public Task SignOutAsync() => Task.CompletedTask;
    }
}
