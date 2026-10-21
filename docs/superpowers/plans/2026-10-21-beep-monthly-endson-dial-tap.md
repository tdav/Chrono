# Короткий сигнал, «каждый месяц», дата окончания, «Циферблат» в одно нажатие — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** новый уровень оповещения `Beep` (три пика ≤ 3 с через поток будильника), повтор `Monthly`, обязательная в редакторе дата окончания `EndsOn` для повторяющихся записей, переход в редактор с «Циферблата» одним нажатием; затем перенос 8 записей лекарств на новые возможности.

**Architecture:** модель (`Alarm`) получает два значения enum в конец, поле `EndsOn` и метод `Ended`; все планировщики (Android, iOS-план) и разбор Google опираются на них. Beep на Android едет путём Notify (`ScheduleReceiver`), но звук играет само приложение через `MediaPlayer` с `Usage=Alarm` под `GoAsync`; на iOS — как Notify с `beep.wav`. UI — существующие шторки и строки редактора, вторая `CalendarSheetModel` для окончания.

**Tech Stack:** .NET 11 (`net11.0-android;net11.0-ios`), MAUI, CommunityToolkit.Mvvm 8.4.2, xunit.v3 (`tests/Chrono.Tests`, файлы приложения подключены ссылками), Python 3 stdlib (`tools/gen_sounds.py`).

**Spec:** `docs/superpowers/specs/2026-10-21-beep-monthly-endson-dial-tap-design.md` — исполнители читают и его.

## Global Constraints

- Новые значения enum — только в конец: `AlertKind.Beep = 3`, `RepeatKind.Monthly = 5`. Существующие номера не меняются (`alarms.json` хранит числа).
- `EndsOn` — `DateOnly?`, последний день **включительно**; `null` = бессрочно (старые записи, серии Google без конца).
- Запись после `EndsOn` не удаляется и **не выключается** (`IsEnabled` не трогать) — просто нет срабатываний.
- Monthly: число месяца из `At`; месяц без такого числа пропускается.
- Beep звучит и в беззвучном режиме / «Не беспокоить» на Android (поток будильника). На iOS — подчиняется беззвучному (принято).
- Стиль C#: приватные поля без `_`, обращение через `this.`; комментарии и commit-сообщения на русском; без private-методов-помощников без особой нужды.
- Тексты (en / ru) — ровно из таблицы в задаче 1.
- Коммит только с точными путями файлов: `git commit -m "..." -- <файлы>`; `git add -A`, `git add .` и пути-папки запрещены.
- Тесты: `dotnet test tests/Chrono.Tests` (на `main` до начала: 305 зелёных). Android-сборка и установка на телефон пользователя (Redmi K70 Pro, adb по Wi-Fi, серийник смотреть `adb devices`, adb: `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe`):
  `dotnet build src/Chrono/Chrono.csproj -t:Install -f net11.0-android -p:EmbedAssembliesIntoApk=true "-p:AdbTarget=-s <serial>"`.
  Только один агент за раз ставит на телефон. После `am force-stop` приложение обязательно запустить (`monkey -p com.chrono.alarm -c android.intent.category.LAUNCHER 1`) — иначе сняты будильники пользователя.
- Окружение: БД нет, Docker не нужен, сервера нет. Данные приложения — `run-as com.chrono.alarm` → `files/alarms.json`, `files/done.json`, `files/sync.json`.
- Прогресс — в `docs/superpowers/plans/2026-10-21-beep-monthly-endson-dial-tap-progress.md` (создаёт задача 1, дописывает каждая): что сделано, хеш, выводы, меняющие следующие задачи.

## Review Focus

1. **Запись с `EndsOn` в прошлом, но включённая** — ни Android, ни iOS ничего не планируют; Google-эхо не перезаписывает её. Тесты: задача 2 (`Build_EndedSeries_NotPlanned`, `GetAll_EndedSeries_KeepsEnabled`), задача 3 (`FromEvent_UntilInPast_ImportedEnabledWithoutChange`).
2. **Monthly на 31-е число через ноябрь и февраль** — пропуск месяца, а не «последний день» и не бесконечный цикл. Тест: задача 1 (`NextAfter_Monthly31_SkipsShortMonths`).
3. **`EndsOn` попадает на день, когда время уже прошло** — сегодня последний день, время 07:00, сейчас 08:00: следующее срабатывание позже `EndsOn` → `Ended` истинно, ничего не звонит завтра. Тест: задача 1 (`Ended_NextAfterLastDay_True`).
4. **Beep, у которого запись удалили до срабатывания** (`alarm is null` в `ScheduleReceiver`) — пик всё равно звучит только если таймер был Beep (тип едет в extras), не Notify. Проверка: задача 4, ручной сценарий шага 6.
5. **UNTIL в часовом поясе со смещением** — `EndsOn = 9.11` в Ташкенте (+05) пишется как `UNTIL=20261109T185959Z` и читается обратно как 9.11, не 10.11. Тест: задача 3 (`ToEvent_EndsOn_UntilIsEndOfLocalDayInUtc`, `RoundTrip_MonthlyWithEndsOn_Equal`).

## Скиллы, агенты, модели, параллельность

| Задача | Модель | Зависит от | Параллельно с |
|---|---|---|---|
| 1 Модель + строки | sonnet | — | — |
| 2 AlarmService + iOS-план | sonnet | 1 | 3, 4, 5 |
| 3 Google-маппинг | sonnet | 1 | 2, 4, 5 |
| 4 Android Beep | sonnet | 1 | 2, 3, 5 |
| 5 iOS-планировщик | haiku | 1 | 2, 3, 4 |
| 6 Редактор | sonnet | 1 | 7 (разные файлы) |
| 7 «Циферблат» | sonnet | 1 | 6 |
| 8 Записи лекарств + приёмка на телефоне | sonnet | 2–7 | — |
| Финальное ревью ветки | opus | 1–8 | — |

Исполнение — `superpowers:subagent-driven-development`; ревью задач — `superpowers:requesting-code-review` (агент `feature-dev:code-reviewer`); финал — `superpowers:verification-before-completion`. Задачи 2–5 трогают разные файлы, но тесты 2 и 3 — разные файлы тестов; общий только прогон `dotnet test` (сборка в свой каталог `-o` при блокировке, MSB3026/3027). Установку на телефон делают по очереди: 4, затем 6/7, затем 8.

---

### Task 1: Модель — Beep, Monthly, EndsOn; все новые строки

**Files:**
- Modify: `src/Chrono/Models/Alarm.cs`
- Modify: `src/Chrono/Resources/Strings/AppResources.resx`, `src/Chrono/Resources/Strings/AppResources.ru.resx`
- Test: `tests/Chrono.Tests/AlarmModelTests.cs`
- Create: `docs/superpowers/plans/2026-10-21-beep-monthly-endson-dial-tap-progress.md`

**Interfaces:**
- Produces:
  - `AlertKind.Beep` (= 3), `RepeatKind.Monthly` (= 5).
  - `Alarm.EndsOn : DateOnly?` (init, по умолчанию null), XML-комментарий «последний день включительно; null — бессрочно».
  - `bool Alarm.Ended(DateTime occurrence)` — `!IsOneShot() && EndsOn is { } e && DateOnly.FromDateTime(occurrence) > e`.
  - `OccursOn(day)`: `false`, если `!IsOneShot() && EndsOn is { } e && day > e`; ветка `RepeatKind.Monthly => day.Day == at.Day`.
  - `NextAfter`: ветка Monthly — первое `new DateTime(year, month, At.Day) + время > moment`, месяцы без такого числа пропускаются (по образцу цикла Yearly; завершается за ≤ 12 шагов, т. к. ≥ 7 месяцев имеют 31 день). `NextAfter` по-прежнему **не смотрит** на `EndsOn` (решение: вызывающие проверяют `Ended`).
  - `IsOccurrence(at)` дополнительно `&& !this.Ended(at)`.
  - Строки (ключ: en / ru):

    | Ключ | en | ru |
    |---|---|---|
    | `Alert_Beep` | Short beep | Короткий сигнал |
    | `Repeat_Monthly` | Monthly | Каждый месяц |
    | `MonthlyOnDay` | Monthly, day {0} | Каждый месяц, {0}-го |
    | `RowEnds` | Ends | Окончание |
    | `EndsNotSet` | Not set | Не выбрано |
    | `EndsPickHint` | Pick the last day | Выберите последний день |
    | `EndsBeforeStart` | The end date is before the start | Дата окончания раньше начала |
    | `ChannelBeep` | Short beeps | Короткие сигналы |
    | `DialTapToEdit` | Tap to edit | Нажмите, чтобы изменить |

- [ ] **Step 1: Написать падающие тесты** в `AlarmModelTests.cs`:

```csharp
[Fact]
public void AlertKind_ExistingValues_KeepNumbers()
{
    Assert.Equal(0, (int)AlertKind.None);
    Assert.Equal(1, (int)AlertKind.Notify);
    Assert.Equal(2, (int)AlertKind.Ring);
    Assert.Equal(3, (int)AlertKind.Beep);
}
// в RepeatKind_ExistingValues_KeepNumbers добавить: Assert.Equal(5, (int)RepeatKind.Monthly);

[Fact]
public void NextAfter_Monthly_SameDayNextMonth()
{
    var a = new Alarm { At = new DateTime(2026, 10, 15, 8, 0, 0), Repeat = RepeatKind.Monthly, StartsOn = new DateOnly(2026, 10, 15) };
    Assert.Equal(new DateTime(2026, 11, 15, 8, 0, 0), a.NextAfter(new DateTime(2026, 10, 15, 9, 0, 0)));
}

[Fact]
public void NextAfter_Monthly31_SkipsShortMonths()
{
    var a = new Alarm { At = new DateTime(2026, 10, 31, 8, 0, 0), Repeat = RepeatKind.Monthly, StartsOn = new DateOnly(2026, 10, 31) };
    Assert.Equal(new DateTime(2026, 12, 31, 8, 0, 0), a.NextAfter(new DateTime(2026, 10, 31, 9, 0, 0)));
    Assert.Equal(new DateTime(2027, 1, 31, 8, 0, 0), a.NextAfter(new DateTime(2026, 12, 31, 9, 0, 0)));
    Assert.Equal(new DateTime(2027, 3, 31, 8, 0, 0), a.NextAfter(new DateTime(2027, 1, 31, 9, 0, 0)));
}

[Fact]
public void OccursOn_Monthly_OnlyThatDayOfMonth()
{
    var a = new Alarm { At = new DateTime(2026, 10, 15, 8, 0, 0), Repeat = RepeatKind.Monthly, StartsOn = new DateOnly(2026, 10, 15) };
    Assert.True(a.OccursOn(new DateOnly(2026, 12, 15)));
    Assert.False(a.OccursOn(new DateOnly(2026, 12, 16)));
}

[Fact]
public void OccursOn_EndsOn_InclusiveThenStops()
{
    var a = new Alarm { At = new DateTime(2026, 10, 21, 7, 30, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 21), EndsOn = new DateOnly(2026, 11, 9) };
    Assert.True(a.OccursOn(new DateOnly(2026, 11, 9)));
    Assert.False(a.OccursOn(new DateOnly(2026, 11, 10)));
}

[Fact]
public void Ended_NextAfterLastDay_True()
{
    var a = new Alarm { At = new DateTime(2026, 11, 9, 7, 0, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 21), EndsOn = new DateOnly(2026, 11, 9) };
    var next = a.NextAfter(new DateTime(2026, 11, 9, 8, 0, 0));
    Assert.Equal(new DateTime(2026, 11, 10, 7, 0, 0), next);
    Assert.True(a.Ended(next));
    Assert.False(a.Ended(new DateTime(2026, 11, 9, 7, 0, 0)));
}

[Fact]
public void Ended_OneShot_AlwaysFalse()
{
    var a = new Alarm { At = new DateTime(2026, 12, 1, 7, 0, 0), EndsOn = new DateOnly(2026, 11, 1) };
    Assert.False(a.Ended(a.At));
}

[Fact]
public void IsOccurrence_AfterEndsOn_False()
{
    var a = new Alarm { At = new DateTime(2026, 11, 10, 7, 0, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 21), EndsOn = new DateOnly(2026, 11, 9) };
    Assert.False(a.IsOccurrence(new DateTime(2026, 11, 10, 7, 0, 0)));
}
```

- [ ] **Step 2: Прогнать — убедиться, что падают.** `dotnet test tests/Chrono.Tests --filter "FullyQualifiedName~AlarmModelTests"` → ошибки компиляции (нет `Beep`, `Monthly`, `EndsOn`, `Ended`).
- [ ] **Step 3: Реализовать** интерфейсы из блока Produces в `Alarm.cs`; добавить строки в оба resx (в ru-файле — по образцу соседних `<data>` с `<value>` на отдельной строке).
- [ ] **Step 4: Прогнать весь набор.** `dotnet test tests/Chrono.Tests` → все зелёные (305 + новые). Мутация: временно убрать `&& !this.Ended(at)` из `IsOccurrence` → `IsOccurrence_AfterEndsOn_False` краснеет; вернуть, `git diff` по `Alarm.cs` — только задуманное.
- [ ] **Step 5: Собрать приложение** (строки генерируются при сборке): `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` → 0 ошибок. Новые switch с `_ =>` пока подписывают Beep/Monthly неверно — это чинят задачи 6 и 7.
- [ ] **Step 6: Создать файл прогресса** с таблицей задач 1–8 и отметкой задачи 1.
- [ ] **Step 7: Коммит.** `git add` по точным путям, `git commit -m "Модель: короткий сигнал, повтор «каждый месяц», дата окончания" -- src/Chrono/Models/Alarm.cs src/Chrono/Resources/Strings/AppResources.resx src/Chrono/Resources/Strings/AppResources.ru.resx tests/Chrono.Tests/AlarmModelTests.cs docs/superpowers/plans/2026-10-21-beep-monthly-endson-dial-tap-progress.md`

---

### Task 2: AlarmService и iOS-план учитывают EndsOn и Beep

**Files:**
- Modify: `src/Chrono/Services/AlarmService.cs` (`Save`, `:107-165`)
- Modify: `src/Chrono/Services/IosNotificationPlan.cs` (`Build`, `:26-57`)
- Test: `tests/Chrono.Tests/AlarmServiceTests.cs`, `tests/Chrono.Tests/IosNotificationPlanTests.cs`

**Interfaces:**
- Consumes: `Alarm.Ended(DateTime)`, `AlertKind.Beep`, `Alarm.EndsOn` (задача 1).
- Produces: `AlarmService.Save` сбрасывает `EndsOn` в null у разовых (`alarm.IsOneShot()`); `GetAll` не меняется (истёкшая серия остаётся включённой с `At = NextAfter(now)`). `IosNotificationPlan.Build`: в цикле по k — `if (alarm.Ended(at)) break;` до добавления моментов; Beep — одно уведомление (условие `Take` — `Alert is AlertKind.Notify or AlertKind.Beep`).

- [ ] **Step 1: Падающие тесты** (вспомогательные объекты — из `TestDoubles.cs`, по образцу соседних тестов файла):
  - `IosNotificationPlanTests.Build_Beep_SingleNotification` — Beep-запись → ровно 1 `PlannedNotification` на срабатывание.
  - `IosNotificationPlanTests.Build_EndedSeries_NotPlanned` — Daily, `At = 2026-11-10 07:00`, `EndsOn = 2026-11-09`, now `2026-11-09 08:00` → пусто.
  - `IosNotificationPlanTests.Build_DailyEndingSoon_StopsAtEndsOn` — Notify, Daily, `At = 2026-11-07 07:00`, `StartsOn = 2026-10-21`, now `2026-11-06 08:00`, `EndsOn = 2026-11-09` → ровно 3 уведомления (7, 8, 9 ноября 07:00).
  - `AlarmServiceTests.Save_OneShot_ClearsEndsOn` — сохранить разовую с `EndsOn` → в хранилище `EndsOn == null`.
  - `AlarmServiceTests.GetAll_EndedSeries_KeepsEnabled` — Daily, `EndsOn` вчера, `At` в прошлом → после `GetAll` `IsEnabled == true`, `At > now`, `Ended(At) == true`.
- [ ] **Step 2: Прогнать — падают** (`--filter "FullyQualifiedName~IosNotificationPlanTests|FullyQualifiedName~AlarmServiceTests"`).
- [ ] **Step 3: Реализовать** по блоку Produces.
- [ ] **Step 4: Весь набор зелёный.** Мутация: убрать `break` по `Ended` → `Build_DailyEndingSoon_StopsAtEndsOn` краснеет (16 вместо 3); вернуть.
- [ ] **Step 5: Коммит** точными путями: «Планирование: дата окончания и короткий сигнал в iOS-плане и сохранении».

---

### Task 3: Google — FREQ=MONTHLY и UNTIL туда и обратно

**Files:**
- Modify: `src/Chrono/Services/CalendarMapping.cs` (`ToEvent :36-112`, `FromEvent :154-218`)
- Test: `tests/Chrono.Tests/CalendarMappingTests.cs`, `tests/Chrono.Tests/CalendarSyncTests.cs`

**Interfaces:**
- Consumes: `RepeatKind.Monthly`, `Alarm.EndsOn` (задача 1).
- Produces:
  - `ToEvent`: Monthly → `RRULE:FREQ=MONTHLY`; startDay Monthly — первая дата ≥ startDay с числом `At.Day` (месяцы без него пропускаются), по образцу блока Yearly. При `EndsOn` у повторяющейся к RRULE дописывается `;UNTIL=<UTC>` где `<UTC>` = `EndsOn` 23:59:59 местного времени `zone` → UTC, формат `yyyyMMdd'T'HHmmss'Z'`.
  - `FromEvent`: принимает `UNTIL` — формы `yyyyMMdd`, `yyyyMMdd'T'HHmmss'Z'` (UTC → `zone`), `yyyyMMdd'T'HHmmss` (местное); `EndsOn` = местная дата. `BYMONTHDAY` принимается только при `FREQ=MONTHLY` и значении = число старта; `FREQ=MONTHLY` без `BYDAY` → Monthly. `COUNT`, `BYDAY` при MONTHLY, `INTERVAL≠1` — по-прежнему «чужая серия». Нечитаемый `UNTIL` — «чужая серия».
  - Истёкшая серия (`UNTIL` в прошлом) импортируется как обычная: `IsEnabled` по свойствам, `At = NextAfter(now)` (не Skip).
- [ ] **Step 1: Переделать сломанные примеры «чужой серии»** — решение пользователя «обновить». В `CalendarMappingTests.cs:444,447,471` и `CalendarSyncTests.cs:365,404,419,646,716,735,759,781` заменить `FREQ=MONTHLY` → `FREQ=MONTHLY;BYDAY=1MO`, а `UNTIL=...` → `COUNT=5` (смысл каждого теста — «правило, которое Chrono не умеет» — сохраняется; номера строк сверить поиском, после задачи 1 могли сместиться). Прогнать — эти тесты зелёные и до правки кода.
- [ ] **Step 2: Падающие тесты** в `CalendarMappingTests.cs` (`Tashkent`, `Now`, `Timed(...)` — существующие помощники файла):
  - `ToEvent_Monthly_FreqMonthlyAndStartOnDayOfMonth` — `At 2026-10-15 08:00`, `StartsOn 2026-10-20` → `RRULE:FREQ=MONTHLY`, старт `2026-11-15T08:00:00+05:00`.
  - `ToEvent_EndsOn_UntilIsEndOfLocalDayInUtc` — Daily, `EndsOn 2026-11-09`, Ташкент → `RRULE:FREQ=DAILY;UNTIL=20261109T185959Z`.
  - `FromEvent_Monthly_Supported` — `FREQ=MONTHLY` → `Repeat == Monthly`, `ForeignSeries == false`.
  - `FromEvent_UntilUtc_EndsOnLocalDate` — `FREQ=DAILY;UNTIL=20261109T185959Z` → `EndsOn == 2026-11-09`; и `UNTIL=20261109` → тоже `2026-11-09`.
  - `FromEvent_UntilInPast_ImportedEnabledWithoutChange` — `UNTIL` раньше `Now` → `ImportUpsert`, `IsEnabled == true`.
  - `RoundTrip_MonthlyWithEndsOn_Equal` и `RoundTrip_WeeklyWithEndsOn_Equal` — `FromEvent(ToEvent(a), …, existing: a)` → запись `== a` (record-равенство, как требует `CalendarSync.cs:165`; `At` исходной брать как `a.NextAfter(Now)`, `GoogleEventId` задать; у Monthly `StartsOn` — дата с тем же числом, что `At` (иначе ToEvent сдвинет старт и StartsOn не совпадёт — это не дефект)).
  - В `FromEvent_UnsupportedRule_NeedsInstance` добавить `[InlineData("RRULE:FREQ=MONTHLY;BYMONTHDAY=3")]` (старт 26-го ≠ 3) и `[InlineData("RRULE:FREQ=DAILY;UNTIL=garbage")]`.
- [ ] **Step 3: Прогнать — падают.**
- [ ] **Step 4: Реализовать** по блоку Produces; обновить комментарий о поддерживаемых правилах над разбором RRULE.
- [ ] **Step 5: Весь набор зелёный.** Мутация: заменить в ToEvent `23:59:59` на `00:00:00` → `ToEvent_EndsOn_...` и `RoundTrip_...` краснеют; вернуть.
- [ ] **Step 6: Коммит** точными путями: «Google: повтор «каждый месяц» и дата окончания (UNTIL)».

---

### Task 4: Android — короткий сигнал через поток будильника; ничего после EndsOn

**Files:**
- Modify: `tools/gen_sounds.py` (функция `beep`, файл `beep.wav`)
- Create: `src/Chrono/Resources/Raw/beep.wav` (результат генератора)
- Modify: `src/Chrono/Platforms/Android/AlarmScheduler.cs` (`:60-63`)
- Modify: `src/Chrono/Platforms/Android/ScheduleReceiver.cs`

**Interfaces:**
- Consumes: `AlertKind.Beep`, `Alarm.Ended`, строка `ChannelBeep` (задача 1).
- Produces:
  - `AlarmScheduler.Sync`: `upcoming = alarm.IsEnabled && alarm.At > now && !alarm.Ended(alarm.At)`; `notify = upcoming && alarm.Alert is AlertKind.Notify or AlertKind.Beep` (Beep едет теми же таймерами и ключами `notify_keys`).
  - `ScheduleReceiver.CreatePendingIntent(..., bool beep = false)` — новый extra `ExtraBeep` (bool); `AlarmScheduler` передаёт `alarm.Alert == AlertKind.Beep`.
  - `ScheduleReceiver.BeepChannelId = "beep_v1"` — канал без звука (`SetSound(null, null)`), `NotificationImportance.Default`, вибрация канала выключена (вибрирует приложение).
  - Условие показа: `alarm.Alert is AlertKind.Notify or AlertKind.Beep` вместо `== Notify`; при `alarm is null` тип берётся из `ExtraBeep`.
  - Beep: уведомление в канал `beep_v1` + звук: `var pending = this.GoAsync();` `MediaPlayer` с `AudioAttributes` `Usage=Alarm`, `ContentType=Sonification`, данные из assets `beep.wav` (как `SoundPlayer.cs:22-25`); `Completion` и `Error` → `Release()` + `pending.Finish()`; страховка `Handler(Looper.MainLooper).PostDelayed(... , 5000)` с тем же завершением (Finish ровно один раз). Вибрация при `VibrationEnabled`: `VibrationEffect.CreateWaveform([0, 120, 880, 120, 880, 120], -1)` с атрибутами Alarm (как `AlarmRingService.cs:131-146`). `RescheduleAll` в `finally` — как сейчас.
- [ ] **Step 1: Звук.** В `gen_sounds.py` функция `beep(t)`: три пика по 0,12 с с периодом 1,0 с, синус 1760 Гц с атакой 5 мс без щелчка; длительность файла 2,3 с (свой `DURATION` для beep). Частота/длина пика/период — константы `BEEP_FREQ`, `BEEP_ON`, `BEEP_PERIOD` с комментарием «калибровка на телефоне». Запуск `python tools/gen_sounds.py` перегенерирует все файлы — остальные должны остаться побайтно прежними: `git status` показывает только новый `beep.wav` (иначе генерировать только beep).
- [ ] **Step 2: Реализовать** Produces в `AlarmScheduler.cs` и `ScheduleReceiver.cs`. Комментарий в шапке `ScheduleReceiver` — про Beep и почему звук играет приложение (звук канала идёт по громкости уведомлений и молчит в беззвучном режиме).
- [ ] **Step 3: Сборка.** `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` → 0 ошибок; `dotnet test tests/Chrono.Tests` зелёный.
- [ ] **Step 4: Установка на телефон** (команда в Global Constraints) и запуск.
- [ ] **Step 5: Ручная проверка Beep** — без UI редактора (он в задаче 6): через adb добавить в `alarms.json` тестовую запись `Label "T4-beep"`, `Alert 3`, `Repeat 0`, `At` = сейчас + 2 мин, остальное по образцу соседних; force-stop → подменить файл → запустить приложение. Телефон в беззвучный режим (`adb shell cmd audio set-ringer-mode SILENT` или вручную — сообщить пользователю). Ожидаемо: три пика слышны, вибрация в такт, уведомление «T4-beep» в шторке, экрана звонка нет; `adb logcat` без исключений `Chrono`/`AndroidRuntime`. Вернуть режим звонка (`NORMAL`), удалить тестовую запись тем же способом и запустить приложение.
- [ ] **Step 6: Удалённая запись.** Повторить шаг 5, но удалить запись из файла за 1 минуту до срабатывания (force-stop, подмена, запуск): уведомление и пики всё равно приходят (`ExtraBeep`) — так ведёт себя и Notify (см. комментарий `ScheduleReceiver.cs:62-66`). Записать результат в прогресс.
- [ ] **Step 7: Коммит** точными путями: «Android: короткий сигнал через поток будильника, без срабатываний после даты окончания».

---

### Task 5: iOS-планировщик — Beep как Notify со своим звуком

**Files:**
- Modify: `src/Chrono/Platforms/iOS/AlarmScheduler.cs` (`:30-55`)
- Modify: `src/Chrono/Platforms/iOS/NotificationDelegate.cs` (только комментарий шапки)

**Interfaces:**
- Consumes: `AlertKind.Beep` (задача 1); `beep.wav` в `Resources/Raw` (задача 4 — в бандл попадает автоматически по `Chrono.csproj:46`).
- Produces: `isNotify = planned.Alarm.Alert is AlertKind.Notify or AlertKind.Beep` (Beep получает `open_day`, `Active2`, путь «баннер» в делегате); `content.Sound = Beep ? UNNotificationSound.GetSound("beep.wav") : Default` для этой ветки.
- [ ] **Step 1: Реализовать** Produces.
- [ ] **Step 2: Сборка.** Попробовать `dotnet build src/Chrono/Chrono.csproj -f net11.0-ios`. На Windows без Mac сборка iOS может быть невозможна — тогда записать в прогресс точный текст ошибки и считать проверкой `dotnet test` (план iOS покрыт задачей 2). Не обходить ошибку правкой csproj.
- [ ] **Step 3: Коммит** точными путями: «iOS: короткий сигнал — баннер со звуком beep.wav».

---

### Task 6: Редактор — Monthly, Beep, строка «Окончание», сохранение только с датой

**Files:**
- Modify: `src/Chrono/ViewModels/AlarmEditViewModel.cs`
- Modify: `src/Chrono/ViewModels/CalendarSheetModel.cs` (`Hint :60-62`)
- Modify: `src/Chrono/ViewModels/RepeatSheetModel.cs` (`:15-16`)
- Modify: `src/Chrono/ViewModels/AlarmListViewModel.cs` (`:24-37`)
- Modify: `src/Chrono/Views/AlarmEditPage.xaml` (строки `:44-73`, шторки `:151-192`)

**Interfaces:**
- Consumes: `RepeatKind.Monthly`, `AlertKind.Beep`, `Alarm.EndsOn`, строки `Repeat_Monthly`, `MonthlyOnDay`, `RowEnds`, `EndsNotSet`, `EndsPickHint`, `EndsBeforeStart` (задача 1).
- Produces:
  - `CalendarSheetModel.EmptyHint : string` (по умолчанию `AppResources.NearestHint`); `Hint` для `Selected == null` возвращает `EmptyHint`.
  - `RepeatSheetModel.Options` в явном порядке: None, Daily, Weekly, Monthly, Yearly, OnceThenDelete.
  - `AlarmEditViewModel`:
    - `[ObservableProperty] DateOnly? EndsOn` (+ `MarkDirty`, `NotifyPropertyChangedFor(EndsText, IsEndsMissing)`, `NotifyCanExecuteChangedFor(SaveCommand)`); `Repeat` тоже `NotifyCanExecuteChangedFor(SaveCommand)` и `NotifyPropertyChangedFor(IsEndsRowVisible, IsEndsMissing)`.
    - `bool IsEndsRowVisible => !this.IsOneShot`; `bool IsEndsMissing => this.IsEndsRowVisible && this.EndsOn is null`; `string EndsText` — `EndsNotSet` или дата `"dd.MM.yyyy"`.
    - `bool CanSave => this.IsOneShot || this.EndsOn is not null`; `SaveAsync` — `[RelayCommand(CanExecute = nameof(CanSave))]`.
    - `CalendarSheetModel EndsSheet` (второй экземпляр, `EmptyHint = EndsPickHint`), `[ObservableProperty] bool IsEndsSheetOpen`, `[RelayCommand] OpenEnds()` → `EndsSheet.Load(this.EndsOn, сегодня, allowPast: false)` и затем `EndsSheet.IsClearVisible = false`; `Done` при «Окей» → `EndsOn = EndsSheet.Selected ?? EndsOn`. Учесть в `IsAnySheetOpen`, `CloseSheet`, `partial OnIsEndsSheetOpenChanged`.
    - Загрузка: `EndsOn = existing.EndsOn`; Monthly, как Yearly, загружает `Date` из `At`, требует дату в `RepeatSheet.Done`/`CalendarSheet.Done` и видит строку даты (`IsDateRowVisible`), `allowPast` в `OpenDate` — как у Yearly; `DateText` у Monthly — `"dd.MM.yyyy"`.
    - `RepeatText`: ветка `RepeatKind.Monthly => Repeat_Monthly`.
    - `IsSoundRowsVisible` без изменений (только Ring).
    - `SaveAsync`: `EndsOn = this.IsOneShot ? null : this.EndsOn`; если `!IsOneShot && EndsOn < StartsOn записи` → `ErrorText = EndsBeforeStart`, не сохранять.
  - `AlarmListViewModel`: ветка `RepeatKind.Monthly => string.Format(culture, AppResources.MonthlyOnDay, alarm.At.Day)`.
  - XAML: строка «Окончание» после строки «Дата» в той же группе (`IsVisible="{Binding IsEndsRowVisible}"`, разделитель тоже), `Text="{Binding EndsText}"`, при `IsEndsMissing` цвет значения `{StaticResource GoldInk}` (DataTrigger); вторая `CalendarSheet` с `BindingContext="{Binding EndsSheet}"` и `IsEndsSheetOpen` — копия существующей шторки даты. Кнопка «Сохранить» при `CanExecute == false` должна выглядеть неактивной — проверить, есть ли у `PrimaryButton` состояние `Disabled` в стилях; если нет — добавить `VisualState Disabled` (Opacity 0.4) в стиль.
- [ ] **Step 1: Реализовать** Produces (ViewModels в тестовый проект не подключены — проверка на телефоне).
- [ ] **Step 2: Сборка** `-f net11.0-android` → 0 ошибок; `dotnet test` зелёный.
- [ ] **Step 3: Установка и приёмка на телефоне со скриншотами** в `.superpowers/sdd/2026-10-21-beep/` (каталог в git не добавлять):
  1. «+» → Повтор «Каждый день» → строка «Окончание: Не выбрано» золотом, «Сохранить» неактивна (нажатие ничего не делает).
  2. Окончание → шторка без «Очистить», подсказка «Выберите последний день», прошлые дни недоступны → выбрать дату → «Сохранить» активна → сохранить → запись в «Распорядке», после даты окончания (листать неделю) её нет.
  3. Повтор «Каждый месяц» есть между «По дням недели» и «Каждый год»; видна строка «Дата».
  4. Оповещение → «Короткий сигнал»; строк «Звук»/«Вибрация» нет.
  5. Повтор «Однократно» → строки «Окончание» нет, «Сохранить» активна.
  6. Открыть существующую запись без `EndsOn` (тестовая `T10-daily-6`) → «Сохранить» неактивна до выбора даты. **Не сохранять** её (это будильник пользователя на 22:32) — выйти «Назад» с отказом от правок.
  Удалить созданные проверкой записи через редактор.
- [ ] **Step 4: Коммит** точными путями: «Редактор: повтор «каждый месяц», короткий сигнал, обязательная дата окончания».

---

### Task 7: «Циферблат» — в редактор одним нажатием

**Files:**
- Modify: `src/Chrono/ViewModels/DialViewModel.cs`
- Modify: `src/Chrono/Views/DialPage.xaml` (`:88-133`)
- Modify: `src/Chrono/Resources/Strings/AppResources.resx`, `AppResources.ru.resx` (удаление строк)

**Interfaces:**
- Consumes: `AlertKind.Beep`, `Alert_Beep`, `DialTapToEdit` (задача 1).
- Produces:
  - `public sealed record DialIdleLine(Guid? Id, string Text);` в `DialViewModel.cs`.
  - `IdleHeadline : DialIdleLine` — Id = идущее дело, иначе ближайшее, иначе null («Свободно весь день»); `IdleLines : IReadOnlyList<DialIdleLine>` с Id дела.
  - `[RelayCommand] Task OpenEntryAsync(DialIdleLine? line)` → при `line?.Id` — `Shell.Current.GoToAsync($"edit?id={id}")`, иначе ничего.
  - Карточка выбранного: `TapGestureRecognizer Command="{Binding EditCommand}"` (существующая `EditAsync`).
  - `SelectAt`: промах (`HitTest` вернул null) при наличии выбора → `this.selected = null; this.Refresh();`.
  - Подпись оповещения в карточке: ветка `AlertKind.Beep => AppResources.Alert_Beep`, `AlertKind.Ring => Alert_Ring` (явно, без `_`).
  - Удалить: `IsExpanded`, `IsHintVisible`, `DoneText`, `DoneDescription`, команды `ToggleExpanded`, `ClearSelection`, `ToggleDone`; кнопки и подсказку в XAML; строки `DialTapForActions`, `DialClearSelection`, `DialEdit`, `DialDone`, `DialUndone` из обоих resx — предварительно `grep` по `src/` подтверждает, что больше не используются. `MarkDone`/`UnmarkDone` не трогать (они у «Распорядка»).
  - XAML: стрелка `icon_chevron.png` 16×16 справа в заголовке карточки; `SemanticProperties.Hint="{x:Static res:AppResources.DialTapToEdit}"` на заголовке (не на Border — см. комментарий `DialPage.xaml:111`); строки сводки — `TapGestureRecognizer` с командой через `RelativeSource AncestorType={x:Type vm:DialViewModel}` и `x:DataType`, как `SchedulePage.xaml:62`; `CommandParameter="{Binding .}"`.
- [ ] **Step 1: Реализовать** Produces.
- [ ] **Step 2: Сборка** `-f net11.0-android` → 0 ошибок, 0 предупреждений XAML-биндингов (XC0022/XC0025 — признак неверного `x:DataType`); `dotnet test` зелёный.
- [ ] **Step 3: Приёмка на телефоне со скриншотами** (`.superpowers/sdd/2026-10-21-beep/`):
  1. Без выбора: нажатие на верхнюю строку сводки → редактор того дела; «Назад» → нажатие на строку «ЧЧ:ММ …» → редактор этого дела.
  2. Нажатие на сектор → сектор выделен, карточка со стрелкой «›», кнопок нет; нажатие на карточку → сразу редактор этой записи, в нём есть «Удалить».
  3. Нажатие на пустое место циферблата → выделение снято, видна сводка.
  4. Переключение «Сейчас»/«Вечер» работает как раньше.
  Ничего не сохранять — выходить «Назад».
- [ ] **Step 4: Коммит** точными путями: «Циферблат: редактор одним нажатием, без кнопок карточки».

---

### Task 8: Записи лекарств на телефоне и общая приёмка

**Files:** (только данные на телефоне; в репозитории — запись в прогрессе)

**Interfaces:**
- Consumes: всё из задач 1–7, установленная сборка.

Целевые записи (все `Repeat 1`, `Alert 3`, `Icon "pill"`, `StartsOn 2026-10-21`, `DurationMinutes 0`, `ReminderMinutes ""`):

| Время | Подпись | EndsOn | Id |
|---|---|---|---|
| 07:00 | Эутирокс 50 мкг — 1 таб. натощак | 2036-10-21 | прежний |
| 07:30 | Хофитол 200 мг — 2 таб. | 2026-11-09 | прежний |
| 08:00 | Завтрак: Фестал 1 таб. | 2026-11-20 | прежний (бывш. «Завтрак: Фестал…, Дюфалак…») |
| 08:01 | Дюфалак 20 мл с водой | 2026-11-03 | новый |
| 12:30 | Хофитол 200 мг — 2 таб. | 2026-11-09 | прежний |
| 13:00 | Обед: Фестал 1 таб. | 2026-11-20 | прежний (бывш. «Обед: Фестал…, Урсосан…») |
| 13:01 | Урсосан форте 1 таб. | 2027-04-20 | новый |
| 18:30 | Хофитол 200 мг — 2 таб. | 2026-11-09 | прежний |
| 19:00 | Ужин: Фестал 1 таб. | 2026-11-20 | прежний |
| 22:30 | На ночь: Урсосан форте 1 таб. | 2027-04-20 | прежний |

- [ ] **Step 1: Снять копии** `alarms.json`, `sync.json`, `done.json` с телефона в scratchpad сессии (не в репозиторий).
- [ ] **Step 2: Собрать новый `alarms.json`**: прежние записи найти по подписи и времени; у них сохранить `Id` и `GoogleEventId`, поменять поля по таблице; две новые — новый `Id`, `GoogleEventId null`; `At` — ближайшее будущее срабатывание. Запись `T10-daily-6` не трогать.
- [ ] **Step 3: Поставить изменённые записи в очередь синхронизации**: в `sync.json` в `Pending` добавить по `Upsert` на каждый прежний `Id` с его `GoogleEventId` (формат — `SyncStore.cs`, `PendingChange`). Без этого правки не уйдут в Google. Новые записи очередь подхватит сама (`CalendarSync.cs:226`).
- [ ] **Step 4: Записать на телефон**: force-stop → подмена файлов через `run-as ... cp` → **запуск приложения**. Проверить, что `alarms.corrupt-*.json` не появился и файл пересохранён по порядку `At`.
- [ ] **Step 5: Приёмка**: скриншоты «Распорядка» (сегодня; 4.11 — нет Дюфалака; 10.11 — нет Хофитола; 21.11 — нет Фестала) и «Циферблата»; `adb shell dumpsys alarm | grep -c com.chrono.alarm` > 0. Синхронизация: через минуту в `sync.json` пустой `Pending`, ошибок в настройках синхронизации нет (если вход в Google не выполнен — записать и не настаивать).
- [ ] **Step 6: Прогресс** — итоговая запись с хешами задач.

---

### Финал

- [ ] Полный `dotnet test tests/Chrono.Tests` и `dotnet build -f net11.0-android` на итоговом `main`.
- [ ] Финальное ревью всей ветки (opus, `superpowers:requesting-code-review`): дифф `git diff a861ec6..HEAD` файлом; назвать риски из Review Focus и анти-цели спецификации; разрешить заглядывать в соседние файлы (`CalendarSync.cs`, `SmartWakeService.cs`, `AlarmReceiver.cs`); предупредить, что конец хунка ≠ конец файла.
- [ ] Обновить память проекта (`chrono-projects-status.md`).
