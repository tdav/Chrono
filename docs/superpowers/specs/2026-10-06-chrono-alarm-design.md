# Chrono — мобильный будильник на .NET MAUI 11

Дата: 2026-10-06. Статус: дизайн согласован, спецификация на ревью.

## 1. Цель и критерии успеха

Приложение-будильник для Android и iPhone, предназначенное для публикации в Google Play и App Store.
Пользователь создаёт несколько разовых будильников на любую дату и время, включает и выключает их;
в момент срабатывания показывается текст, играет выбранная мелодия и работает вибрация.

**Главный критерий:** будильник срабатывает вовремя, даже если приложение выгружено из памяти,
смахнуто из недавних, телефон в Doze или был перезагружен. Это достигается не удержанием процесса
в памяти, а передачей расписания операционной системе.

Критерий приёмки первой версии — сценарии раздела 11 на Android-эмуляторе.

## 2. Зафиксированные решения

| Вопрос | Решение |
|---|---|
| Назначение | Публикация в сторах — учитываем политики Google Play и App Store |
| Повторы | Только разовые будильники; сработавший становится выключенным |
| Звук | Набор из 5 встроенных мелодий, одинаковый на обеих платформах |
| Срабатывание | Экран сигнала с одной кнопкой «Стоп», без «Отложить» |
| iOS | Локальные time-sensitive уведомления; AlarmKit — отдельным проектом позже |
| Языки | Английский (по умолчанию) и русский, по языку системы |
| Стиль | «Ночное небо», только тёмная тема |
| Иконка | Янтарный колокол на тёмно-синем градиенте |
| Подход | Собственный платформенный код за интерфейсом `IAlarmScheduler` |
| Хранение | JSON-файл, без SQLite |
| Тесты | Unit-тесты чистой логики (xUnit); платформенный код — приёмка на эмуляторе |
| Mac | Нет. iOS-код пишется, но сборка и приёмка iOS — отдельный шаг, когда появится Mac |

Готовые библиотеки отклонены: `Plugin.LocalNotification` 14.1.2 и `Shiny.Notifications` 5.8.1 собраны
под net10 (не net11) и не дают зацикленный звук через foreground service с полноэкранным сигналом.

## 3. Не входит в первую версию

- Повторяющиеся будильники, «Отложить», очередь одновременных сигналов.
- AlarmKit (iOS 26) — биндинга в `Microsoft.iOS.Ref.net11.0_26.5` нет, нужна Swift-обёртка и Mac.
- Direct Boot на Android (срабатывание после перезагрузки до первой разблокировки).
- Запуск и приёмка iOS (компиляция `net11.0-ios` на Windows работает и входит в проверки).
- Пользовательские рингтоны и системные рингтоны Android.
- Светлая тема, виджеты, синхронизация между устройствами.

## 4. Окружение

- Windows 11, .NET SDK `11.0.100-rc.1`, workloads `android` (37.0 rc) и `ios` (26.5).
- Целевые платформы: `net11.0-android;net11.0-ios`.
- Android: min API 26, target — значение по умолчанию для `net11.0-android`, не ниже 36.
- iOS: минимум 15.0 (уровень `time-sensitive`).
- Отладка и приёмка — Android-эмулятор; состав эмулятора и Android SDK проверить `dotnet-maui-doctor`
  до первой задачи.

## 5. Архитектура

Одно приложение `src/Chrono`. Общий код — модель, сервисы, UI; всё, что обращается к ОС, — в `Platforms/*`.

```
src/Chrono/
  Models/Alarm.cs             — Id (Guid), At (DateTime, локальное «настенное» время), Label,
                                SoundEnabled, SoundId, VibrationEnabled, IsEnabled
  Models/Sounds.cs            — каталог мелодий: id → имя файла и ключ строки resx
  Services/AlarmStore.cs      — чтение и атомарная запись alarms.json; путь приходит в конструктор
  Services/PlatformContracts.cs — IAlarmScheduler: Sync(IReadOnlyList<Alarm>), Cancel(Guid), StopRinging(Guid);
                                ISoundPlayer: Play(soundId, loop), Stop(); IReliabilityChecks
  Services/AlarmService.cs    — API для UI: GetAll / Save / Toggle / Delete / RescheduleAll; время из TimeProvider
  Services/IosNotificationPlan.cs — чистый расчёт цепочки уведомлений iOS и лимита 16
  Services/RingLauncher.cs    — открытие RingPage по запросу платформы (в т.ч. при холодном старте)
  ViewModels/                 — AlarmListViewModel, AlarmEditViewModel, RingViewModel,
                                ReliabilityViewModel (CommunityToolkit.Mvvm)
  Views/                      — SplashPage, AlarmListPage, AlarmEditPage, RingPage, ReliabilityPage
  Platforms/Android/          — AlarmScheduler, AlarmReceiver, BootReceiver, AlarmRingService,
                                правки MainActivity, ReliabilityChecks
  Platforms/iOS/              — AlarmScheduler, NotificationDelegate, ReliabilityChecks
  Resources/                  — AppIcon, Splash, Images/bell.svg, Fonts (Manrope), Raw/*.wav (корень, без
                                подпапок), Strings/AppResources.resx и AppResources.ru.resx
tools/gen_sounds.py           — скрипт синтеза мелодий (Python stdlib)
tests/Chrono.Tests/           — xUnit, net11.0
```

Правила:
- `IAlarmScheduler` — единственная граница с ОС; по реализации на платформу. AlarmKit станет ещё одной
  реализацией для iOS 26+ без изменений остального кода.
- `Models/*`, `AlarmStore`, `AlarmService`, `IAlarmScheduler`, `IosNotificationPlan` не используют API MAUI
  (`FileSystem.AppDataDirectory` передаётся из `MauiProgram`), поэтому подключаются в тестовый проект ссылкой
  на файлы. Отдельный проект `Core` не создаётся.
- Время хранится как локальное «настенное» (`DateTimeKind.Unspecified`, в JSON без смещения — `Save` приводит
  Kind принудительно) и переводится в абсолютное в момент планирования. Смена часового пояса или системного
  времени приводит к переустановке всех будильников (с `TimeZoneInfo.ClearCachedData()`).
- `IAlarmScheduler.Sync(список)` приводит расписание ОС к списку целиком: так iOS соблюдает лимит 16, а Android
  снимает выключенные. Удалённый будильник снимается отдельно через `Cancel(id)`.
- Сохранение из редактора всегда включает будильник (как в системных «Часах») и требует время в будущем.
- Стиль кода — по глобальным правилам: приватные поля без `_`, обращение через `this.`, без private-методов-
  помощников без необходимости, комментарии на русском.

## 6. Данные и потоки

**Хранение.** `alarms.json` в `FileSystem.AppDataDirectory`. Запись — во временный файл, затем
`File.Move(temp, path, overwrite: true)`. Пишет только UI-код; receiver'ы и сервис только читают.

**Сохранение** — `AlarmService.Save(alarm)`:
1. Если `At <= now` — ошибка валидации «Это время уже прошло», сохранение запрещено.
2. Запись в store.
3. Если `IsEnabled` — `scheduler.Schedule(alarm)`; повторный вызов с тем же `Id` заменяет прежнее расписание.

**Включение/выключение.** Выключение — `Cancel(id)`. Включение будильника с прошедшим временем не выполняется:
открывается редактор с подсказкой выбрать новую дату.

**Удаление** (кнопкой в редакторе или свайпом в списке) — `Cancel(id)` и удаление из store.

**«Сработавший — значит выключен».** `AlarmService.GetAll()` переводит включённые будильники с `At <= now`
в выключенные и сохраняет файл. Правило одно для обеих платформ, потому что на iOS приложение в момент сигнала
может не работать.

**Старт приложения** — `RescheduleAll()` для всех включённых будильников (восстанавливает расписание после
принудительной остановки на Android и ротирует лимит 16 на iOS).

**Текст сигнала** — `Label`, при пустом — строка «Будильник» / «Alarm» из resx.

**Два будильника в одну минуту** — новый сигнал заменяет текущий на экране; «Стоп» останавливает звучащий.

## 7. Android

**Планирование.** `AlarmManager.SetAlarmClock(info, pendingIntent)` — точный будильник, не ограничивается Doze
и App Standby, система показывает значок будильника. `PendingIntent` адресован `AlarmReceiver` с `Id` в extras.

**Срабатывание.**
1. `AlarmReceiver` запускает `AlarmRingService` как foreground service типа `systemExempted`. Запуск FGS из фона
   разрешён: точный будильник входит в исключения Android, а тип `systemExempted` разрешён приложениям
   с `USE_EXACT_ALARM`/`SCHEDULE_EXACT_ALARM` (проверено по developer.android.com, разделы service-types
   и restrictions-bg-start).
2. Сервис читает будильник из `alarms.json`, держит `WakeLock`, запускает:
   - мелодию в цикле через `MediaPlayer` с `AudioAttributes.Usage = Alarm` (если звук включён);
   - вибрацию по повторяющемуся шаблону (если включена);
   - уведомление в канале `alarm` (важность High, без собственного звука) с текстом, действием «Стоп»
     и full-screen intent на `MainActivity`.
3. `MainActivity` при intent с `Id` будильника включает `SetShowWhenLocked(true)` и `SetTurnScreenOn(true)`
   и открывает `RingPage`. `LaunchMode = SingleTop`, новый intent обрабатывается в `OnNewIntent`.
4. «Стоп» (на `RingPage` или в уведомлении) останавливает сервис.
5. Без остановки через 10 минут сервис выключает звук и вибрацию и оставляет уведомление
   «Пропущенный будильник HH:mm».

**Автозапуск.** `BootReceiver` на `BOOT_COMPLETED`, `TIME_SET`, `TIMEZONE_CHANGED`, `MY_PACKAGE_REPLACED`
читает `alarms.json` и вызывает `RescheduleAll()`.

**Манифест.**

| Разрешение | Назначение |
|---|---|
| `USE_EXACT_ALARM` | Точные будильники (API 33+), допускается политикой Play для будильников |
| `SCHEDULE_EXACT_ALARM` с `maxSdkVersion="32"` | Точные будильники на API 31–32 |
| `POST_NOTIFICATIONS` | Уведомления (runtime-запрос при первом запуске) |
| `USE_FULL_SCREEN_INTENT` | Сигнал поверх экрана блокировки |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_SYSTEM_EXEMPTED` | Сервис звонка |
| `RECEIVE_BOOT_COMPLETED`, `VIBRATE`, `WAKE_LOCK` | Автозапуск, вибрация, удержание CPU |

`REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` не используется (ограничен политикой Play).

**Экран «Надёжность»** (шестерёнка в шапке списка; автоматически при первом запуске). Проверки со статусом
✅/⚠️ и кнопкой «Исправить», открывающей нужный экран настроек:
1. Уведомления разрешены.
2. Точные будильники разрешены (`AlarmManager.CanScheduleExactAlarms()` на API 31+).
3. Полноэкранные уведомления разрешены (`NotificationManager.CanUseFullScreenIntent()` на API 34+,
   `ACTION_MANAGE_APP_USE_FULL_SCREEN_INTENT`).
4. Оптимизация батареи отключена (`PowerManager.IsIgnoringBatteryOptimizations`,
   `ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS`).

Ниже — текстовая подсказка для Xiaomi, Huawei, Samsung: где включить «Автозапуск».
Если хотя бы одна проверка не пройдена, в шапке списка — плашка «Будильник может не сработать — исправить».

## 8. iOS

**Планирование.** `UNUserNotificationCenter` + `UNCalendarNotificationTrigger` (без повтора),
`InterruptionLevel = UNNotificationInterruptionLevel.TimeSensitive2` — **не** `TimeSensitive`: устаревший член
биндинга имеет значение 3, что на стороне iOS означает Critical (проверено рефлексией по `Microsoft.iOS.dll`). Entitlement `com.apple.developer.usernotifications.time-sensitive`
в `Entitlements.plist`; включается в App ID и provisioning profile, одобрения Apple не требует.

**Цепочка.** На один будильник — 4 уведомления: `At`, `At+30 с`, `At+60 с`, `At+90 с`, идентификаторы
`{Id}-0..3`. iOS хранит не более 64 запланированных уведомлений, поэтому планируются ближайшие 16 включённых
будильников; остальные добавляются при `RescheduleAll()` на старте. Расчёт — чистая функция
`IosNotificationPlan.Build(alarms, now)`.

**Звук.** `UNNotificationSound.GetSound("<file>.wav")` из бандла; при выключенном звуке — без звука.
Вибрацией в фоне управляет система; в приложении на экране — `Vibration.Vibrate` по шаблону, если включена.

**Нажатие на уведомление** — `NotificationDelegate.DidReceiveNotificationResponse` открывает `RingPage(Id)`
и снимает оставшиеся уведомления цепочки.
**Приложение на экране** — `WillPresentNotification` не показывает баннер, открывает `RingPage` и играет
мелодию в цикле через `AVAudioPlayer` до «Стоп»; оставшиеся уведомления цепочки снимаются.

**Разрешения** — запрос `Alert | Sound` при первом запуске. «Надёжность» на iOS: уведомления разрешены,
time-sensitive разрешены (`UNNotificationSettings.TimeSensitiveSetting`); кнопка открывает настройки
приложения; текст: «В беззвучном режиме будет только вибрация».

Автозапуск на iOS не нужен: запланированные уведомления переживают перезагрузку.

## 9. UI и ресурсы

**Экраны** (макеты утверждены в визуальном компаньоне, `.superpowers/brainstorm/`):
- `AlarmListPage` — заголовок «Chrono», строка «Следующий через N ч M мин», карточки будильников (время
  крупно, «день недели, дата · текст», переключатель), выключенные — приглушены; удаление свайпом;
  плавающая кнопка «+»; шестерёнка → «Надёжность»; плашка предупреждения при непройденных проверках.
- `AlarmEditPage` — крупные время и дата (нажатие открывает нативные `TimePicker`/`DatePicker`), поле «Текст»,
  строка «Звук» с переключателем и выбором мелодии (список с прослушиванием по тапу), строка «Вибрация»;
  внизу «Удалить будильник» (для существующего), затем «Отмена» и «Сохранить», прижатые вправо,
  «Сохранить» крайняя справа.
- `RingPage` — качающийся колокол со звуковыми волнами, время, текст, дата, большая кнопка «Стоп».
- `ReliabilityPage` — раздел 7 / 8.
- `SplashPage` — см. ниже.

**Поле ввода (по памятке `~/.claude/references/fix-user-experience.md`).** В `App.xaml` —
`android:Application.WindowSoftInputModeAdjust="Resize"`; корневой layout `AlarmEditPage` —
`SafeAreaEdges="Container, Container, Container, All"` на `Grid` с `RowDefinitions="*,Auto"`, кнопки во второй
строке поднимаются над клавиатурой. Масок ввода нет.

**Тема** — только тёмная. Токены в `Resources/Styles/Colors.xaml`: фон — градиент `#0A0B1A` → `#2A2F6B`;
поверхность — белый 6 %; акцент — `#FFB35C` → `#FF7A59`; текст — `#E9EBFF`; приглушённый — `#9AA0D6`;
опасное действие — `#FF8F7A`.

**Шрифт** — Manrope (OFL, есть кириллица), начертания Light, Regular, SemiBold. Критерий пригодности
файлов: лицензия OFL, кириллица, статические TTF нужных начертаний.

**Иконка** — `appicon.svg` (фон: радиальный тёмно-синий градиент) + `appiconfg.svg` (янтарный колокол
с полупрозрачным радиальным «свечением» вместо SVG-фильтров) → adaptive icon на Android, иконка iOS.

**Анимированный splash** — двухступенчатый, так как нативные splash статичны:
1. `MauiSplashScreen`: тёмно-синий фон и колокол по центру.
2. `SplashPage` с тем же фоном и колоколом в той же позиции: ≈1,2 с — колокол качается (`RotateTo`),
   расходятся три волны (`ScaleTo` + `FadeTo`), проявляется «Chrono»; затем переход к списку.
При запуске по сигналу splash пропускается — сразу `RingPage`. Та же анимация колокола в цикле — на `RingPage`.

**Мелодии** — 5 WAV до 30 с: «Колокол», «Перезвон», «Цифровой», «Нарастающий», «Мягкий». Синтезируются
скриптом `tools/gen-sounds` (лицензионно чисто, воспроизводимо); результат лежит в `Resources/Raw/sounds/`
и используется обеими платформами (на iOS — как ресурс бандла для уведомлений).

**Локализация** — `AppResources.resx` (en) и `AppResources.ru.resx`: все строки UI, тексты уведомлений,
имена каналов уведомлений Android, названия мелодий.

## 10. Обработка ошибок

| Ситуация | Поведение |
|---|---|
| `alarms.json` не читается | Переименовать в `alarms.corrupt-<yyyyMMddHHmmss>.json`, начать с пустого списка, показать сообщение |
| Исключение планировщика (например, `SecurityException`) | Будильник сохранён; плашка «Будильник может не сработать — исправить» |
| Отказ в разрешениях | Та же плашка; приложение работает |
| Мелодия не воспроизводится (Android) | `RingtoneManager.GetDefaultUri(RingtoneType.Alarm)`; вибрация остаётся |
| Сигнал не остановлен 10 минут | Остановить звук и вибрацию, уведомление «Пропущенный будильник» |

## 11. Тестирование и приёмка

**Unit-тесты** (`tests/Chrono.Tests`, xunit.v3 4.0.1 на Microsoft.Testing.Platform — `global.json` с
`"test": {"runner": "Microsoft.Testing.Platform"}`, без VSTest-пакетов; net11.0; файлы логики подключены ссылкой, планировщик —
ручной фейк, время — собственный наследник `TimeProvider` с фиксированным `GetUtcNow()`, без пакета
`Microsoft.Extensions.TimeProvider.Testing`):
- `AlarmService`: отказ сохранить прошедшее время; `Save` включённого вызывает `Schedule`, выключенного — нет;
  `Toggle` вызывает `Schedule`/`Cancel`; `Delete` вызывает `Cancel` и удаляет; `GetAll` выключает
  сработавшие и сохраняет.
- `AlarmStore`: запись и чтение туда-обратно; повреждённый файл переименовывается, возвращается пустой список.
- `IosNotificationPlan`: 4 уведомления со смещениями 0/30/60/90 с; только включённые и будущие; не более
  16 будильников, ближайшие по времени.

Платформенный код автотестами не покрывается.

**Приёмка на Android-эмуляторе** (подтверждение скриншотами через MCP `maui-devflow` или `adb`):
1. Будильник на +2 мин, приложение смахнуто из недавних, экран заблокирован → полноэкранный сигнал со звуком
   и вибрацией; «Стоп» останавливает.
2. То же после `adb reboot` и разблокировки → сигнал приходит.
3. `adb shell dumpsys deviceidle force-idle` → сигнал вовремя.
4. Выключенный не звонит; сработавший становится выключенным в списке.
5. Звук выкл. + вибрация вкл. и наоборот → работает только выбранное.
6. Поле «Текст»: ввод по одному символу с чтением поля после каждого; скриншот с открытой клавиатурой —
   видны поле и кнопка «Сохранить».
7. Локали ru и en.
8. Иконка, splash-анимация, экран «Надёжность» — скриншоты и дизайн-ревью (`impeccable`).

## 12. Skills и инструменты реализации

| Skill / инструмент | Применение |
|---|---|
| `dotnet-maui:dotnet-maui-doctor` | Проверка окружения до первой задачи |
| `dotnet-maui:maui-theming` | Токены, стили, тёмная тема |
| `dotnet-maui:maui-collectionview` | Список будильников, `SwipeView` |
| `dotnet-maui:maui-data-binding` | Compiled bindings (`x:DataType`) |
| `dotnet-maui:maui-dependency-injection` | Регистрация сервисов и страниц |
| `dotnet-maui:maui-shell-navigation` | Маршруты, переход на `RingPage` |
| `dotnet-maui:maui-app-lifecycle` | Запуск по intent/уведомлению, `RescheduleAll` на старте |
| `dotnet-maui:maui-safe-area` | `SafeAreaEdges`, клавиатура |
| `impeccable:impeccable` | Дизайн-ревью и полировка по скриншотам |
| MCP `maui-devflow` | Управление приложением на эмуляторе для приёмки |

## 13. Риски

- **.NET 11 — RC.** Совместимость `CommunityToolkit.Mvvm` и xUnit с net11 проверить командой до фиксации
  версий в плане; при проблемах MAUI-сборки — фиксировать воспроизведение, не обходить в задачах.
- **Политики сторов.** `USE_EXACT_ALARM` и `USE_FULL_SCREEN_INTENT` требуют декларации в Play Console
  (основная функция — будильник). Entitlement time-sensitive — в App ID.
- **OEM-прошивки** (Xiaomi, Huawei) могут убивать приложение вопреки `setAlarmClock`; смягчение — экран
  «Надёжность» и подсказка про автозапуск.
- **iOS без Mac** — iOS-код компилируется на Windows, но не запускается; ошибки поведения всплывут позже.
- **Android 16 AudioHardening** — система пишет `background playback would be muted` для звука сервиса, пока
  активити не на экране (сейчас только предупреждение, `mutedState:none`). Если ограничение станет действующим —
  добавить сервису тип `mediaPlayback`. Проверяется при приёмке по `dumpsys audio`.
