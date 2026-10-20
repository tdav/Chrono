# Распорядок дня и нижнее меню — план реализации

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development (рекомендуется) или superpowers:executing-plans. Шаги — чекбоксы (`- [ ]`).

**Цель:** запись Chrono получает длительность, иконку, уровень оповещения и дату начала; появляется вкладка «Распорядок» с лентой дня и нижнее меню из четырёх вкладок.

**Архитектура:** расширяется существующая `Alarm` (одна запись — два вида). Чистая логика (модель, `AlarmService`, `DayPlan`, `IosNotificationPlan`) — без MAUI, покрыта xUnit. Платформенные планировщики различают уровни `Ring` (как сейчас) и `Notify` (обычное уведомление по точному таймеру). UI — `Shell` с `TabBar`.

**Стек:** .NET MAUI 11 (`11.0.0-rc.1.26451.6`), `net11.0-android;net11.0-ios`, CommunityToolkit.Mvvm, xUnit v3 (`tests/Chrono.Tests`, net11.0, Microsoft.Testing.Platform).

**Спецификация:** [docs/superpowers/specs/2026-10-20-daily-schedule-design.md](../specs/2026-10-20-daily-schedule-design.md). Исполнитель читает её целиком до начала задачи.

## Глобальные ограничения

- Палитра без красного и зелёного; только токены `Resources/Styles/Colors.xaml` и стили `Styles.xaml` (`DESIGN.md` §2–3).
- Иконки — контурные SVG 24×24, линия 2–2.4, скругления, цвет `#2E3136`; эмодзи в XAML и коде нет. Образец стиля — `Resources/Images/icon_gear.svg`.
- Неделя с понедельника; Сб и Вс — `GoldInk`.
- Кнопки в шторках: «Отмена» слева, primary «Окей» крайняя справа; правки в шторке — черновик до «Окей».
- Приватные поля без `_`, обращение через `this.`; private-методы-помощники — только при реальной необходимости (глобальное правило пользователя).
- Комментарии в коде и сообщения коммитов — на русском, коммит заканчивается строкой `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `AlarmService.Save` по-прежнему отклоняет `At <= now` — это правило не ослаблять.
- Формы с `Entry` — по `~/.claude/references/fix-user-experience.md` (прочитать до правки формы).
- Все новые строки интерфейса — в `AppResources.resx` и `AppResources.ru.resx`; их добавляет **только задача 2** (см. её таблицу), остальные задачи ссылаются на ключи.

## Окружение исполнителя

- Windows 11, Git Bash и PowerShell, .NET SDK `11.0.100-rc.1`, workloads `android`, `ios`. Docker и БД не нужны.
- Тесты: `dotnet test tests/Chrono.Tests` (≈3 с, базовая линия — 57 зелёных).
- Сборка приложения: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android -o <свой каталог вне репозитория>` (≈1 мин; базовая линия — 0 предупреждений, 0 ошибок). Путь `-o` давать в форме `$TEMP/chrono-<задача>` из Git Bash или `C:\...` из PowerShell и после сборки проверить `git status`: в Git Bash Windows-путь с обратными слешами превращается в каталог в корне репозитория.
- iOS: только `dotnet build src/Chrono/Chrono.csproj -f net11.0-ios` (компиляция; Mac нет).
- Эмулятор Android нужен только задаче 10.

## Порядок и параллельность

Задачи одной волны идут параллельно, **каждая в своём git worktree** (Agent `isolation: "worktree"`) от вершины `feature/daily-schedule`; после волны ветки сливаются в `feature/daily-schedule`, затем — полный `dotnet test` и сборка Android. Общий `obj/` не даёт собирать один проект параллельно в одном дереве, поэтому worktree обязателен.

| Волна | Задачи | Модель исполнителя |
|---|---|---|
| 1 | 1 — модель и `AlarmService`; 2 — оболочка, вкладки, строки, иконки | sonnet, sonnet |
| 2 | 3 — `DayPlan`; 4 — Android; 5 — iOS; 6 — вкладка «Будильники» и сигнал | sonnet ×4 |
| 3 | 7 — экран «Распорядок»; 8 — шторки записи; затем 9 — экран записи | sonnet ×2, затем sonnet |
| 4 | 10 — приёмка на эмуляторе и `DESIGN.md` | sonnet |

Ревью: задачи 1 и 4 — `opus` (надёжность срабатывания), остальные — `sonnet`; финальное ревью ветки — `opus`. Навыки: исполнителю — `superpowers:test-driven-development` (задачи 1, 3, 5), `dotnet-maui:maui-shell-navigation` (задачи 2, 7), ревьюеру — `superpowers:requesting-code-review`.

Правила для агентов в общем дереве (на случай работы без worktree): `git add` только по точным путям файлов, не папок; `git commit -m "..." -- <пути>`; убивать только свой PID.

## Решения, принятые при планировании

Ревьюеры не видят промптов исполнителей — все решения здесь.

1. **Новое значение `RepeatKind.OnceThenDelete` добавляется последним.** `System.Text.Json` по умолчанию пишет enum числом: вставка в середину перенумерует повторы в уже сохранённых файлах пользователей.
2. **Новые вычисляемые члены `Alarm` — методы, не свойства.** STJ сериализует get-only свойства; метод в JSON не попадает.
3. **Отметки «сделано» хранятся отдельным файлом `done.json`, а не полем `Alarm`.** Массив в записи ломает равенство record (`Equals` сравнивает ссылку массива) — существующие тесты вида `Assert.Equal([alarm], store.Load().Alarms)` молча покраснели бы, а ещё хуже — перестали бы ловить реальные расхождения.
4. **«Разовость» — метод `Alarm.IsOneShot()`** (`None` или `OnceThenDelete`). Все проверки `Repeat == RepeatKind.None` в смысле «разовый» заменяются им; иначе `OnceThenDelete` молча поведёт себя как ежедневный (так работает `NextAfter` для всего, кроме `Yearly`). Места на момент планирования: `AlarmService.cs:40,109`, `IosNotificationPlan.cs:28`, `SmartWakeService.cs:170`, `AlarmEditViewModel.cs:168,205,235,363`.
5. **`Alarm.RingTimeout = 10 мин`** — единый источник; `AlarmRingService.RingTimeout` ссылается на него.
6. **Нижнее меню — нативный `TabBar` Shell** с цветами `Shell.TabBarBackgroundColor=Surface`, `TabBarTitleColor`/`TabBarForegroundColor=Text`, `TabBarUnselectedColor=TextMuted`. Металлическая капсула под выбранной иконкой из макета нативным `TabBar` не рисуется — отступление от макета `tabs.html`, сообщить пользователю.
7. **Маршруты вкладок:** `alarms`, `schedule`, `cycle`, `settings`. Зарегистрированный маршрут `settings` в `AppShell` удаляется (иначе два элемента с одним именем маршрута).
8. **Экраны поверх вкладок** (`AlarmEditPage`, `RingPage`, `ReliabilityPage`) — `Shell.TabBarIsVisible="False"`.
9. **Вкладка «Распорядок» не открывается раньше «Будильников»** при холодном старте: отложенные переходы (`RingLauncher`, `SettingsLauncher`, новый `ScheduleLauncher`) по-прежнему выполняет `AlarmListViewModel.AppearingAsync`.
10. **Приглушённая (серая) строка ленты** — только у выключенной записи, чьё срабатывание в этот день ещё впереди (`!IsEnabled && Start > now`). Разовая запись, которая уже сработала и поэтому выключена, в своём дне не серая.
11. **Дело через полночь** (23:30, 60 мин) показывается только в дне начала.

## Review Focus

1. Пользователь обновил приложение со старым `alarms.json` без новых полей → все будильники остаются `Ring`, звонят как раньше, получают `StartsOn`. Тест — задача 1.
2. Смена уровня записи `Ring → Notify` → на Android снимается `SetAlarmClock` (иначе запись продолжит звонить полноэкранно). Проверка — задача 4 (ручная) и задача 10.
3. `OnceThenDelete` после срабатывания не переходит на «завтра» → тесты `GetAll` (задача 1) и `IosNotificationPlan` (задача 5).
4. «Каждый день», созданный на день через неделю, не срабатывает раньше → тесты `NextAfter` и `Save` (задача 1).
5. Нажатие на уведомление `Notify` открывает «Распорядок» на нужном дне, даже если приложение было выгружено → ручная проверка задачи 4 и сценарий задачи 10.

---

### Задача 1: модель записи и `AlarmService`

**Files:**
- Modify: `src/Chrono/Models/Alarm.cs`
- Modify: `src/Chrono/Services/AlarmService.cs`
- Modify: `src/Chrono/Services/AlarmStore.cs`
- Modify: `src/Chrono/Platforms/Android/SmartWakeService.cs:170` (только `IsOneShot()`)
- Test: `tests/Chrono.Tests/AlarmModelTests.cs` (новый), `tests/Chrono.Tests/AlarmServiceTests.cs`, `tests/Chrono.Tests/AlarmStoreTests.cs`

**Interfaces — Produces:**
```csharp
public enum RepeatKind { None, Daily, Weekly, Yearly, OnceThenDelete }   // новое — последним
public enum AlertKind { None, Notify, Ring }

// Alarm — новые init-свойства:
public int DurationMinutes { get; init; }               // 0 — без длительности
public string Icon { get; init; } = "bell";             // ключ из ItemIcons.Keys (задача 2)
public AlertKind Alert { get; init; } = AlertKind.Ring; // старые файлы без поля → Ring
public DateOnly? StartsOn { get; init; }                // null только у непромигрированных
public static readonly TimeSpan RingTimeout = TimeSpan.FromMinutes(10);
public bool IsOneShot();                                 // None или OnceThenDelete
public DateTime ExpiresAt();                             // At + max(Duration, Alert==Ring ? RingTimeout : 0)
public bool OccursOn(DateOnly day);
public DateTime NextAfter(DateTime moment);              // теперь не раньше StartsOn 00:00

// AlarmStore:
public Dictionary<Guid, HashSet<DateOnly>> LoadDone();   // done.json; нет файла или JsonException → пустой
public void SaveDone(IReadOnlyDictionary<Guid, HashSet<DateOnly>> done);

// AlarmService:
public IReadOnlyDictionary<Guid, HashSet<DateOnly>> GetDone();
public void SetDone(Guid id, DateOnly day, bool done);   // без вызова планировщика
```

Правила `OccursOn(day)`: `false`, если `StartsOn is {} s && day < s`; разовый — `day == DateOnly.FromDateTime(At)`; `Daily` — `true`; `Weekly` — `Days == None` или день недели в `Days`; `Yearly` — совпадают месяц и число `At` (29 февраля — только в високосные).

Правила `GetAll()` по порядку для каждой записи:
1. `StartsOn is null` → `StartsOn = DateOnly.FromDateTime(At)` (миграция), запись изменена.
2. `Repeat == OnceThenDelete && ExpiresAt() <= now` → запись удаляется (независимо от `IsEnabled`).
3. `IsEnabled && At <= now`: `None` → выключить; `OnceThenDelete` → без изменений; повторяющаяся → `At = NextAfter(now)`.

`Save`: перед проверкой времени `StartsOn ??= DateOnly.FromDateTime(At)`. `Toggle`: `Repeat == RepeatKind.None` → `IsOneShot()`.

- [ ] **Шаг 1: тесты модели** — `AlarmModelTests.cs`:

```csharp
[Fact] public void NextAfter_DailyWithFutureStart_FirstFiresOnStartDay()
{
    var a = new Alarm { At = new DateTime(2026, 10, 20, 7, 15, 0), Repeat = RepeatKind.Daily, StartsOn = new DateOnly(2026, 10, 26) };
    Assert.Equal(new DateTime(2026, 10, 26, 7, 15, 0), a.NextAfter(new DateTime(2026, 10, 20, 12, 0, 0)));
}
[Fact] public void NextAfter_StartInPast_IsIgnored()      // StartsOn 2026-10-01, moment 2026-10-20 12:00 → 2026-10-21 07:15
[Fact] public void OccursOn_DailyBeforeStart_False()      // StartsOn 26.10 → OccursOn(25.10) false, OccursOn(26.10) true
[Fact] public void OccursOn_WeeklyMonWed_OnlyThoseDays()  // Days = Monday|Wednesday → пн 26.10 true, вт 27.10 false
[Fact] public void OccursOn_WeeklyNoDays_EveryDay()
[Fact] public void OccursOn_YearlyFeb29_NonLeapYearFalse() // At 2028-02-29 → OccursOn(2027-02-28) false, (2028-02-29) true
[Fact] public void OccursOn_OneShot_OnlyAtDate()           // и для None, и для OnceThenDelete
[Fact] public void IsOneShot_NoneAndOnceThenDelete_True()  // Daily/Weekly/Yearly → false
[Fact] public void ExpiresAt_NotifyWithDuration_IsEnd()    // 07:15 + 30 → 07:45
[Fact] public void ExpiresAt_RingShorterThanTimeout_UsesTimeout() // Ring, 0 мин → At + 10 мин; Ring, 45 мин → At + 45 мин
[Fact] public void RepeatKind_ExistingValues_KeepNumbers()  // (int)None==0, Daily==1, Weekly==2, Yearly==3, OnceThenDelete==4
```

- [ ] **Шаг 2: тесты `AlarmService`** — дописать в `AlarmServiceTests.cs` (часы `FixedTime`, `Now = 2026-10-06 12:00`):

```csharp
[Fact] public void GetAll_LegacyJsonWithoutNewFields_IsRingWithStartDate()
// записать в alarms.json руками: [{"Id":"…","At":"2026-10-07T07:00:00","Label":"","SoundEnabled":true,
//   "SoundId":"<Sounds.Default>","VibrationEnabled":true,"IsEnabled":true,"Repeat":1,"Days":0}]
// → Alert == Ring, DurationMinutes == 0, Icon == "bell", StartsOn == 2026-10-07, Repeat == Daily
[Fact] public void GetAll_FiredOnceThenDelete_BeforeExpiry_StaysEnabledAndUnmoved()
// Notify, At = Now-10 мин, 30 мин → после GetAll: та же At, IsEnabled == true
[Fact] public void GetAll_OnceThenDeleteAfterExpiry_IsRemoved()   // Notify, At = Now-31 мин, 30 мин → нет в списке и в файле
[Fact] public void GetAll_RingOnceThenDelete_KeptForRingTimeout() // Ring, 0 мин, At = Now-9 мин → есть; Now-10 мин → нет
[Fact] public void Save_DailyWithFutureStart_AtIsStartDay()      // Save(Daily, StartsOn = Now+6 дн) → сохранено, At.Date == StartsOn
[Fact] public void Save_WithoutStartsOn_SetsDateOfAt()
[Fact] public void Toggle_EnableExpiredOnceThenDelete_Rejected() // как разовый: false
[Fact] public void SetDone_PersistsAndDoesNotCallScheduler()     // SetDone(id, 06.10, true) → GetDone()[id] содержит 06.10; scheduler.Synced пуст; SetDone(…, false) убирает
```

И в `AlarmStoreTests.cs`: `LoadDone_CorruptFile_ReturnsEmpty`, `SaveDone_RoundTrip`.

- [ ] **Шаг 3:** `dotnet test tests/Chrono.Tests` — новые тесты красные (не компилируются или падают), старые 57 зелёные по смыслу.
- [ ] **Шаг 4:** реализовать по Interfaces и правилам выше. `NextAfter`: если `StartsOn` позже `moment`, считать от `StartsOn.ToDateTime(TimeOnly.MinValue).AddTicks(-1)`. `ExpiresAt` и прочие — методы (решение 2). Заменить проверки «разовый» по решению 4 в файлах этой задачи (`AlarmService`, `SmartWakeService`).
- [ ] **Шаг 5:** `dotnet test tests/Chrono.Tests` — все зелёные.
- [ ] **Шаг 6: мутации.** По очереди: (а) переставить `OnceThenDelete` перед `Daily` в enum; (б) убрать шаг 1 миграции в `GetAll`; (в) убрать учёт `StartsOn` в `NextAfter`. После каждой — нужный тест краснеет; вернуть, `git diff` по этому месту пуст. Перечислить в отчёте, какой тест поймал какую мутацию.
- [ ] **Шаг 7:** сборка Android (см. «Окружение») — 0 ошибок.
- [ ] **Шаг 8: коммит** `Модель записи: длительность, иконка, уровень оповещения, дата начала, «Один раз и удалить»` с точными путями.

### Задача 2: оболочка, вкладки, строки и иконки

**Files:**
- Modify: `src/Chrono/AppShell.xaml`, `src/Chrono/AppShell.xaml.cs`
- Modify: `src/Chrono/Views/AlarmListPage.xaml` (убрать шестерёнку), `Views/SettingsPage.xaml` + `ViewModels/SettingsViewModel.cs` (убрать «назад» и `CloseCommand`)
- Modify: `Views/AlarmEditPage.xaml`, `Views/RingPage.xaml`, `Views/ReliabilityPage.xaml` — `Shell.TabBarIsVisible="False"`
- Modify: `src/Chrono/Services/SettingsLauncher.cs` (маршрут `//settings`)
- Create: `src/Chrono/Services/ScheduleLauncher.cs`, `src/Chrono/Models/ItemIcons.cs`
- Create: `src/Chrono/Views/CyclePage.xaml(.cs)`, `src/Chrono/Views/SchedulePage.xaml(.cs)` — **заглушка**, задача 7 заменит
- Create: `Resources/Images/icon_tab_alarms.svg`, `icon_tab_schedule.svg`, `icon_tab_cycle.svg`, 12 × `icon_item_<key>.svg`
- Modify: `Resources/Strings/AppResources.resx`, `AppResources.ru.resx`, `src/Chrono/MauiProgram.cs`, `ViewModels/AlarmListViewModel.cs` (вызов `ScheduleLauncher.ShowPendingAsync()`)

**Interfaces — Produces:**
```csharp
public static class ItemIcons            // Models/ItemIcons.cs, без MAUI
{
    public static readonly IReadOnlyList<string> Keys =
        ["bell", "yoga", "shower", "coffee", "food", "bike", "work", "book", "pill", "phone", "walk", "sleep"];
    public static string ImageFor(string key);   // $"icon_item_{key}.png"; неизвестный ключ → "icon_item_bell.png"
}
public static class ScheduleLauncher     // Services/ScheduleLauncher.cs, по образцу RingLauncher
{
    public static DateOnly? PendingDay { get; }
    public static void Request(DateOnly day);
    public static Task ShowPendingAsync();       // Shell.Current.GoToAsync($"//schedule?day={day:yyyy-MM-dd}")
}
```
Маршруты вкладок — решение 7; порядок и подписи: `Tab_Alarms`, `Tab_Schedule`, `Tab_Cycle`, `SettingsTitle`. Иконки вкладок: `icon_tab_alarms.png`, `icon_tab_schedule.png`, `icon_tab_cycle.png`, `icon_gear.png`. Цвета TabBar — решение 6. `CyclePage`: заголовок `Tab_Cycle` стилем `ScreenTitle`, строка `CycleSoon` стилем `Muted`. `SchedulePage`-заглушка: заголовок `Tab_Schedule`.

**Строки** (ключ — en — ru), добавить все сразу:

| Ключ | en | ru |
|---|---|---|
| Tab_Alarms | Alarms | Будильники |
| Tab_Schedule | Schedule | Распорядок |
| Tab_Cycle | Cycle | Цикл |
| CycleSoon | Coming soon | Скоро |
| Repeat_OnceThenDelete | Once, then delete | Один раз и удалить |
| RowIcon | Icon | Иконка |
| RowDuration | Duration | Длительность |
| RowAlert | Alert | Оповещение |
| Alert_None | No notification | Без уведомления |
| Alert_Notify | Notification | Уведомление |
| Alert_Ring | Ring like an alarm | Звонить как будильник |
| DurationNone | None | Нет |
| DurationMinutes | {0} min | {0} мин |
| DurationCustom | Custom, min | Своё, мин |
| ScheduleEmpty | Nothing planned for this day | На этот день ничего нет |
| ScheduleLeft | {0} min left | осталось {0} мин |
| ChannelSchedule | Schedule | Распорядок |
| MarkDone | Mark as done | Отметить выполненным |
| Icon_bell … Icon_sleep | Alarm, Yoga, Shower, Coffee, Food, Bike, Work, Book, Medicine, Phone, Walk, Sleep | Будильник, Йога, Душ, Кофе, Еда, Велосипед, Работа, Книга, Лекарство, Телефон, Прогулка, Сон |

- [ ] **Шаг 1:** строки, `ItemIcons`, SVG (стиль — «Глобальные ограничения»; 15 файлов).
- [ ] **Шаг 2:** `AppShell` → `TabBar` с четырьмя `ShellContent`; удалить `Routing.RegisterRoute("settings", …)`; зарегистрировать `CyclePage`, `SchedulePage` в DI; `ScheduleLauncher`; правки страниц из списка Files.
- [ ] **Шаг 3:** сборка Android и iOS — 0 ошибок; `dotnet test` — зелёный.
- [ ] **Шаг 4: проверка смысла на эмуляторе, если он запущен** (иначе — отметить в отчёте как непроверенное, это закроет задача 10): четыре вкладки, приложение стартует на «Будильниках», «+» открывает редактор без нижней панели, «Назад» возвращает на вкладку.
- [ ] **Шаг 5: коммит** `Нижнее меню: четыре вкладки, строки и иконки распорядка`.

### Задача 3: `DayPlan` — записи дня и неделя

**Files:** Create `src/Chrono/Services/DayPlan.cs`; Test `tests/Chrono.Tests/DayPlanTests.cs`; Modify `tests/Chrono.Tests/Chrono.Tests.csproj` (ссылка на `DayPlan.cs` по образцу остальных `Compile Include`).

**Interfaces — Consumes:** `Alarm.OccursOn`, `AlarmService.GetDone` (задача 1). **Produces:**
```csharp
public readonly record struct DayEntry(Alarm Alarm, DateTime Start, DateTime End, bool IsCurrent, bool IsDone, bool IsMuted);
public static class DayPlan
{
    // Записи, у которых OccursOn(day); Start = day + время суток At; End = Start + DurationMinutes;
    // IsCurrent = DurationMinutes > 0 && Start <= now && now < End; IsDone = done[id] содержит day;
    // IsMuted — решение 10. Порядок: Start, затем Label.
    public static IReadOnlyList<DayEntry> For(IEnumerable<Alarm> alarms, IReadOnlyDictionary<Guid, HashSet<DateOnly>> done, DateOnly day, DateTime now);
    public static IReadOnlyList<DateOnly> Week(DateOnly day);   // пн…вс недели, содержащей day
}
```

- [ ] **Шаг 1: тесты** (`now = 2026-10-20 07:30`, вторник):
  - `For_DailyYoga_IsCurrentWithEnd` — 07:15, 30 мин → `End == 07:45`, `IsCurrent`.
  - `For_ZeroDuration_NeverCurrent` — 07:30, 0 мин, now 07:30 → `IsCurrent == false`.
  - `For_SortedByStartThenLabel` — 08:00 «Б», 07:00, 08:00 «А» → 07:00, «А», «Б».
  - `For_DoneOnlyForThatDay` — done {20.10} → `IsDone` для 20.10, не для 21.10.
  - `For_DisabledFutureOccurrence_Muted_PastOneNot` — выключенная ежедневная 09:00 → `IsMuted`; выключенная разовая 07:00 сегодня → не `IsMuted`.
  - `For_AcrossMidnight_OnlyStartDay` — 23:30, 60 мин: есть 20.10, нет 21.10; при `now = 21.10 00:15` в списке дня 20.10 `IsCurrent == true` (решение 11).
  - `For_BeforeStartsOn_Excluded`.
  - `Week_Sunday_ReturnsPrecedingMonday` — 2026-10-25 (вс) → 19.10 … 25.10.
- [ ] **Шаг 2:** красные → реализовать → зелёные; мутация: заменить `now < End` на `now <= End` — краснеет хотя бы один тест? Если нет — добавить тест на границу `now == End` (`IsCurrent == false`). Вернуть, `git diff` пуст.
- [ ] **Шаг 3: коммит** `DayPlan: записи дня и неделя`.

### Задача 4: Android — уведомление `Notify` и планировщик по уровням

**Files:**
- Create: `src/Chrono/Platforms/Android/ScheduleReceiver.cs`
- Modify: `Platforms/Android/AlarmScheduler.cs`, `Platforms/Android/MainActivity.cs`, `Platforms/Android/AlarmRingService.cs` (`RingTimeout` → `Alarm.RingTimeout`)

**Interfaces — Consumes:** `AlertKind`, `Alarm.DurationMinutes` (задача 1), `ScheduleLauncher.Request` (задача 2). **Produces:**
```csharp
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ScheduleReceiver : BroadcastReceiver
{
    public const string ChannelId = "schedule";
    public const string ExtraOpenDay = "open_day";   // "yyyy-MM-dd", InvariantCulture
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId); // Data = chrono://notify/{id}
}
```
- `AlarmScheduler.Sync`: для каждой записи `ring = IsEnabled && At > now && Alert == Ring`, `notify = … && Alert == Notify`. `ring` → нынешний `SetAlarmClock` (иначе `Cancel`); `notify` → `SetExactAndAllowWhileIdle(RtcWakeup, …, ScheduleReceiver PI)` (иначе `Cancel`); окно умного пробуждения — только при `ring`. `Cancel(id)` снимает и `ScheduleReceiver` PI.
- `ScheduleReceiver.OnReceive`: читает запись из `AlarmStore`; канал `ChannelId` с `NotificationImportance.Default` и именем `ChannelSchedule` (системный звук и вибрация канала); заголовок — `Label` (пусто → `DefaultLabel`), текст `HH:mm–HH:mm` или `HH:mm` при длительности 0 (по `At`); иконка `Resource.Drawable.ic_stat_alarm`; `SetAutoCancel(true)`; id уведомления — `alarmId.GetHashCode()`; content intent → `MainActivity` с `ExtraOpenDay`, **requestCode = `alarmId.GetHashCode()`** (extras не входят в ключ PendingIntent — с общим requestCode все уведомления открывали бы один день). Затем `new AlarmService(…).RescheduleAll()` — как в `AlarmRingService`, чтобы повторяющаяся запись встала на следующее срабатывание.
- `MainActivity.OnCreate`/`OnNewIntent`: есть `ExtraOpenDay` → `ScheduleLauncher.Request(day)`; поверх блокировки не показываться.

- [ ] **Шаг 1:** реализовать; сборка Android — 0 ошибок.
- [ ] **Шаг 2: проверка смысла на эмуляторе, если запущен** (иначе отметить непроверенным — закроет задача 10): запись `Notify` через 2 мин → уведомление пришло при выключенном экране; нажатие открывает «Распорядок» на её дне; сменить у будущей записи `Ring` на `Notify` → `adb shell dumpsys alarm | grep chrono` не показывает `alarm/` PI этой записи.
- [ ] **Шаг 3: коммит** `Android: уведомления распорядка и планирование по уровню оповещения`.

### Задача 5: iOS — `IosNotificationPlan` и уведомление `Notify`

**Files:** Modify `src/Chrono/Services/IosNotificationPlan.cs`, `Platforms/iOS/AlarmScheduler.cs`, `Platforms/iOS/NotificationDelegate.cs`; Test `tests/Chrono.Tests/IosNotificationPlanTests.cs`.

**Interfaces — Consumes:** задача 1, `ScheduleLauncher` (задача 2). **Правила `Build`:** записи с `Alert == None` не планируются; разовость — `IsOneShot()`; у `Notify` одно уведомление (смещение 0), у `Ring` — цепочка из 4; лимит `MaxAlarms = 16` срабатываний общий (`ponytail:` — уведомления дел вытесняют дальние будильники; при нехватке — отдельные лимиты). Идентификаторы срабатываний не меняются (`{id}-{k*4+i}`), чтобы `ChainIds` работал как раньше.

- [ ] **Шаг 1: тесты:** `Build_NoneAlert_NotPlanned`; `Build_Notify_SingleNotification`; `Build_OnceThenDelete_SingleOccurrence` (Ring → ровно 4 уведомления, не 64); `Build_ChainIdsUnchangedForRing` (существующие тесты остаются зелёными).
- [ ] **Шаг 2:** красные → реализовать → зелёные; мутация: вернуть `alarm.Repeat == RepeatKind.None` вместо `IsOneShot()` — `Build_OnceThenDelete_SingleOccurrence` краснеет.
- [ ] **Шаг 3:** `AlarmScheduler` (iOS): для `Notify` — `UNNotificationSound.Default`, `InterruptionLevel = Active`, в `UserInfo` ключи `alarm_id` и `open_day`. `NotificationDelegate`: если есть `open_day` — `WillPresent` показывает баннер со звуком (`Banner | Sound | List`), `DidReceive` вызывает `ScheduleLauncher.Request`; путь `Ring` не меняется. Сборка `-f net11.0-ios` — 0 ошибок.
- [ ] **Шаг 4: коммит** `iOS: уведомления распорядка в общем плане уведомлений`.

### Задача 6: вкладка «Будильники» и сигнал

**Files:** Modify `ViewModels/AlarmListViewModel.cs`, `ViewModels/RingViewModel.cs`.

- `AlarmListViewModel.Load`: список и «Ближайший — через …» только из записей `Alert == Ring`.
- `AlarmItem.Details`: `OnceThenDelete` → `"{ddd, d MMM} · {Repeat_OnceThenDelete}"`.
- `AddCommand` — без изменений (`edit`; уровень по умолчанию `Ring` задаёт редактор).
- `RingViewModel`: при `Load` запомнить запись; в `StopAsync` и при верном ответе в `ChooseAsync` после `StopRinging` — если `Repeat == OnceThenDelete`, `alarmService.Delete(id)`.

- [ ] **Шаг 1:** реализовать; сборка Android — 0 ошибок.
- [ ] **Шаг 2: коммит** `Будильники: только записи со звонком; удаление «Один раз и удалить» по «Стоп»`.

### Задача 7: экран «Распорядок»

**Files:** Replace `Views/SchedulePage.xaml(.cs)` (заглушка задачи 2); Create `ViewModels/ScheduleViewModel.cs`; Modify `MauiProgram.cs` (регистрация VM).

**Interfaces — Consumes:** `DayPlan.For/Week` (3), `AlarmService.GetAll/GetDone/SetDone` (1), `ItemIcons.ImageFor` (2), `AlarmSchedule.Until` (для «осталось»). Маршрут редактора: `edit?id={id}` и `edit?day={yyyy-MM-dd}&alert=Notify` (задача 9 читает эти параметры).

**`ScheduleViewModel : ObservableObject, IQueryAttributable`** — параметр `day`; по умолчанию сегодня. Команды: `SelectDay(DateOnly)`, `NextWeek`, `PrevWeek`, `ToggleDone(DayEntryItem)`, `Edit(DayEntryItem)`, `Add` (неактивна, если выбранный день < сегодня — спецификация 6.5). Свойства: `TitleDay` (`d MMMM`), `TitleYear` (`yyyy`), `Days` (7 ячеек: подпись, число, выбран, сегодня, выходной, количество точек `min(записей, 5)`), `Entries`, `IsEmpty`. Пересчёт: `OnAppearing` и таймер страницы раз в 30 с.

**Вид** — спецификация 6.5 и макет B (`.superpowers/brainstorm/289-1792481550/content/timeline-style.html`): заголовок `ScreenTitle` из двух `Span` (год — `GoldInk`); неделя — ячейки 44, кружок 40, выбранный `MetalBrush` + `OnMetal`, сегодня — кольцо 1.5 `Gold`, выходные `GoldInk`; свайп влево/вправо по неделе (`SwipeGestureRecognizer`) — соседняя неделя. Строка ленты: время `Footnote`/`TextMuted`; иконка в круге 42 (`Surface`, обводка `Hairline`), у `IsCurrent` — вертикальная капсула `MetalBrush` с кромкой `Gold`, высота `max(42, DurationMinutes × 1.2)` pt, иконка светлая; мелкая строка `HH:mm–HH:mm · N мин` (`DurationMinutes`), без длительности — `HH:mm`, у текущей — `ScheduleLeft`; название `RowValue`; кружок 22 справа: пустой с обводкой `Hairline` / залит `Gold`, `SemanticProperties.Description = MarkDone`. `IsMuted` → время и название `Hairline`. Пусто — `ScheduleEmpty`. «+» — как на `AlarmListPage` (60, `MetalBrush`, кромка `Gold`, тень primary), неактивная — opacity .35.

Иконка на капсуле: тёмная SVG на металле не видна, перекрасить `Image` поэлементно нечем (CommunityToolkit.Maui не подключён — проверено, пакет не добавлять). Поэтому на капсуле иконка лежит в круге `Surface` 30 pt у верхнего края капсулы.

Строка ленты в VM — `DayEntryItem` (определяет эта задача): обёртка над `DayEntry` с готовыми текстами (`TimeText`, `RangeText`, `Title`, `IconImage`) и `CapsuleHeight`.

- [ ] **Шаг 1:** реализовать; сборка Android — 0 ошибок.
- [ ] **Шаг 2: проверка рендером** (эмулятор, если запущен; иначе — в задаче 10): скриншот ленты с тремя записями, одна текущая, одна отмеченная.
- [ ] **Шаг 3: коммит** `Экран «Распорядок»: неделя, лента дня, отметки`.

### Задача 8: шторки записи

**Files:** Create `ViewModels/OptionsSheetModel.cs`, `ViewModels/DurationSheetModel.cs`, `Views/Sheets/AlertSheet.xaml(.cs)`, `Views/Sheets/IconSheet.xaml(.cs)`, `Views/Sheets/DurationSheet.xaml(.cs)`.

**Produces:**
```csharp
// Один выбор из списка; черновик до «Окей». Образец — RepeatSheetModel.
public sealed partial class OptionsSheetModel : ObservableObject
{
    public ObservableCollection<ChoiceOption> Options { get; }
    public OptionsSheetModel(IEnumerable<ChoiceOption> options);
    public string SelectedId { get; }
    public event EventHandler<bool>? Done;
    public void Load(string selectedId);
    // команды: Select(ChoiceOption), Ok, Cancel
}
public sealed partial class DurationSheetModel : ObservableObject
{
    public static readonly IReadOnlyList<int> Presets = [0, 15, 30, 45, 60, 90];
    public int Minutes { get; }               // 0…720
    public string CustomText { get; set; }    // своё значение; пусто или не число 1…720 → «Окей» неактивен
    public event EventHandler<bool>? Done;
    public void Load(int minutes);
    // команды: SelectPreset(int), Ok, Cancel
}
```
- `AlertSheet` — строки как `RepeatSheet` (три варианта, ✓ у выбранного), над `OptionsSheetModel`.
- `IconSheet` — сетка 4×3 иконок `ItemIcons` в кругах 56 (выбранная — `MetalBrush`), над `OptionsSheetModel` (`Id` — ключ иконки, `Name` — `Icon_<key>` для TalkBack).
- `DurationSheet` — чипы `Presets` (`DurationNone`, `DurationMinutes`), ниже `Entry` `Keyboard.Numeric` с подписью `DurationCustom`; ввод в `Entry` снимает выбор чипа. Правила формы — `fix-user-experience.md` (маски нет, курсор не трогаем; кнопки не под клавиатурой — страница уже `SafeAreaEdges="…, All"`).

- [ ] **Шаг 1:** реализовать; сборка Android — 0 ошибок.
- [ ] **Шаг 2: коммит** `Шторки записи: оповещение, иконка, длительность`.

### Задача 9: экран записи

**Files:** Modify `ViewModels/AlarmEditViewModel.cs`, `Views/AlarmEditPage.xaml`, `Views/AlarmEditPage.xaml.cs`.

**Consumes:** задачи 1, 2, 8. Параметры маршрута: `id`; `day` (`yyyy-MM-dd`); `alert` (`AlertKind` по имени).

- Новые свойства VM: `Icon`, `DurationMinutes`, `Alert`, `StartsOn`; `IsSoundRowsVisible => Alert == Ring`; тексты `IconImage`, `DurationText`, `AlertText`. Все новые — в `MarkDirty` по образцу существующих.
- Создание: `Alert` из параметра (`Notify` из ленты), иначе `Ring`; `StartsOn = day ?? сегодня`; `Date = day`, только если `day > сегодня` (для сегодня — «Ближайшее»). Редактирование: значения из записи.
- Решение 4: `IsDateRowVisible`, `PlateTitle`, `Target`, загрузка `Date` — через «разовый» (`None` или `OnceThenDelete`). `Target` для повтора передаёт `StartsOn = this.Date ?? this.StartsOn` в `new Alarm { … }.NextAfter(…)`.
- `SaveAsync`: новые поля; `StartsOn = this.Date ?? this.StartsOn`.
- Разметка: группа строк сверху вниз — **«Повтор»** (первым), «Время», «Дата»; вторая группа — «Название», «Иконка», «Длительность», «Оповещение», «Звук» и «Вибрация» (последние две — `IsVisible="{Binding IsSoundRowsVisible}"`). Три новые шторки — `BottomSheet` по образцу существующих; `CloseSheet` и `IsAnySheetOpen` учитывают их.

- [ ] **Шаг 1:** реализовать; сборка Android и iOS — 0 ошибок; `dotnet test` — зелёный.
- [ ] **Шаг 2: проверка ввода на эмуляторе** (если запущен; иначе — задача 10): своё значение длительности «4», «45» по одному символу, чтение поля после каждого; скриншот с открытой клавиатурой — «Окей» и «Сохранить» видны.
- [ ] **Шаг 3: коммит** `Экран записи: повтор первым, иконка, длительность, оповещение, дата начала`.

### Задача 10: приёмка на эмуляторе и `DESIGN.md`

**Files:** Modify `DESIGN.md` (разделы «Нижнее меню», «4.9 Распорядок», шторки «Оповещение», «Иконка», «Длительность»; отступление — решение 6).

- [ ] **Шаг 1:** `dotnet test` и сборка Android — зелёные; установить на эмулятор.
- [ ] **Шаг 2: сценарии** (каждый — скриншот в отчёт):
  1. Обновление поверх версии с `main`: созданные до обновления будильники на месте, звонят, видны в ленте (Review Focus 1).
  2. Запись `Notify` через 2 мин, экран выключен → уведомление со звуком; нажатие → «Распорядок» на её дне (RF 5).
  3. Запись `Ring → Notify` до срабатывания → полноэкранного сигнала нет, есть уведомление (RF 2).
  4. «Один раз и удалить» `Notify`, 2 мин длительность → пришло, видна текущей, через 2 мин исчезла из ленты при следующем открытии.
  5. «Один раз и удалить» `Ring` → «Стоп» → записи нет ни в одной вкладке.
  6. «Каждый день» на понедельник следующей недели → в ленте с понедельника, раньше — нет; «Ближайший» на «Будильниках» не показывает её раньше срока, если она `Ring` (RF 4).
  7. Прошедший день: «+» неактивен, галочка ставится.
  8. Вкладки: «Настройки» без «назад»; редактор и сигнал без нижней панели.
- [ ] **Шаг 3:** правки `DESIGN.md`; коммит `DESIGN.md: нижнее меню и распорядок`.

---

## Журнал исполнения

Записывать после каждой задачи: хеш коммита (сверять с `git log`, не с отчётом), что проверено исполнением, что осталось непроверенным, **выводы, меняющие следующие задачи** (уточнённые сигнатуры, найденные ограничения).

| Задача | Коммит | Проверено | Непроверено / выводы |
|---|---|---|---|
| — | `ef9c21a` | спецификация | базовая линия: 57 тестов, Android 0/0 |
