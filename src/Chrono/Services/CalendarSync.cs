using System.Globalization;
using Chrono.Models;

namespace Chrono.Services;

/// <summary>
/// Итог прохода. Pushed — события, вставленные, обновлённые или удалённые в Google; Pulled — записи, созданные или
/// изменённые в Chrono по событиям Google; Deleted — записи, удалённые в Chrono по отмене события в Google.
/// </summary>
public sealed record SyncResult(int Pushed, int Pulled, int Deleted, bool FullReload);

/// <summary>
/// Один проход синхронизации с календарём «Chrono» (спецификация 6.3), без MAUI. Одновременно выполняется не более одного прохода.
/// SyncState читается и пишется коротко, до и после каждого сетевого вызова, а не держится через await: иначе затёрся бы
/// Enqueue, который пользователь сделал во время синхронизации. Записи меняются только через AlarmService.
/// </summary>
public sealed class CalendarSync
{
    private readonly GoogleCalendarClient client;
    private readonly AlarmService alarms;
    private readonly SyncStore store;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate = new(1, 1);

    public CalendarSync(GoogleCalendarClient client, AlarmService alarms, SyncStore store, TimeProvider time)
    {
        this.client = client;
        this.alarms = alarms;
        this.store = store;
        this.time = time;
    }

    /// <summary>
    /// Порядок: календарь; приём изменений Google; отправка очереди. Приём идёт первым намеренно: запись без GoogleEventId,
    /// у которой в Google уже есть событие с тем же chronoId (второй телефон, потерянный ответ events.insert), сначала
    /// связывается с ним, а не получает второе событие. Сетевые ошибки и SignInRequiredException пробрасываются наверх,
    /// сделанное к этому моменту уже сохранено, неотправленное осталось в очереди.
    /// </summary>
    public async Task<SyncResult> SyncAsync(CancellationToken ct)
    {
        await this.gate.WaitAsync(ct);
        try
        {
            var zone = this.time.LocalTimeZone;
            int pushed = 0, pulled = 0, deleted = 0;
            var fullReload = false;

            // Время события Google в моменте; для «на весь день» — полночь этого дня в поясе телефона.
            DateTimeOffset? ParseTime(GoogleEventTime? t)
            {
                if (t?.DateTime is { } dateTime)
                {
                    return DateTimeOffset.TryParse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
                }

                if (DateOnly.TryParseExact(t?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                {
                    var midnight = day.ToDateTime(TimeOnly.MinValue);
                    return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
                }

                return null;
            }

            // 1. Календарь. Если id уже сохранён, поиск не нужен (на нём область calendar.app.created может ответить 403).
            var calendarId = this.store.Load().CalendarId;
            if (calendarId is null)
            {
                calendarId = await this.client.FindOrCreateCalendarAsync(ct);
                this.store.Save(this.store.Load() with { CalendarId = calendarId });
            }

            // 2. Приём изменений.
            IReadOnlyList<GoogleEvent> events;
            string nextSyncToken;
            try
            {
                (events, nextSyncToken) = await this.client.ListChangesAsync(calendarId, this.store.Load().SyncToken, ct);
            }
            catch (SyncTokenExpiredException)
            {
                fullReload = true;
                (events, nextSyncToken) = await this.client.ListChangesAsync(calendarId, null, ct);
            }

            foreach (var ev in events)
            {
                var snapshot = this.store.Load();
                if (ev.Id is null || snapshot.IgnoredEventIds.Contains(ev.Id))
                {
                    continue;
                }

                // Локальное удаление ещё не отправлено — оно победит; событие заново не загружаем.
                if (snapshot.Pending.Any(p => p.Op == SyncOp.Delete && p.GoogleEventId == ev.Id))
                {
                    continue;
                }

                // Запись ищем по id события, затем по chronoId. Запись, уже связанная с другим событием, — это копия
                // события (в Google копируются extendedProperties): не трогаем, иначе запись переключалась бы между ними.
                var all = this.alarms.GetAll();
                var existing = all.FirstOrDefault(a => a.GoogleEventId == ev.Id);
                if (existing is null && Guid.TryParse(ev.ExtendedProperties?.Private?.GetValueOrDefault(CalendarMapping.ChronoIdKey), out var chronoId))
                {
                    existing = all.FirstOrDefault(a => a.Id == chronoId);
                    if (existing?.GoogleEventId is not null)
                    {
                        continue;
                    }
                }

                // Конфликт: локальная правка новее события — победит при отправке. Связь с событием всё равно сохраняем,
                // чтобы отправка обновила его, а не вставила второе.
                var local = existing is null ? null : snapshot.Pending.FirstOrDefault(p => p.Op == SyncOp.Upsert && p.AlarmId == existing.Id);
                if (existing is not null && local is not null && ev.Updated is { } updated && updated.UtcDateTime <= local.ChangedAtUtc)
                {
                    if (existing.GoogleEventId is null && ev.Status != "cancelled")
                    {
                        this.alarms.Save(existing with { GoogleEventId = ev.Id }, fromSync: true);
                    }

                    continue;
                }

                var now = this.time.GetLocalNow().DateTime;
                var result = CalendarMapping.FromEvent(ev, zone, now, existing);
                if (result is ImportNeedsInstance)
                {
                    // timeMin в events.instances отбирает по концу события, поэтому может вернуться идущий сейчас экземпляр:
                    // берём первый, чьё начало позже now, а за следующим идём с after = конец полученного.
                    var after = this.time.GetLocalNow();
                    DateTimeOffset? instanceStart = null;
                    for (var step = 0; step < 32; step++)
                    {
                        var instance = await this.client.NextInstanceAsync(calendarId, ev.Id, after, ct);
                        var start = ParseTime(instance?.Start);
                        if (start is null)
                        {
                            break;
                        }

                        if (TimeZoneInfo.ConvertTime(start.Value, zone).DateTime > now)
                        {
                            instanceStart = start;
                            break;
                        }

                        var end = ParseTime(instance!.End);
                        if (end is null || end <= after)
                        {
                            break;
                        }

                        after = end.Value;
                    }

                    result = instanceStart is { } found ? CalendarMapping.FromEvent(ev, zone, now, existing, found) : new ImportSkip("no future instance");
                }

                switch (result)
                {
                    case ImportDelete delete:
                        this.alarms.Delete(delete.AlarmId, fromSync: true);
                        deleted++;
                        break;
                    case ImportUpsert upsert:
                        // Id берём у найденной записи: иначе Save добавил бы вторую, если chronoId события отличается.
                        var incoming = existing is null ? upsert.Alarm : upsert.Alarm with { Id = existing.Id };
                        if (incoming == existing)
                        {
                            // Эхо нашей же отправки: данные те же, записывать нечего.
                            break;
                        }

                        if (!this.alarms.Save(incoming, fromSync: true))
                        {
                            // Время прошло между разбором и записью: пропускаем, очередь не трогаем.
                            continue;
                        }

                        pulled++;
                        break;
                    default:
                        continue;
                }

                // Google победил: локальная правка этой записи заменена, её элемент очереди больше не нужен.
                // Удаляем именно прочитанный элемент: новый Enqueue пользователя (иной ChangedAtUtc) остаётся.
                if (local is not null)
                {
                    var state = this.store.Load();
                    state.Pending.RemoveAll(p => p == local);
                    this.store.Save(state);
                }
            }

            this.store.Save(this.store.Load() with { SyncToken = nextSyncToken });

            // 3. Отправка. Запись без GoogleEventId, не чужая серия и не прошедшая разовая (её связь с событием не
            // сохранить: Save отклоняет At <= now), считается Upsert, даже если её нет в очереди (первый вход).
            var queueNow = this.time.GetLocalNow().DateTime;
            foreach (var alarm in this.alarms.GetAll().Where(a => a.GoogleEventId is null && !a.ForeignSeries && (!a.IsOneShot() || a.At > queueNow)))
            {
                if (!this.store.Load().Pending.Any(p => p.AlarmId == alarm.Id))
                {
                    this.store.Enqueue(new PendingChange(alarm.Id, SyncOp.Upsert, null, this.time.GetUtcNow().UtcDateTime));
                }
            }

            while (this.store.Load().Pending.FirstOrDefault() is { } item)
            {
                if (item.Op == SyncOp.Delete)
                {
                    if (item.GoogleEventId is { } eventId)
                    {
                        await this.client.DeleteAsync(calendarId, eventId, ct);
                        pushed++;
                    }
                }
                else if (this.alarms.GetAll().FirstOrDefault(a => a.Id == item.AlarmId) is { ForeignSeries: false } alarm)
                {
                    // Запись читаем заново по id: снимок из очереди устарел, а правку пользователя затирать нельзя.
                    var gev = CalendarMapping.ToEvent(alarm, zone);
                    GoogleEvent? created = null;
                    if (alarm.GoogleEventId is not null)
                    {
                        try
                        {
                            await this.client.UpdateAsync(calendarId, gev, ct);
                            pushed++;
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
                        {
                            // Событие удалено в Google, а локальная правка новее: создаём заново, очередь не должна застрять.
                            created = await this.client.InsertAsync(calendarId, gev with { Id = null }, ct);
                        }
                    }
                    else if (!alarm.IsOneShot() || alarm.At > this.time.GetLocalNow().DateTime)
                    {
                        created = await this.client.InsertAsync(calendarId, gev, ct);
                    }

                    if (created?.Id is { } newId)
                    {
                        pushed++;

                        // Запись могла измениться или исчезнуть, пока шёл запрос: берём актуальную.
                        if (this.alarms.GetAll().FirstOrDefault(a => a.Id == alarm.Id) is { } fresh)
                        {
                            this.alarms.Save(fresh with { GoogleEventId = newId }, fromSync: true);
                        }
                        else
                        {
                            // Пользователь удалил запись во время вставки: событие без записи вернулось бы при следующем приёме.
                            await this.client.DeleteAsync(calendarId, newId, ct);
                        }
                    }
                }

                // Элемент удаляется только после успеха и только если его не заменил новый Enqueue.
                var queue = this.store.Load();
                queue.Pending.RemoveAll(p => p == item);
                this.store.Save(queue);
            }

            this.store.Save(this.store.Load() with { LastSyncUtc = this.time.GetUtcNow().UtcDateTime });
            return new SyncResult(pushed, pulled, deleted, fullReload);
        }
        finally
        {
            // Звонки должны соответствовать данным и после неудачного прохода.
            this.alarms.RescheduleAll();
            this.gate.Release();
        }
    }
}
