# Вход через Google и синхронизация с Google Календарём — план реализации

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development. Шаги — чекбоксы (`- [ ]`).

**Цель:** вход через Google в «Настройках», календарь «Chrono» и двусторонняя синхронизация записей с его событиями, включая звонки по напоминаниям Google.

**Архитектура:** чистая логика без MAUI (`CalendarMapping`, `CalendarSync`, `GoogleCalendarClient`, `SyncStore`) — в `Services/`, покрыта xUnit через ссылки на файлы, как сейчас. Платформа даёт только токен (`IGoogleAuth`: Android — `AuthorizationClient`, iOS — `WebAuthenticator`). Звонки по-прежнему ставит телефон; синхронизация только меняет записи через `AlarmService` и вызывает `RescheduleAll`.

**Стек:** .NET MAUI 11 (`11.0.0-rc.1.26451.6`), xUnit v3; новые пакеты: `Xamarin.GooglePlayServices.Auth` 122.0.0 (Android), `Xamarin.AndroidX.Work.Runtime` 2.11.2.1 (Android), `Microsoft.Extensions.Caching.Memory` `11.0.0-*` (приложение и тесты; если версия не найдётся при restore — `10.0.12` из локального кэша, записать в журнал). Calendar API — `HttpClient` + `System.Text.Json`, без `Google.Apis.*`.

**Спецификация:** [docs/superpowers/specs/2026-10-20-google-calendar-sync-design.md](../specs/2026-10-20-google-calendar-sync-design.md). Модель записи — из [2026-10-20-daily-schedule-design.md](../specs/2026-10-20-daily-schedule-design.md).

**Ветка:** `feature/google-sync` от `df804cb` (вершина «Распорядка»; там открыты дефекты D1–D3, D5 — журнал `.superpowers/sdd/2026-10-20-daily-schedule/progress.md`; их не чинить в этом плане).

## Глобальные ограничения

- Только календарь «Chrono»; область доступа `https://www.googleapis.com/auth/calendar.app.created` плюс `openid`, `email`. Других календарей не читать и не менять.
- Звонок не зависит от сети: синхронизация пишет записи только через `AlarmService` и после себя вызывает `AlarmService.RescheduleAll()`; ошибка синхронизации не должна мешать сохранению и звонку.
- `AlarmService.Save` по-прежнему отклоняет `At <= now`; прошедшие события Google в Chrono не загружаются.
- Токены — в `IMemoryCache`, ключ `google-access-token`, срок — `expires_in` минус 5 мин; на `401` — `cache.Remove` и одна повторная попытка (правило пользователя).
- Приватные поля без `_`, обращение через `this.`; private-методы-помощники — только при реальной необходимости; общая настройка `HttpClient` — в конструкторе (правило пользователя).
- Новые вычисляемые члены `Alarm` — методы, не свойства (STJ сериализует get-only свойства). Новых коллекций-полей в `Alarm` нет: массив ломает равенство record (решение плана «Распорядка»).
- Комментарии и коммиты на русском; коммит заканчивается `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- ApplicationId Android `com.chrono.alarm`; отладочный ключ — `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`, SHA-1 `37:85:04:7F:1F:D0:63:57:DB:36:88:5C:98:9B:82:3E:DC:DE:8D:E0`.
- Строки интерфейса — в обоих resx; добавляет только задача, указанная в её Files.
- Пароли и учётные данные агенты не вводят; вход в Google на эмуляторе — шаг пользователя.

## Окружение исполнителя

Windows 11, Git Bash, .NET SDK `11.0.100-rc.1`, workloads android/ios. Тесты `dotnet test tests/Chrono.Tests` (базовая линия на `0ff8f94` — 103 зелёных). Сборка `dotnet build src/Chrono/Chrono.csproj -f net11.0-android -o $TEMP/chrono-<задача>` (для установки на эмулятор — ещё `-p:EmbedAssembliesIntoApk=true`), затем `git status`. Эмулятор: adb `C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe`, `emulator.exe -avd chrono_api36`. Задачи волны — в своих git worktree (`git merge --ff-only <вершина ветки>` первым шагом); отчёт — внутри worktree `.superpowers/sdd/2026-10-20-google-calendar-sync/task-N-report.md`.

## Порядок

| Волна | Задачи | Кто |
|---|---|---|
| 1 | 1 — Google Cloud (интерактивно, контроллер с пользователем); 2 — модель и `SyncStore` | контроллер; sonnet |
| 2 | 3 — `CalendarMapping`; 4 — звонки по напоминаниям; 5 — `GoogleCalendarClient` | sonnet ×3 |
| 3 | 6 — `CalendarSync`; 7 — вход Android; 8 — вход iOS | sonnet ×3 |
| 4 | 9 — «Настройки» и запуск синхронизации | sonnet |
| 5 | 10 — приёмка на эмуляторе с пользователем | sonnet + пользователь |

При слиянии волны 2 ожидается конфликт строк в `tests/Chrono.Tests/Chrono.Tests.csproj` (задачи 3 и 5 добавляют ссылки рядом) — разрешает контроллер, сохраняя обе.

Ревью: 4, 6, 7 — opus (звонки, слияние данных, вход); остальные — sonnet; финальное — opus.

## Решения, принятые при планировании

1. **Напоминания хранятся строкой** `Alarm.ReminderMinutes` (`"30"`, `"30,10"`, пусто), разбор — метод `Reminders()`. Массив сломал бы равенство record.
2. **Очередь изменений — в `sync.json`, не в записи.** `AlarmService.Save/Toggle/Delete` добавляют запись в очередь, если не вызваны с `fromSync: true`. Изменения от `GetAll` (сдвиг повтора, автоудаление «Один раз и удалить») и от `SmartWakeService`/`BootReceiver` (свой экземпляр `AlarmService` без очереди) в Google не уходят — так и задумано: «Один раз и удалить» остаётся в Google, а сдвиг `At` повтора не меняет событие.
3. **Конфликт:** у элемента очереди есть `ChangedAtUtc`; при приёме изменения события с тем же `chronoId`/`GoogleEventId`, если `event.updated > ChangedAtUtc` — побеждает Google (элемент очереди удаляется), иначе — локальная правка (событие будет перезаписано отправкой).
4. **`reminders.useDefault = true`** у события считается «без напоминаний»: у нового вторичного календаря нет напоминаний по умолчанию, а читать настройки календаря областью `calendar.app.created` нельзя.
5. **Неподдерживаемый повтор** (есть `INTERVAL` ≠ 1, `COUNT`, `UNTIL`, `BYMONTHDAY`, `FREQ=MONTHLY` и т. п.): ближайшее повторение берётся `events.instances(timeMin=now, maxResults=1)`, запись — разовая с `ForeignSeries = true`; её локальные правки в Google **не отправляются**, ручное удаление удаляет только в Chrono и заносит id события в `IgnoredEventIds`, чтобы не загрузить снова. *(Отступление от спецификации 6.2 «не перезаписывается, пока пользователь не изменил запись»: отправка правки превратила бы чужую серию в разовое событие. Сообщить пользователю.)*
6. **Поиск календаря «Chrono» на новом телефоне** — `calendarList.list` с отбором `summary == "Chrono"`. `calendar.app.created` этот метод не даёт, поэтому по решению пользователя (Ruling 6 в журнале) запрашиваются области `calendar.app.created`, `calendar.calendarlist.readonly` (non-sensitive), `openid`, `email`. В задаче 10 это проверяется на живом аккаунте, сценарии 1 и 6.
7. **Отправка записи** использует `events.insert` (нет `GoogleEventId`) или `events.update` (есть); после вставки `GoogleEventId` сохраняется в записи через `AlarmService.Save(…, fromSync: true)`.
8. **Запись, выключенная пользователем,** остаётся событием в Google с `extendedProperties.private.enabled = "false"`.
9. **Вход на Android** — `AuthorizationClient` без offline-доступа; веб-клиент OAuth не нужен (Android-клиент опознаётся по пакету и SHA-1). Email — запрос `https://openidconnect.googleapis.com/v1/userinfo`.

## Review Focus

1. Первый вход на телефоне, где уже есть записи, и затем вход на втором телефоне с тем же аккаунтом → ни одного дубля ни в Chrono, ни в Google. Тест — задача 6 `Sync_SecondDevice_MatchesByChronoIdWithoutDuplicates`.
2. Нет сети или токен отозван посреди синхронизации → записи на телефоне не теряются, очередь не теряется, звонки стоят. Тест — задача 6 `Sync_NetworkFailureMidPush_KeepsQueueAndAlarms`.
3. Событие Google с напоминанием 30 мин → два звонка, и после правки записи в Chrono напоминание в Google не исчезает. Тесты — задача 3 `ToEvent_KeepsReminderMinutes`, задача 4 `AlertTimes_ReminderAndStart`.
4. Пояс времени: событие создано в Google в другом поясе → в Chrono местное время; запись Chrono в Google — с поясом телефона. Тест — задача 3 `FromEvent_OtherZone_ConvertsToLocalWallClock`.
5. `410 Gone` на `syncToken` → полная загрузка без дублей и без повторного появления сработавших «Один раз и удалить». Тест — задача 6 `Sync_SyncTokenExpired_FullReloadWithoutDuplicates`.

---

### Задача 1: Google Cloud (контроллер с пользователем, интерактивно)

Не для субагента: действия в аккаунте пользователя через браузер, каждый шаг с принятием условий, вводом данных или изменением настроек — с отдельным подтверждением пользователя в чате.

- [ ] Проект Google Cloud «Chrono» (или существующий — по выбору пользователя).
- [ ] Включить Google Calendar API.
- [ ] Экран согласия / Audience: тип External, название «Chrono», email поддержки — пользователя (подтверждение), области `calendar.app.created`, `openid`, `email`; **статус публикации — «In production»** (спецификация 6.1), проверку не запускать. Записать, какую классификацию консоль показывает у `calendar.app.created` (sensitive / non-sensitive).
- [ ] OAuth-клиент Android: пакет `com.chrono.alarm`, SHA-1 из «Глобальных ограничений».
- [ ] OAuth-клиент iOS: bundle id — `ApplicationId` из `Chrono.csproj` (`com.chrono.alarm`). Записать client id и схему перенаправления (`com.googleusercontent.apps.<…>`).
- [ ] В журнал: id проекта, client id iOS, классификацию области. Client id не секрет; секретов в репозиторий не класть.

### Задача 2: модель записи и `SyncStore`

**Files:** Modify `src/Chrono/Models/Alarm.cs`, `src/Chrono/Services/AlarmService.cs`; Create `src/Chrono/Services/SyncStore.cs`; Modify `tests/Chrono.Tests/Chrono.Tests.csproj` (ссылка на `SyncStore.cs`); Test `tests/Chrono.Tests/SyncStoreTests.cs`, `AlarmServiceTests.cs`, `AlarmModelTests.cs`.

**Produces:**
```csharp
// Alarm — новые init-свойства и метод:
public string? GoogleEventId { get; init; }        // null — ещё не отправлена
public string ReminderMinutes { get; init; } = ""; // "30,10"; решение 1
public bool ForeignSeries { get; init; }           // решение 5
public IReadOnlyList<int> Reminders();             // разбор ReminderMinutes, пропуск мусора, по убыванию
public IReadOnlyList<DateTime> AlertTimes(DateTime occurrence); // [occurrence − m для m из Reminders()] + occurrence, по возрастанию, без дублей

// Services/SyncStore.cs — файл sync.json рядом с alarms.json, атомарная запись как в AlarmStore:
public enum SyncOp { Upsert, Delete }
public sealed record PendingChange(Guid AlarmId, SyncOp Op, string? GoogleEventId, DateTime ChangedAtUtc);
public sealed record SyncState
{
    public string? CalendarId { get; init; }
    public string? SyncToken { get; init; }
    public string? Email { get; init; }
    public DateTime? LastSyncUtc { get; init; }
    public List<PendingChange> Pending { get; init; } = [];
    public List<string> IgnoredEventIds { get; init; } = [];
}
public sealed class SyncStore
{
    public SyncStore(string directory);
    public SyncState Load();                     // нет файла или JsonException → new SyncState()
    public void Save(SyncState state);
    public void Enqueue(PendingChange change);   // заменяет прежний элемент с тем же AlarmId
}

// AlarmService — новый необязательный параметр конструктора и флаги:
public AlarmService(AlarmStore store, IAlarmScheduler scheduler, TimeProvider time, SyncStore? sync = null);
public bool Save(Alarm alarm, bool fromSync = false);   // !fromSync && sync != null → Enqueue(Upsert, ChangedAtUtc = time.GetUtcNow())
public bool Toggle(Guid id, bool enabled);              // всегда Enqueue(Upsert) при успехе
public void Delete(Guid id, bool fromSync = false);     // !fromSync: ForeignSeries → IgnoredEventIds += GoogleEventId; иначе при GoogleEventId != null → Enqueue(Delete)
public event EventHandler? Changed;                      // после успешных Save/Toggle/Delete без fromSync (для запуска синхронизации, задача 9)
```
Для `PendingChange` равенство record со `string?` безопасно; `List` внутри `SyncState` — допустимо (сравнение `SyncState` не используется).

- [ ] **Тесты:** `Reminders_ParsesAndSkipsGarbage` (`"30, x,10,30"` → `[30, 10]`); `AlertTimes_ReminderAndStart` (07:00, `"30"` → 06:30, 07:00); `AlertTimes_NoReminders_OnlyStart`; `GetAll_LegacyJsonWithoutSyncFields_Defaults` (`GoogleEventId == null`, `ReminderMinutes == ""`, `ForeignSeries == false`); `SyncStore_RoundTripAndCorruptFile`; `Enqueue_ReplacesSameAlarm`; `Save_EnqueuesUpsert_UnlessFromSync`; `Delete_WithEventId_EnqueuesDelete`; `Delete_ForeignSeries_AddsIgnoredAndNoQueue`; `Delete_WithoutEventId_NoQueue`; `GetAll_AutoDeletedOnceThenDelete_NotQueued`; `Changed_RaisedOnlyWithoutFromSync`; `Service_WithoutSyncStore_WorksAsBefore` (все 103 прежних теста зелёные без правок, кроме добавления новых полей в ожидания, если нужно).
- [ ] RED → реализация → GREEN; мутация: убрать проверку `fromSync` в `Save` — `Save_EnqueuesUpsert_UnlessFromSync` краснеет.
- [ ] Сборка Android; коммит `Синхронизация: поля записи, очередь изменений sync.json`.

### Задача 3: `CalendarMapping` — запись ↔ событие

**Files:** Create `src/Chrono/Services/GoogleCalendarModels.cs` (DTO), `src/Chrono/Services/CalendarMapping.cs`; Modify `tests/Chrono.Tests/Chrono.Tests.csproj`; Test `tests/Chrono.Tests/CalendarMappingTests.cs`.

**Consumes:** задача 2. **Produces:**
```csharp
// DTO с [JsonPropertyName] по API v3: id, status, summary, start/end {dateTime, date, timeZone},
// recurrence (string[]?), reminders {useDefault, overrides[{method, minutes}]},
// extendedProperties {private (Dictionary<string,string>)}, updated (DateTimeOffset?).
public sealed record GoogleEvent { … }
public sealed record GoogleEventTime { … }
public sealed record GoogleReminders { … }
public sealed record GoogleReminder(string Method, int Minutes);

public abstract record ImportResult;
public sealed record ImportUpsert(Alarm Alarm) : ImportResult;
public sealed record ImportDelete(Guid AlarmId) : ImportResult;
public sealed record ImportSkip(string Reason) : ImportResult;            // прошедшее и т. п.
public sealed record ImportNeedsInstance(string EventId) : ImportResult;   // неподдерживаемый повтор

public static class CalendarMapping
{
    public const string ChronoIdKey = "chronoId";   // и ключи alert, icon, soundId, soundEnabled, vibration, enabled, repeat
    public static GoogleEvent ToEvent(Alarm alarm, TimeZoneInfo zone);
    // existing — запись с тем же GoogleEventId или chronoId, если есть; instanceStart — для ImportNeedsInstance второго прохода
    public static ImportResult FromEvent(GoogleEvent ev, TimeZoneInfo zone, DateTime now, Alarm? existing, DateTimeOffset? instanceStart = null);
}
```
Правила — спецификация 6.2 и решения 1, 4, 5, 8 плана; дополнительно:
- `ToEvent`: `start.dateTime` — для разовой `At`, для повтора `StartsOn` + время суток `At`; формат ISO 8601 со смещением пояса `zone` на эту дату; `timeZone = zone.Id`. `end = start + DurationMinutes`. `Weekly` с `Days == None` → `FREQ=DAILY`. `BYDAY` — `MO,TU,WE,TH,FR,SA,SU` в порядке с понедельника. `reminders`: `useDefault = false`, `overrides` = `Reminders()` с `method = "popup"`. `summary` — `Label`.
- `FromEvent`: `status == "cancelled"` → `ImportDelete` (если `existing != null`, иначе `ImportSkip`). Весь день (`start.date`) → разовая 00:00, `Alert = None`. Время → местное «настенное» (`TimeZoneInfo.ConvertTime` в `zone`, `Kind = Unspecified`). Разовая с началом `<= now` → `ImportSkip`. Повтор: `At = NextAfter(now)` с `StartsOn` = местная дата начала. `Id` = `chronoId` из свойств, иначе `existing?.Id`, иначе новый `Guid`. Без `extendedProperties` → `Alert = Ring`, `Icon = "bell"`. `ReminderMinutes` — из `overrides` (`useDefault` → пусто). `GoogleEventId = ev.Id`.

- [ ] **Тесты** (пояс телефона в тестах — `TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent")` (+05:00, без летнего времени); `now = 2026-10-20 12:00`):
  - `ToEvent_Daily_RruleAndStartFromStartsOn` — `StartsOn 26.10`, 07:15, 30 мин → `start.dateTime == "2026-10-26T07:15:00+05:00"`, `end` 07:45, `recurrence == ["RRULE:FREQ=DAILY"]`.
  - `ToEvent_WeeklyMonWed_ByDay` → `"RRULE:FREQ=WEEKLY;BYDAY=MO,WE"`; `ToEvent_WeeklyNoDays_Daily`; `ToEvent_Yearly`; `ToEvent_OnceThenDelete_NoRecurrenceAndRepeatProperty`.
  - `ToEvent_KeepsReminderMinutes` — `"30"` → `overrides == [popup 30]`, `useDefault == false`.
  - `ToEvent_Disabled_EnabledFalseProperty`.
  - `FromEvent_GoogleCreated_RingBellAndReminders` — без свойств, напоминание 30 → `Alert == Ring`, `Icon == "bell"`, `ReminderMinutes == "30"`.
  - `FromEvent_OtherZone_ConvertsToLocalWallClock` — `"2026-10-21T10:00:00+03:00"` → `At == 2026-10-21 12:00`.
  - `FromEvent_PastOneShot_Skip`; `FromEvent_InProgressOneShot_Skip`; `FromEvent_AllDay_NoneAtMidnight`.
  - `FromEvent_Cancelled_DeleteWhenExisting_SkipOtherwise`.
  - `FromEvent_ChronoIdProperty_KeepsIdAndFields` (round-trip: `FromEvent(ToEvent(a))` даёт запись, равную `a` с `GoogleEventId` события, при `a.At > now`).
  - `FromEvent_UnsupportedRule_NeedsInstance` — `FREQ=MONTHLY`, `FREQ=WEEKLY;INTERVAL=2`, `COUNT=5` → `ImportNeedsInstance`; со вторым вызовом с `instanceStart` → разовая, `ForeignSeries == true`.
  - `FromEvent_UseDefaultReminders_None`.
- [ ] RED → реализация → GREEN; мутация: убрать перевод пояса — `FromEvent_OtherZone_…` краснеет.
- [ ] Коммит `Синхронизация: соответствие записи и события Google`.

### Задача 4: звонки по напоминаниям

**Files:** Modify `src/Chrono/Platforms/Android/AlarmScheduler.cs`, `src/Chrono/Platforms/Android/AlarmReceiver.cs`, `src/Chrono/Platforms/Android/ScheduleReceiver.cs` (только PendingIntent-фабрики), `src/Chrono/Services/IosNotificationPlan.cs`; Test `tests/Chrono.Tests/IosNotificationPlanTests.cs`.

**Consumes:** `Alarm.AlertTimes`, `Alarm.Reminders` (задача 2).
- Android `Sync`: для включённой будущей записи — по одному точному таймеру на каждый момент из `AlertTimes(At)`, который позже `now`. Ring → `SetAlarmClock` на `AlarmReceiver`, Notify → `SetExactAndAllowWhileIdle` на `ScheduleReceiver`. PendingIntent напоминания отличается Data: `chrono://alarm/{id}/r{минуты}` и `chrono://notify/{id}/r{минуты}`; основной (момент начала) — прежний без суффикса. Снятие: для каждой записи снимаются основной и напоминания для минут `5, 10, 15, 30, 60, 120, 1440` и всех из `Reminders()` — `ponytail:` старое напоминание с иным числом минут после смены в Google снимется только при совпадении со списком; полный учёт — хранить прошлые минуты. `Cancel(id)` — то же. Окно умного пробуждения — только от момента начала.
- `IosNotificationPlan.Build`: срабатывания — моменты `AlertTimes(occurrence)` каждого повторения; лимит `MaxAlarms` — на моменты; идентификаторы напоминаний — `{id}-r{минуты}-{k}`; `ChainIds` основного срабатывания не меняется.
- [ ] **Тесты** `IosNotificationPlan`: `Build_Reminder_AddsEarlierNotification` (Ring, `"30"` → цепочка в 06:30 и в 07:00); `Build_NotifyReminder_SingleEach`; `Build_RemindersShareLimit` (лимит 16 моментов); прежние тесты зелёные.
- [ ] Сборка Android и iOS; ручная проверка на эмуляторе, если запущен: запись Ring на +3 мин с `ReminderMinutes = "2"` (правкой `alarms.json` через `adb shell run-as com.chrono.alarm`) звонит дважды. Иначе — отметить для задачи 10.
- [ ] Коммит `Звонки по напоминаниям Google`.

### Задача 5: `GoogleCalendarClient`

**Files:** Create `src/Chrono/Services/GoogleCalendarClient.cs`, `src/Chrono/Services/IGoogleAuth.cs`; Modify `src/Chrono/Chrono.csproj` (`Microsoft.Extensions.Caching.Memory`), `tests/Chrono.Tests/Chrono.Tests.csproj` (тот же пакет + ссылки); Test `tests/Chrono.Tests/GoogleCalendarClientTests.cs` (фейковый `HttpMessageHandler`).

**Consumes:** DTO задачи 3. **Produces:**
```csharp
public sealed record GoogleToken(string AccessToken, TimeSpan ExpiresIn);
public interface IGoogleAuth
{
    /// <summary>interactive=false — только без интерфейса; null — нужен вход.</summary>
    Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct);
    Task SignOutAsync();
}
public sealed class SyncTokenExpiredException : Exception { }
public sealed class SignInRequiredException : Exception { }

public sealed class GoogleCalendarClient
{
    public GoogleCalendarClient(HttpClient http, IMemoryCache cache, IGoogleAuth auth); // BaseAddress https://www.googleapis.com/calendar/v3/ и Accept — в конструкторе
    public Task<string> FindOrCreateCalendarAsync(CancellationToken ct);   // calendarList.list summary=="Chrono", иначе calendars.insert {summary:"Chrono", timeZone}
    public Task<(IReadOnlyList<GoogleEvent> Events, string NextSyncToken)> ListChangesAsync(string calendarId, string? syncToken, CancellationToken ct); // showDeleted=true; постранично pageToken; 410 → SyncTokenExpiredException
    public Task<GoogleEvent> InsertAsync(string calendarId, GoogleEvent ev, CancellationToken ct);
    public Task<GoogleEvent> UpdateAsync(string calendarId, GoogleEvent ev, CancellationToken ct);   // PUT events/{id}
    public Task DeleteAsync(string calendarId, string eventId, CancellationToken ct);                // 404/410 — не ошибка
    public Task<GoogleEvent?> NextInstanceAsync(string calendarId, string eventId, DateTimeOffset after, CancellationToken ct);
    public Task<string?> GetEmailAsync(CancellationToken ct); // https://openidconnect.googleapis.com/v1/userinfo
}
```
Токен — по «Глобальным ограничениям»; `GetTokenAsync(false)` вернул `null` → `SignInRequiredException`. Полная загрузка (`syncToken == null`) — без `timeMin`: прошлое отбрасывает `CalendarMapping`.

- [ ] **Тесты:** `Token_CachedUntilExpiry` (два запроса — один вызов `IGoogleAuth`); `Unauthorized_RemovesTokenAndRetriesOnce` (401 → новый токен → успех; второй 401 → исключение); `NoToken_SignInRequired`; `ListChanges_FollowsPagesAndReturnsSyncToken`; `ListChanges_410_SyncTokenExpired`; `Delete_404_IsNotError`; `FindOrCreate_FindsExistingByName_ElseCreates`.
- [ ] RED → GREEN; мутация: убрать `cache.Remove` на 401 — `Unauthorized_…` краснеет.
- [ ] Коммит `Клиент Google Calendar API`.

### Задача 6: `CalendarSync` — слияние

**Files:** Create `src/Chrono/Services/CalendarSync.cs`; Modify `tests/Chrono.Tests/Chrono.Tests.csproj`; Test `tests/Chrono.Tests/CalendarSyncTests.cs` (настоящие `AlarmService`/`AlarmStore`/`SyncStore` во временной папке + `GoogleCalendarClient` с фейковым обработчиком, имитирующим календарь в памяти).

**Consumes:** задачи 2, 3, 5. **Produces:**
```csharp
public sealed record SyncResult(int Pushed, int Pulled, int Deleted, bool FullReload);
public sealed class CalendarSync
{
    public CalendarSync(GoogleCalendarClient client, AlarmService alarms, SyncStore store, TimeProvider time);
    public Task<SyncResult> SyncAsync(CancellationToken ct); // одновременно не более одного выполнения (SemaphoreSlim)
}
```
Порядок одного прохода: 1) `CalendarId` (найти или создать, сохранить); 2) каждая запись без `GoogleEventId` и не `ForeignSeries` считается `Upsert`, даже если её нет в очереди (спецификация 6.5: при первом входе локальные записи отправляются); отправить очередь: `Upsert` — запись (кроме `ForeignSeries`) через `ToEvent` → `Insert`/`Update`, после вставки сохранить `GoogleEventId` (`Save(…, fromSync: true)`); `Delete` — `DeleteAsync`; элемент удаляется из очереди только после успеха; 3) `ListChangesAsync` с `SyncToken` (при `SyncTokenExpiredException` — заново без токена, `FullReload = true`); 4) каждое событие: пропустить `IgnoredEventIds`; найти существующую запись по `GoogleEventId` или `chronoId`; если в очереди есть `Upsert` этой записи и `event.updated <= ChangedAtUtc` — пропустить (локальная правка победит в следующий раз), иначе применить `FromEvent` (`ImportNeedsInstance` → `NextInstanceAsync` → второй вызов `FromEvent`); `ImportUpsert` → `Save(…, fromSync: true)` (если отклонено как прошедшее — пропустить), `ImportDelete` → `Delete(…, fromSync: true)`; 5) сохранить `SyncToken`, `LastSyncUtc`; 6) `RescheduleAll`. Сетевые ошибки и `SignInRequiredException` пробрасываются наверх после сохранения того, что уже сделано.

- [ ] **Тесты:** `Sync_FirstLogin_PushesLocalAndCreatesCalendar`; `Sync_PullsNewGoogleEventAsRingRecord`; `Sync_RemoteCancelled_DeletesLocal`; `Sync_LocalEditNewer_Wins`; `Sync_RemoteEditNewer_Wins`; `Sync_SecondDevice_MatchesByChronoIdWithoutDuplicates`; `Sync_SyncTokenExpired_FullReloadWithoutDuplicates`; `Sync_AutoDeletedOnceThenDelete_StaysInGoogleAndNotReimported`; `Sync_NetworkFailureMidPush_KeepsQueueAndAlarms`; `Sync_ForeignSeries_NotPushed_DeleteIgnoredForever`; `Sync_ConcurrentCalls_Serialized`.
- [ ] RED → GREEN; мутации: (а) убрать проверку `ChangedAtUtc` — `Sync_LocalEditNewer_Wins` краснеет; (б) искать запись только по `GoogleEventId` — `Sync_SecondDevice_…` краснеет.
- [ ] Коммит `Синхронизация записей с календарём «Chrono»`.

### Задача 7: вход через Google на Android

**Files:** Create `src/Chrono/Platforms/Android/GoogleAuth.cs`; Modify `src/Chrono/Platforms/Android/MainActivity.cs` (результат авторизации), `src/Chrono/Chrono.csproj` (`Xamarin.GooglePlayServices.Auth` 122.0.0 в группе Android).

**Consumes:** `IGoogleAuth`, `GoogleToken` (задача 5). Области — `calendar.app.created`, `openid`, `email`. Без offline-доступа (решение 9). `interactive = true` → если у результата есть разрешение (`HasResolution`), запуск его `PendingIntent` из текущей активности и ожидание `OnActivityResult` (через `TaskCompletionSource`); `false` → `null`, если нужен интерфейс. `SignOutAsync` — `AuthorizationClient.revokeAccess` если есть в привязке, иначе только очистка локального состояния (записать в отчёт).

- [ ] **Шаг 1: сверить имена API привязки** по сборке пакета (XML-документация или метаданные `Xamarin.GooglePlayServices.Auth.dll` в `~/.nuget/packages/xamarin.googleplayservices.auth/122.0.0`): `Identity.GetAuthorizationClient`, построитель `AuthorizationRequest`, `AuthorizeAsync`/`Authorize` + `Task`-обёртка, `AuthorizationResult.AccessToken`, `HasResolution`, `PendingIntent`, `GetAuthorizationResultFromIntent`, `RevokeAccess`. Записать в отчёт фактические имена до написания кода.
- [ ] **Шаг 2:** проверить, что подпись отладочной сборки — именно `debug.keystore` с SHA-1 из ограничений (`apksigner verify --print-certs` на apk).
- [ ] **Шаг 3:** реализация; сборка Android.
- [ ] **Шаг 4:** установить на эмулятор и убедиться, что приложение стартует без падения. Живой вход проверяется в задаче 10 (после «Настроек» из задачи 9).
- [ ] Коммит `Вход через Google на Android`.

### Задача 8: вход через Google на iOS (только сборка)

**Files:** Create `src/Chrono/Platforms/iOS/GoogleAuth.cs`; Modify `src/Chrono/Platforms/iOS/Info.plist` (схема перенаправления из задачи 1).

`WebAuthenticator` MAUI, PKCE (`S256`), iOS client id из журнала задачи 1, обмен кода на `https://oauth2.googleapis.com/token`; refresh token — `SecureStorage` ключ `google-refresh-token`; `GetTokenAsync(false)` — обмен refresh token, при ошибке `invalid_grant` — `null`.
- [ ] Реализация; `dotnet build -f net11.0-ios` — 0 ошибок; коммит `Вход через Google на iOS`.

### Задача 9: «Настройки» и запуск синхронизации

**Files:** Modify `src/Chrono/Views/SettingsPage.xaml`, `src/Chrono/ViewModels/SettingsViewModel.cs`, `src/Chrono/MauiProgram.cs` (DI: `SyncStore`, `IMemoryCache` (`AddMemoryCache`), `HttpClient` для `GoogleCalendarClient` (`AddHttpClient` не использовать — нет пакета; `new HttpClient()` singleton), `GoogleCalendarClient`, `CalendarSync`, `IGoogleAuth` по платформе, `AlarmService` с `SyncStore`), `src/Chrono/App.xaml.cs` (синхронизация при старте и `Window.Resumed`), `src/Chrono/Platforms/Android/BootReceiver.cs`, Create `src/Chrono/Platforms/Android/SyncWorker.cs`, `src/Chrono/Services/SyncLauncher.cs`, `src/Chrono/Chrono.csproj` (`Xamarin.AndroidX.Work.Runtime` 2.11.2.1), `Resources/Strings/AppResources*.resx`.

- `SyncLauncher` (MAUI): `RequestSoon()` — отложенный на 3 с запуск `CalendarSync.SyncAsync` (повторные вызовы в окне сливаются), `RunNowAsync()`; ничего не делает без входа (`SyncState.Email == null`); ошибки → состояние для «Настроек», не исключения. `AlarmService` после `Save/Toggle/Delete` без `fromSync` вызывает `SyncLauncher.RequestSoon()` — через событие `AlarmService.Changed` (задача 2), подписка в `MauiProgram`/`App`.
- `SyncWorker : Worker` — периодическая работа раз в 15 мин с требованием сети, уникальное имя `chrono-sync`, `ExistingPeriodicWorkPolicy.Keep`; ставится после входа, снимается при выходе. `DoWork` собирает зависимости вручную (как `BootReceiver`) и ждёт `SyncAsync` синхронно; `SignInRequiredException` → `Success` (не повторять).
- `BootReceiver` — дополнительно перепланирует `SyncWorker`, если вошли.
- «Настройки»: группа «Google Календарь» — строка «Аккаунт» (email) или accent-кнопка «Войти через Google»; строка состояния: «Синхронизировано в HH:mm» / «Нет сети» / «Нужно войти снова» (`GoldInk`); кнопки «Синхронизировать сейчас» и «Выйти» (выход: `SignOutAsync`, снять `SyncWorker`, очистить `Email`/`SyncToken`/`CalendarId`, записи остаются). Строки (en/ru): `GoogleGroup` Google Calendar / Google Календарь; `SignInGoogle` Sign in with Google / Войти через Google; `GoogleAccount` Account / Аккаунт; `SyncedAt` Synced at {0} / Синхронизировано в {0}; `SyncNoNetwork` No network / Нет сети; `SyncSignInAgain` Sign in again / Нужно войти снова; `SyncNow` Sync now / Синхронизировать сейчас; `SignOut` Sign out / Выйти.
- [ ] Реализация; сборки Android и iOS; `dotnet test`; коммит `Настройки Google и запуск синхронизации`.

### Задача 10: приёмка на эмуляторе (с пользователем)

- [ ] Установить сборку (`-p:EmbedAssembliesIntoApk=true`); пользователь входит в Google на эмуляторе (пауза до его подтверждения в чате).
- [ ] Сценарии (скриншот на каждый):
  1. Вход: создан календарь «Chrono» (видно в Google Календаре на компьютере пользователя — подтверждает пользователь или `calendarList` в логе); локальные записи появились в нём.
  2. Пользователь создаёт в календаре «Chrono» с компьютера событие через 5 мин с напоминанием за 2 мин → после «Синхронизировать сейчас» оно в ленте и во «Будильниках», звонит дважды.
  3. Запись, созданная в Chrono, — в Google с поясом телефона, без напоминаний Google.
  4. Правка в Chrono → в Google; правка в Google → в Chrono.
  5. Удаление в Chrono → исчезло в Google; «Один раз и удалить» после срабатывания — осталось в Google.
  6. Удалить приложение, установить заново, войти → всё расписание вернулось без дублей.
  7. Режим полёта: правка записи сохраняется, звонок звонит; после включения сети — изменение доходит до Google.
- [ ] **Уборка:** после подтверждения пользователя — удалить тестовые события из календаря «Chrono» (через приложение или API); сам календарь оставить, если пользователь не скажет иначе.
- [ ] Отчёт `task-10-report.md`, дефекты — без исправлений.

---

## Журнал исполнения

| Задача | Коммит | Проверено | Непроверено / выводы |
|---|---|---|---|
| — | `0ff8f94` | спецификация | базовая линия: 103 теста |
