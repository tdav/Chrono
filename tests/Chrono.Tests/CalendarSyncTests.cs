using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Chrono.Models;
using Chrono.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.Tests;

/// <summary>
/// Настоящие AlarmService/AlarmStore/SyncStore во временной папке + GoogleCalendarClient с фейковым календарём в памяти.
/// В каждом тесте проверяется итоговое число записей в alarms.json и событий в календаре: дубли — главная анти-цель.
/// </summary>
public sealed class CalendarSyncTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-sync-");
    private readonly FakeScheduler scheduler = new();
    private readonly FixedTime time = new(Now);
    private readonly FakeCalendar server;
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly HttpClient http;
    private readonly AlarmStore store;
    private readonly SyncStore sync;
    private readonly AlarmService service;
    private readonly AlarmService plain;
    private readonly CalendarSync calendarSync;

    public CalendarSyncTests()
    {
        this.server = new FakeCalendar(Now.AddHours(-1));
        this.http = new HttpClient(this.server);
        this.store = new AlarmStore(this.directory.FullName);
        this.sync = new SyncStore(this.directory.FullName);
        this.service = new AlarmService(this.store, this.scheduler, this.time, this.sync);
        this.plain = new AlarmService(this.store, this.scheduler, this.time);
        this.calendarSync = new CalendarSync(new GoogleCalendarClient(this.http, this.cache, new FakeAuth()), this.service, this.sync, this.time);
    }

    public void Dispose()
    {
        this.http.Dispose();
        this.cache.Dispose();
        this.directory.Delete(recursive: true);
    }

    private List<Alarm> Alarms => this.store.Load().Alarms;

    private List<PendingChange> Pending => this.sync.Load().Pending;

    private Task<SyncResult> Sync() => this.calendarSync.SyncAsync(CancellationToken.None);

    private static Alarm NewAlarm(string label, int hours = 1) => new() { At = Now.AddHours(hours), Label = label };

    /// <summary>Событие Google, как его создал бы сам Google (без свойств Chrono), если chronoId не задан.</summary>
    private static GoogleEvent Ev(string id, DateTime start, string label = "Встреча", Guid? chronoId = null, string[]? recurrence = null) => new()
    {
        Id = id,
        Status = "confirmed",
        Summary = label,
        Start = new GoogleEventTime { DateTime = $"{start:yyyy-MM-dd'T'HH:mm:ss}+00:00", TimeZone = "UTC" },
        End = new GoogleEventTime { DateTime = $"{start.AddMinutes(30):yyyy-MM-dd'T'HH:mm:ss}+00:00", TimeZone = "UTC" },
        Recurrence = recurrence,
        ExtendedProperties = chronoId is null ? null : new GoogleExtendedProperties { Private = new() { [CalendarMapping.ChronoIdKey] = chronoId.Value.ToString() } },
    };

    [Fact]
    public async Task Sync_FirstLogin_PushesLocalAndCreatesCalendar()
    {
        this.plain.Save(NewAlarm("A", 1)); // не в очереди: уйдёт как «запись без GoogleEventId»
        this.plain.Save(NewAlarm("B", 2));
        this.service.Save(NewAlarm("C", 3)); // в очереди

        var result = await this.Sync();

        Assert.Equal(3, result.Pushed);
        Assert.Equal(1, this.server.CalendarsCreated);
        Assert.Equal(3, this.Alarms.Count);
        Assert.Equal(3, this.server.Live);
        Assert.All(this.Alarms, a => Assert.NotNull(a.GoogleEventId));
        Assert.Equal(this.Alarms.Select(a => a.GoogleEventId).Order(), this.server.All.Select(e => e.Id).Order());
        Assert.Equal(this.Alarms.Select(a => a.Id.ToString()).Order(), this.server.All.Select(e => e.ExtendedProperties!.Private![CalendarMapping.ChronoIdKey]).Order());
        Assert.Empty(this.Pending);
        Assert.Equal("cal-1", this.sync.Load().CalendarId);
        Assert.NotNull(this.sync.Load().LastSyncUtc);

        // Повторный проход ничего не создаёт: ни эхо собственной отправки, ни записи без очереди.
        var again = await this.Sync();
        Assert.Equal(0, again.Pushed);
        Assert.Equal(0, again.Pulled);
        Assert.Equal(3, this.Alarms.Count);
        Assert.Equal(3, this.server.Live);
        Assert.Equal(3, this.server.Count("POST calendars/cal-1/events"));
        Assert.Equal(1, this.server.CalendarsCreated);
    }

    [Fact]
    public async Task Sync_SavedCalendarId_SkipsCalendarLookup()
    {
        this.sync.Save(new SyncState { CalendarId = "cal-1" });
        this.server.Exists = true;

        await this.Sync();

        Assert.Equal(0, this.server.Count("GET users/me/calendarList"));
        Assert.Equal(0, this.server.CalendarsCreated);
    }

    [Fact]
    public async Task Sync_PullsNewGoogleEventAsRingRecord()
    {
        this.server.Add(Ev("g1", Now.AddHours(2), "Врач"));

        var result = await this.Sync();

        Assert.Equal(1, result.Pulled);
        var alarm = Assert.Single(this.Alarms);
        Assert.Equal("Врач", alarm.Label);
        Assert.Equal(AlertKind.Ring, alarm.Alert);
        Assert.Equal("g1", alarm.GoogleEventId);
        Assert.Equal(Now.AddHours(2), alarm.At);
        Assert.Equal(1, this.server.Live);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
        Assert.Equal(0, this.server.Count("PUT"));
        Assert.Empty(this.Pending);
        Assert.Contains(this.scheduler.Synced.Last(), a => a.Id == alarm.Id);
    }

    [Fact]
    public async Task Sync_RemoteCancelled_DeletesLocal()
    {
        this.server.Add(Ev("g1", Now.AddHours(2)));
        await this.Sync();
        var id = Assert.Single(this.Alarms).Id;

        this.server.Clock = Now;
        this.server.Cancel("g1");
        var result = await this.Sync();

        Assert.Equal(1, result.Deleted);
        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Live);
        Assert.Single(this.server.All);
        Assert.Empty(this.Pending);
        Assert.Contains(id, this.scheduler.Cancelled);
        Assert.Equal(0, this.server.Count("DELETE"));
    }

    [Fact]
    public async Task Sync_LocalEditNewer_Wins()
    {
        this.service.Save(NewAlarm("Исходная"));
        await this.Sync();
        var alarm = Assert.Single(this.Alarms);

        this.server.Clock = Now.AddMinutes(-10);
        this.server.Edit(alarm.GoogleEventId!, e => e with { Summary = "Google" });
        this.service.Save(alarm with { Label = "Телефон" }); // ChangedAtUtc = Now, позже правки в Google

        var result = await this.Sync();

        Assert.Equal(1, result.Pushed);
        Assert.Equal(0, result.Pulled);
        Assert.Equal("Телефон", Assert.Single(this.Alarms).Label);
        Assert.Equal("Телефон", Assert.Single(this.server.All).Summary);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_RemoteEditNewer_Wins()
    {
        this.service.Save(NewAlarm("Исходная"));
        await this.Sync();
        var alarm = Assert.Single(this.Alarms);

        this.service.Save(alarm with { Label = "Телефон" }); // ChangedAtUtc = Now
        this.server.Clock = Now.AddMinutes(10);
        this.server.Edit(alarm.GoogleEventId!, e => e with { Summary = "Google" });

        var result = await this.Sync();

        Assert.Equal(0, result.Pushed);
        Assert.Equal(1, result.Pulled);
        Assert.Equal("Google", Assert.Single(this.Alarms).Label);
        Assert.Equal("Google", Assert.Single(this.server.All).Summary);
        Assert.Equal(0, this.server.Count("PUT"));
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_SecondDevice_MatchesByChronoIdWithoutDuplicates()
    {
        // На этом телефоне записи есть, но не связаны с событиями (Id события не сохранился); в Google события с теми же chronoId.
        var plainAlarm = NewAlarm("Общая");
        this.plain.Save(plainAlarm); // не в очереди
        this.server.Add(Ev("g-plain", Now.AddHours(1), "Общая", plainAlarm.Id));

        // Запись в очереди и новее события: побеждает локальная, но второго события создавать нельзя.
        var queuedAlarm = NewAlarm("Телефон", 2);
        this.service.Save(queuedAlarm);
        this.server.Add(Ev("g-queued", Now.AddHours(2), "Google", queuedAlarm.Id));

        await this.Sync();

        Assert.Equal(2, this.Alarms.Count);
        Assert.Equal("g-plain", this.Alarms.Single(a => a.Id == plainAlarm.Id).GoogleEventId);
        var queued = this.Alarms.Single(a => a.Id == queuedAlarm.Id);
        Assert.Equal("g-queued", queued.GoogleEventId);
        Assert.Equal("Телефон", queued.Label);
        Assert.Equal(2, this.server.All.Count);
        Assert.Equal(2, this.server.Live);
        Assert.Equal("Телефон", this.server.All.Single(e => e.Id == "g-queued").Summary);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_InsertResponseLost_NoDuplicateOnRetry()
    {
        // events.insert дошёл до Google, а ответ потерян: GoogleEventId не сохранён, элемент остался в очереди.
        this.service.Save(NewAlarm("A"));
        this.server.DropResponse = key => key == "POST calendars/cal-1/events";
        await Assert.ThrowsAsync<HttpRequestException>(this.Sync);
        Assert.Single(this.server.All);
        Assert.Null(Assert.Single(this.Alarms).GoogleEventId);
        Assert.Single(this.Pending);

        this.server.DropResponse = null;
        await this.Sync();

        Assert.Single(this.Alarms);
        Assert.NotNull(this.Alarms[0].GoogleEventId);
        Assert.Single(this.server.All);
        Assert.Equal(1, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_CopyOfEventWithSameChronoId_DoesNotRebindOrDuplicate()
    {
        var alarm = NewAlarm("Оригинал");
        this.server.Add(Ev("g1", Now.AddHours(1), "Оригинал", alarm.Id));
        await this.Sync();

        this.server.Clock = Now;
        this.server.Add(Ev("g2", Now.AddHours(1), "Копия", alarm.Id)); // «Дублировать» в Google копирует extendedProperties
        await this.Sync();

        var local = Assert.Single(this.Alarms);
        Assert.Equal("g1", local.GoogleEventId);
        Assert.Equal("Оригинал", local.Label);
        Assert.Equal(2, this.server.Live);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
    }

    [Fact]
    public async Task Sync_SyncTokenExpired_FullReloadWithoutDuplicates()
    {
        this.service.Save(NewAlarm("A"));
        await this.Sync();
        this.server.Clock = Now;
        this.server.Add(Ev("g-b", Now.AddHours(2), "Б"));
        // Сработавшая «Один раз и удалить»: её событие в Google уже в прошлом, при полной загрузке она не должна вернуться.
        this.server.Add(Ev("g-p", Now.AddHours(-3), "Сработала") with
        {
            ExtendedProperties = new GoogleExtendedProperties { Private = new() { ["repeat"] = "OnceThenDelete" } },
        });
        this.server.ExpireToken = true;

        var result = await this.Sync();

        Assert.True(result.FullReload);
        Assert.Equal(1, result.Pulled);
        Assert.Equal(["A", "Б"], this.Alarms.Select(a => a.Label).Order(StringComparer.Ordinal));
        Assert.Equal(3, this.server.Live);
        Assert.Equal(1, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);

        // Полная загрузка привела к новому токену: следующий проход обычный.
        this.server.ExpireToken = false;
        var next = await this.Sync();
        Assert.False(next.FullReload);
        Assert.Equal(2, this.Alarms.Count);
    }

    [Fact]
    public async Task Sync_AutoDeletedOnceThenDelete_IsDeletedInGoogleAndNotReimported()
    {
        this.service.Save(new Alarm { At = Now.AddHours(1), Label = "Разово", Repeat = RepeatKind.OnceThenDelete });
        await this.Sync();
        Assert.Equal(1, this.server.Live);

        this.time.Now = Now.AddHours(3); // сработала и истекла: GetAll удалит её и поставит Delete в очередь
        this.server.Clock = this.time.Now;
        var result = await this.Sync();

        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Live);
        Assert.Equal(1, this.server.Count("DELETE"));
        Assert.Equal(0, result.Pulled);
        Assert.Empty(this.Pending);

        // Полная загрузка её не возвращает.
        this.server.ExpireToken = true;
        var reload = await this.Sync();
        Assert.True(reload.FullReload);
        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Live);
        Assert.Equal(1, this.server.Count("POST calendars/cal-1/events"));
    }

    [Fact]
    public async Task Sync_NetworkFailureMidPush_KeepsQueueAndAlarms()
    {
        var a = NewAlarm("A", 1);
        var b = NewAlarm("B", 2);
        var c = NewAlarm("C", 3);
        this.service.Save(a);
        this.service.Save(b);
        this.service.Save(c);
        var posts = 0;
        this.server.Fail = key => key == "POST calendars/cal-1/events" && ++posts == 2;

        await Assert.ThrowsAsync<HttpRequestException>(this.Sync);

        Assert.Equal(3, this.Alarms.Count);
        Assert.Equal(1, this.server.Live);
        Assert.Equal([b.Id, c.Id], this.Pending.Select(p => p.AlarmId));
        Assert.NotNull(this.Alarms.Single(x => x.Id == a.Id).GoogleEventId);
        Assert.Null(this.Alarms.Single(x => x.Id == b.Id).GoogleEventId);
        Assert.Contains(this.scheduler.Synced.Last(), x => x.Id == b.Id); // звонки стоят

        this.server.Fail = null;
        await this.Sync();

        Assert.Equal(3, this.Alarms.Count);
        Assert.Equal(3, this.server.Live);
        Assert.Equal(3, this.server.All.Count);
        Assert.Empty(this.Pending);
        Assert.All(this.Alarms, x => Assert.NotNull(x.GoogleEventId));
    }

    [Fact]
    public async Task Sync_SignInRequired_PropagatesAndKeepsQueue()
    {
        this.service.Save(NewAlarm("A"));
        var failing = new CalendarSync(
            new GoogleCalendarClient(this.http, new MemoryCache(new MemoryCacheOptions()), new FakeAuth { NoToken = true }),
            this.service, this.sync, this.time);

        await Assert.ThrowsAsync<SignInRequiredException>(() => failing.SyncAsync(CancellationToken.None));

        Assert.Single(this.Alarms);
        Assert.Single(this.Pending);
        Assert.Empty(this.server.All);
    }

    [Fact]
    public async Task Sync_ForeignSeries_NotPushed_DeleteIgnoredForever()
    {
        var instanceStart = Now.AddDays(5);
        this.server.Add(Ev("g-m", Now.AddDays(1), "Раз в месяц", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(new DateTimeOffset(instanceStart, TimeSpan.Zero), new DateTimeOffset(instanceStart.AddMinutes(30), TimeSpan.Zero))];

        await this.Sync();
        var alarm = Assert.Single(this.Alarms);
        Assert.True(alarm.ForeignSeries);
        Assert.Equal(instanceStart, alarm.At);

        // Локальная правка в Google не уходит, элемент очереди просто удаляется.
        this.service.Save(alarm with { Label = "Правка" });
        Assert.Single(this.Pending);
        await this.Sync();
        Assert.Empty(this.Pending);
        Assert.Equal(0, this.server.Count("PUT"));
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
        Assert.Equal("Раз в месяц", this.server.All.Single().Summary);

        // Ручное удаление: в Google не уходит и событие не возвращается ни после правки в Google, ни после полной загрузки.
        this.service.Delete(alarm.Id);
        Assert.Contains("g-m", this.sync.Load().IgnoredEventIds);
        Assert.Empty(this.Pending);
        await this.Sync();
        this.server.Clock = Now;
        this.server.Edit("g-m", e => e with { Summary = "Изменено в Google" });
        await this.Sync();
        this.server.ExpireToken = true;
        await this.Sync();

        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Count("DELETE"));
        Assert.Equal(1, this.server.Live);
    }

    [Fact]
    public async Task Sync_NextInstance_RunningInstanceSkippedToNextOne()
    {
        // timeMin фильтрует по концу события: первым приходит идущий сейчас экземпляр, его начало позади; клиент берёт два и пропускает его.
        var running = new DateTimeOffset(Now.AddMinutes(-10), TimeSpan.Zero);
        var next = new DateTimeOffset(Now.AddDays(1), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-30), "Ежемесячно", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(running, running.AddMinutes(30)), (next, next.AddMinutes(30))];

        await this.Sync();

        var alarm = Assert.Single(this.Alarms);
        Assert.Equal(next.DateTime, alarm.At);
        Assert.True(alarm.ForeignSeries);
        Assert.Equal(1, this.server.Count("GET calendars/cal-1/events/g-m/instances"));
        Assert.Equal(1, this.server.Live);
    }

    [Fact]
    public async Task Sync_NextInstance_NoFutureInstance_SkipsEvent()
    {
        this.server.Add(Ev("g-m", Now.AddDays(-30), "Закончилась", recurrence: ["RRULE:FREQ=MONTHLY"]));

        await this.Sync();

        Assert.Empty(this.Alarms);
        Assert.Equal(1, this.server.Live);
    }

    [Fact]
    public async Task Sync_EnqueueDuringNetworkCall_IsPreserved()
    {
        // Пользователь удаляет синхронизированную запись A в момент, когда уходит новая запись B.
        var a = NewAlarm("A", 1);
        this.service.Save(a);
        await this.Sync();
        var linked = Assert.Single(this.Alarms);
        var eventId = linked.GoogleEventId!;
        this.service.Save(NewAlarm("B", 2));
        var fired = false;
        this.server.Hook = key =>
        {
            if (!fired && key == "POST calendars/cal-1/events")
            {
                fired = true;
                this.service.Delete(linked.Id);
            }

            return Task.CompletedTask;
        };

        await this.Sync();

        Assert.True(fired);
        Assert.Equal(["B"], this.Alarms.Select(x => x.Label));
        Assert.Equal(1, this.server.Live);
        Assert.Equal("cancelled", this.server.All.Single(e => e.Id == eventId).Status);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_AlarmEditedDuringInsert_KeepsEditAndLinksEvent()
    {
        var alarm = NewAlarm("Исходная");
        this.service.Save(alarm);
        var fired = false;
        this.server.Hook = key =>
        {
            if (!fired && key == "POST calendars/cal-1/events")
            {
                fired = true;
                this.time.Now = Now.AddSeconds(1); // правка позже снимка очереди: иначе элементы совпали бы по ChangedAtUtc
                this.service.Save(alarm with { Label = "Правка во время отправки" });
            }

            return Task.CompletedTask;
        };

        await this.Sync();

        var local = Assert.Single(this.Alarms);
        Assert.Equal("Правка во время отправки", local.Label);
        Assert.NotNull(local.GoogleEventId);
        Assert.Single(this.server.All);
        Assert.Equal("Правка во время отправки", this.server.All[0].Summary);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_AlarmDeletedDuringInsert_RemovesCreatedEvent()
    {
        var alarm = NewAlarm("A");
        this.service.Save(alarm);
        var fired = false;
        this.server.Hook = key =>
        {
            if (!fired && key == "POST calendars/cal-1/events")
            {
                fired = true;
                this.service.Delete(alarm.Id);
            }

            return Task.CompletedTask;
        };

        await this.Sync();

        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Live);
        Assert.Empty(this.Pending);

        // И следующий проход не возвращает запись из Google.
        await this.Sync();
        Assert.Empty(this.Alarms);
    }

    [Fact]
    public async Task Sync_AlarmPassedDuringInsert_RejectedSaveLeavesNoGarbage()
    {
        // Пока шёл events.insert, время записи прошло: Save(связь) отклонён. Проход не падает, очередь чистая, дубля нет.
        this.service.Save(NewAlarm("A"));
        var fired = false;
        this.server.Hook = key =>
        {
            if (!fired && key == "POST calendars/cal-1/events")
            {
                fired = true;
                this.time.Now = Now.AddHours(5);
            }

            return Task.CompletedTask;
        };

        await this.Sync();

        Assert.Empty(this.Pending);
        Assert.Single(this.Alarms);
        Assert.Single(this.server.All);

        this.server.Clock = this.time.Now;
        await this.Sync();
        Assert.Single(this.Alarms);
        Assert.Single(this.server.All);
        Assert.Equal(1, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_LocalUpdateOfDeletedEvent_ReinsertsInsteadOfBlockingQueue()
    {
        this.service.Save(NewAlarm("A"));
        await this.Sync();
        var alarm = Assert.Single(this.Alarms);
        var oldId = alarm.GoogleEventId;

        this.server.Clock = Now.AddMinutes(-10);
        this.server.Cancel(oldId!);
        this.service.Save(alarm with { Label = "Новее удаления" });

        await this.Sync();

        var local = Assert.Single(this.Alarms);
        Assert.Equal("Новее удаления", local.Label);
        Assert.NotEqual(oldId, local.GoogleEventId);
        Assert.Equal(1, this.server.Live);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_LocalDeleteNotYetPushed_DoesNotReimportEvent()
    {
        this.service.Save(NewAlarm("A"));
        await this.Sync();
        var alarm = Assert.Single(this.Alarms);
        this.server.Clock = Now;
        this.server.Edit(alarm.GoogleEventId!, e => e with { Summary = "Правка в Google" });
        this.service.Delete(alarm.Id);

        await this.Sync();

        Assert.Empty(this.Alarms);
        Assert.Equal(0, this.server.Live);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_ConcurrentCalls_Serialized()
    {
        this.service.Save(NewAlarm("A", 1));
        this.service.Save(NewAlarm("B", 2));
        var inFlight = 0;
        var maxInFlight = 0;
        this.server.Hook = async _ =>
        {
            maxInFlight = Math.Max(maxInFlight, Interlocked.Increment(ref inFlight));
            await Task.Delay(5);
            Interlocked.Decrement(ref inFlight);
        };

        await Task.WhenAll(this.Sync(), this.Sync(), this.Sync());

        Assert.Equal(1, maxInFlight);
        Assert.Equal(2, this.Alarms.Count);
        Assert.Equal(2, this.server.Live);
        Assert.Equal(2, this.server.All.Count);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_ModifiedInstanceOfRecurringEvent_IsIgnored()
    {
        // «Только это событие» приходит отдельным событием с recurringEventId: отдельной записи из него быть не должно.
        this.server.Add(Ev("g-d", Now.AddHours(2), "Ежедневно", recurrence: ["RRULE:FREQ=DAILY"]));
        this.server.Add(Ev("g-d_inst", Now.AddDays(1).AddHours(2), "Завтра иначе") with { RecurringEventId = "g-d" });

        await this.Sync();

        var alarm = Assert.Single(this.Alarms);
        Assert.Equal("g-d", alarm.GoogleEventId);
        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
        Assert.Equal("Ежедневно", alarm.Label);
        Assert.Equal(2, this.server.Live);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
    }

    [Fact]
    public async Task Sync_FullLoad_InstanceWithMasterChronoIdBeforeMaster_StillLoadsSeries()
    {
        // Экземпляр копирует chronoId мастера и приходит раньше него: не должен занять Id записи, иначе мастер сочтётся копией.
        var chronoId = Guid.NewGuid();
        this.server.Add(Ev("g-d_inst", Now.AddDays(1).AddHours(2), "Завтра иначе", chronoId) with { RecurringEventId = "g-d" });
        this.server.Add(Ev("g-d", Now.AddHours(2), "Ежедневно", chronoId, ["RRULE:FREQ=DAILY"]));

        await this.Sync();

        var alarm = Assert.Single(this.Alarms);
        Assert.Equal(chronoId, alarm.Id);
        Assert.Equal("g-d", alarm.GoogleEventId);
        Assert.Equal(RepeatKind.Daily, alarm.Repeat);
        Assert.Equal(2, this.server.Live);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
    }

    [Fact]
    public async Task Sync_ForeignSeries_FiredMovesToNextInstanceOnEverySync()
    {
        var first = new DateTimeOffset(Now.AddDays(5), TimeSpan.Zero);
        var second = new DateTimeOffset(Now.AddDays(35), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-25), "Раз в месяц", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(first, first.AddMinutes(30)), (second, second.AddMinutes(30))];
        await this.Sync();
        Assert.Equal(first.DateTime, Assert.Single(this.Alarms).At);

        // Сработала и уже идёт (конец экземпляра впереди): GetAll выключает разовую запись, а в Google ничего не менялось.
        this.time.Now = first.DateTime.AddMinutes(5);
        var result = await this.Sync();

        var alarm = Assert.Single(this.Alarms);
        Assert.Equal(second.DateTime, alarm.At);
        Assert.Equal(DateOnly.FromDateTime(second.DateTime), alarm.StartsOn);
        Assert.True(alarm.IsEnabled);
        Assert.True(alarm.ForeignSeries);
        Assert.Equal("g-m", alarm.GoogleEventId);
        Assert.Equal(1, result.Pulled);
        Assert.Equal(1, this.server.Live);
        Assert.Equal(0, this.server.Count("PUT"));
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);

        // Следующий проход ничего не меняет.
        var again = await this.Sync();
        Assert.Equal(0, again.Pulled);
        Assert.Equal(second.DateTime, Assert.Single(this.Alarms).At);
    }

    [Fact]
    public async Task Sync_WeeklyStartingOutsideMask_EchoKeepsStartsOn_AddedStartDayNotSkipped()
    {
        // Now — вторник 20-го. Запись начинается во вторник, маска Ср+Пт: DTSTART в Google — среда 21-го.
        var startsOn = new DateOnly(2026, 10, 20);
        this.service.Save(new Alarm
        {
            At = new DateTime(2026, 10, 21, 7, 0, 0), StartsOn = startsOn, Repeat = RepeatKind.Weekly,
            Days = WeekDays.Wednesday | WeekDays.Friday, Label = "Зал",
        });
        await this.Sync();
        Assert.StartsWith("2026-10-21T07:00", Assert.Single(this.server.All).Start!.DateTime);

        var echo = await this.Sync();

        Assert.Equal(0, echo.Pulled);
        Assert.Equal(startsOn, Assert.Single(this.Alarms).StartsOn);

        // Во вторник добавили вторник в маску: он не должен быть пропущен (DTSTART — вторник 20-го).
        this.service.Save(Assert.Single(this.Alarms) with { Days = WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Friday });
        await this.Sync();

        Assert.StartsWith("2026-10-20T07:00", Assert.Single(this.server.All).Start!.DateTime);
        Assert.Equal(startsOn, Assert.Single(this.Alarms).StartsOn);
    }

    [Fact]
    public async Task Sync_EmptyLabel_EchoDoesNotFillLabel()
    {
        this.service.Save(new Alarm { At = Now.AddHours(1) });
        await this.Sync();
        Assert.Equal("Будильник", Assert.Single(this.server.All).Summary);

        var again = await this.Sync();

        Assert.Equal(0, again.Pulled);
        Assert.Equal("", Assert.Single(this.Alarms).Label);
    }

    [Fact]
    public async Task Sync_ForeignSeries_InstanceRequestTimesOut_SkipsRecordLikeNetworkError()
    {
        var first = new DateTimeOffset(Now.AddDays(5), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-25), "Раз в месяц", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(first, first.AddMinutes(30))];
        await this.Sync();

        this.time.Now = first.DateTime.AddHours(1);
        this.service.Save(new Alarm { At = this.time.Now.AddHours(1), Label = "Z" });
        // Таймаут HttpClient: TaskCanceledException при том, что токен вызывающего не отменён.
        this.server.Hook = key => key.EndsWith("/instances", StringComparison.Ordinal) ? throw new TaskCanceledException("таймаут") : Task.CompletedTask;
        await this.Sync();

        Assert.Equal(["Z", "Раз в месяц"], this.Alarms.Select(a => a.Label).Order(StringComparer.Ordinal));
        Assert.Equal(2, this.server.Live);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_ForeignSeries_CallerCancelled_PropagatesCancellation()
    {
        var first = new DateTimeOffset(Now.AddDays(5), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-25), "Раз в месяц", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(first, first.AddMinutes(30))];
        await this.Sync();

        this.time.Now = first.DateTime.AddHours(1);
        using var cts = new CancellationTokenSource();
        this.server.Hook = key =>
        {
            if (!key.EndsWith("/instances", StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            cts.Cancel();
            throw new TaskCanceledException("отмена", null, cts.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => this.calendarSync.SyncAsync(cts.Token));
    }

    [Fact]
    public async Task Sync_ForeignSeries_NoNextInstance_DeletesRecordOnly()
    {
        var only = new DateTimeOffset(Now.AddDays(5), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-25), "Последний раз", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(only, only.AddMinutes(30))];
        await this.Sync();
        Assert.Single(this.Alarms);

        this.time.Now = only.DateTime.AddHours(1);
        var result = await this.Sync();

        Assert.Equal(1, result.Deleted);
        Assert.Empty(this.Alarms);
        Assert.Equal(1, this.server.Live);
        Assert.Equal(0, this.server.Count("DELETE"));

        this.server.ExpireToken = true;
        await this.Sync();
        Assert.Empty(this.Alarms);
    }

    [Fact]
    public async Task Sync_ForeignSeries_NextInstanceFails_SkipsRecordAndRestOfPassContinues()
    {
        var first = new DateTimeOffset(Now.AddDays(5), TimeSpan.Zero);
        this.server.Add(Ev("g-m", Now.AddDays(-25), "Раз в месяц", recurrence: ["RRULE:FREQ=MONTHLY"]));
        this.server.Instances["g-m"] = [(first, first.AddMinutes(30))];
        await this.Sync();

        this.time.Now = first.DateTime.AddHours(1);
        this.service.Save(new Alarm { At = this.time.Now.AddHours(1), Label = "Z" });
        this.server.Fail = key => key.EndsWith("/instances", StringComparison.Ordinal);
        await this.Sync();

        Assert.Equal(["Z", "Раз в месяц"], this.Alarms.Select(a => a.Label).Order(StringComparer.Ordinal));
        Assert.Equal(first.DateTime, this.Alarms.Single(a => a.ForeignSeries).At);
        Assert.Equal(2, this.server.Live);
        Assert.Empty(this.Pending);
    }

    [Fact]
    public async Task Sync_PastGoogleEventWithChronoIdOfUnlinkedLocal_LinksInsteadOfDuplicating()
    {
        // Событие в прошлом FromEvent пропускает, но запись в будущем с тем же chronoId должна получить связь, а не второе событие.
        var alarm = NewAlarm("Будущая");
        this.plain.Save(alarm);
        this.server.Add(Ev("g-old", Now.AddDays(-1), "Старая", alarm.Id));

        await this.Sync();

        var local = Assert.Single(this.Alarms);
        Assert.Equal("g-old", local.GoogleEventId);
        Assert.Equal("Будущая", local.Label);
        Assert.Single(this.server.All);
        Assert.Equal(0, this.server.Count("POST calendars/cal-1/events"));
        Assert.Empty(this.Pending);
    }

    private sealed class FakeAuth : IGoogleAuth
    {
        public bool NoToken { get; init; }

        public Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct) =>
            Task.FromResult<GoogleToken?>(this.NoToken ? null : new GoogleToken("token", TimeSpan.FromHours(1)));

        public Task SignOutAsync() => Task.CompletedTask;
    }

    /// <summary>Календарь «Chrono» в памяти: calendarList, calendars, events (list/insert/update/delete/instances) и syncToken.</summary>
    private sealed class FakeCalendar(DateTime clock) : HttpMessageHandler
    {
        private const string CalendarId = "cal-1";

        private static readonly JsonSerializerOptions Json = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        private readonly List<Stored> events = [];
        private long seq;
        private int nextId;

        /// <summary>Время поля updated у следующих записей на сервере (UTC).</summary>
        public DateTime Clock { get; set; } = clock;

        public bool Exists { get; set; }

        public int CalendarsCreated { get; private set; }

        public bool ExpireToken { get; set; }

        public List<string> Log { get; } = [];

        /// <summary>Вызывается до обработки запроса; ключ — «METHOD путь».</summary>
        public Func<string, Task>? Hook { get; set; }

        /// <summary>true — обрыв сети до обработки запроса.</summary>
        public Func<string, bool>? Fail { get; set; }

        /// <summary>true — запрос обработан сервером, но ответ потерян.</summary>
        public Func<string, bool>? DropResponse { get; set; }

        public Dictionary<string, (DateTimeOffset Start, DateTimeOffset End)[]> Instances { get; } = [];

        public IReadOnlyList<GoogleEvent> All => this.events.Select(e => e.Ev).ToList();

        public int Live => this.events.Count(e => e.Ev.Status != "cancelled");

        public int Count(string prefix) => this.Log.Count(l => l.StartsWith(prefix, StringComparison.Ordinal));

        public void Add(GoogleEvent ev) => this.events.Add(this.Stamp(new Stored(), ev with { Id = ev.Id ?? $"srv-{++this.nextId}" }));

        public void Edit(string id, Func<GoogleEvent, GoogleEvent> change)
        {
            var stored = this.events.Single(e => e.Ev.Id == id);
            this.Stamp(stored, change(stored.Ev));
        }

        public void Cancel(string id) => this.Stamp(this.events.Single(e => e.Ev.Id == id), new GoogleEvent { Id = id, Status = "cancelled" });

        private Stored Stamp(Stored stored, GoogleEvent ev)
        {
            stored.Ev = ev with { Updated = new DateTimeOffset(DateTime.SpecifyKind(this.Clock, DateTimeKind.Utc)) };
            stored.Seq = ++this.seq;
            return stored;
        }

        private static HttpResponseMessage Reply(object body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath["/calendar/v3/".Length..];
            var key = $"{request.Method} {path}";
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            this.Log.Add(key);
            if (this.Hook is not null)
            {
                await this.Hook(key);
            }

            if (this.Fail?.Invoke(key) == true)
            {
                throw new HttpRequestException("нет сети");
            }

            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            var parts = path.Split('/');
            HttpResponseMessage response;
            if (request.Method == HttpMethod.Get && path == "users/me/calendarList")
            {
                response = Reply(new { items = this.Exists ? new[] { new { id = CalendarId, summary = "Chrono", accessRole = "owner" } } : [] });
            }
            else if (request.Method == HttpMethod.Post && path == "calendars")
            {
                this.Exists = true;
                this.CalendarsCreated++;
                response = Reply(new { id = CalendarId, summary = "Chrono" });
            }
            else if (parts is ["calendars", CalendarId, "events", ..])
            {
                response = parts.Length switch
                {
                    3 when request.Method == HttpMethod.Get => this.List(query["syncToken"]),
                    3 => this.Insert(JsonSerializer.Deserialize<GoogleEvent>(body!, Json)!),
                    4 when request.Method == HttpMethod.Put => this.Update(parts[3], JsonSerializer.Deserialize<GoogleEvent>(body!, Json)!),
                    4 => this.Remove(parts[3]),
                    _ => this.ListInstances(parts[3], DateTimeOffset.Parse(query["timeMin"]!), int.Parse(query["maxResults"]!)),
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (this.DropResponse?.Invoke(key) == true)
            {
                throw new HttpRequestException("ответ потерян");
            }

            return response;
        }

        private HttpResponseMessage List(string? syncToken)
        {
            if (syncToken is not null && this.ExpireToken)
            {
                return new HttpResponseMessage(HttpStatusCode.Gone);
            }

            var since = syncToken is null ? 0 : long.Parse(syncToken["tok-".Length..]);
            return Reply(new { items = this.events.Where(e => e.Seq > since).Select(e => e.Ev).ToArray(), nextSyncToken = $"tok-{this.seq}" });
        }

        private HttpResponseMessage Insert(GoogleEvent ev)
        {
            var created = ev with { Id = ev.Id ?? $"srv-{++this.nextId}", Status = "confirmed" };
            this.events.Add(this.Stamp(new Stored(), created));
            return Reply(this.events[^1].Ev);
        }

        private HttpResponseMessage Update(string id, GoogleEvent ev)
        {
            var stored = this.events.SingleOrDefault(e => e.Ev.Id == id);
            if (stored is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (stored.Ev.Status == "cancelled")
            {
                return new HttpResponseMessage(HttpStatusCode.Gone);
            }

            this.Stamp(stored, ev with { Id = id, Status = "confirmed" });
            return Reply(stored.Ev);
        }

        private HttpResponseMessage Remove(string id)
        {
            var stored = this.events.SingleOrDefault(e => e.Ev.Id == id);
            if (stored is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            this.Stamp(stored, new GoogleEvent { Id = id, Status = "cancelled" });
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        private HttpResponseMessage ListInstances(string id, DateTimeOffset timeMin, int maxResults)
        {
            // Как в настоящем API: timeMin отбирает по КОНЦУ экземпляра.
            var first = this.Instances.GetValueOrDefault(id, []).Where(i => i.End > timeMin).Take(maxResults)
                .Select(i => new GoogleEvent
                {
                    Id = id + "_inst",
                    Start = new GoogleEventTime { DateTime = i.Start.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
                    End = new GoogleEventTime { DateTime = i.End.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
                }).ToArray();
            return Reply(new { items = first });
        }

        private sealed class Stored
        {
            public GoogleEvent Ev { get; set; } = new();

            public long Seq { get; set; }
        }
    }
}
