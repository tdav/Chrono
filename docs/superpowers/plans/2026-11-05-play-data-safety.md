# Play Console: анкета «Безопасность данных» (Data safety) для Chrono

Дата: 2026-11-05. Пакет `com.chrono.sata`. Сборка первого релиза: без `ChronoHealthConnect` (константа `CHRONO_HEALTH` не определена).
Пути — относительно `src/Chrono/`. Политика: `docs/privacy/index.html`.

## 1. Факты из кода

| Утверждение | Источник |
|---|---|
| Нет Firebase/AppCenter/Sentry/рекламы: пакеты — MAUI Controls, CommunityToolkit.Mvvm, Caching.Memory, Logging.Debug, Health Connect, Play Services Auth, Fragment.Ktx, WorkManager | `Chrono.csproj:68-82` |
| Health Connect (разрешения `health.*`) только при `CHRONO_HEALTH`; по умолчанию выключено | `Chrono.csproj:36-41`, `Platforms/Android/HealthConnect.cs:14-20` |
| Вход Google через `AuthorizationClient`, области: `calendar.app.created`, `calendar.calendarlist.readonly`, `openid`, `email` | `Platforms/Android/GoogleAuth.cs:27-33`, `Services/GoogleScopes.cs:11-13` |
| Email аккаунта сохраняется в `SecureStorage` (для отзыва доступа при выходе) | `GoogleAuth.cs:94-101` |
| Email также пишется в `sync.json` (`SyncState.Email`); в `sync.json` же CalendarId, SyncToken, очередь изменений | `Services/SyncStore.cs:16-29`, `Services/SyncLauncher.cs:192` |
| Токен доступа хранится в `IMemoryCache` (50 мин), на диск не пишется | `GoogleAuth.cs:25,109`, `Services/GoogleCalendarClient.cs:65-70` (кэш по `TokenKey`) |
| Единственные сетевые адреса: `https://www.googleapis.com/calendar/v3/` и `https://openidconnect.googleapis.com/v1/userinfo` (+ диалоги Google Play services); все HTTPS | `GoogleCalendarClient.cs:20,40` |
| Ищется/создаётся календарь «Chrono» (`calendarList.list`, `calendars.insert`), события `events.list/insert/update/delete/instances` только в нём | `GoogleCalendarClient.cs:44-133,137-370` |
| В событие уходят: название (Label, иначе «Будильник»), дата/время, повтор, напоминания (до 5), приватное свойство `chronoId` | `Services/CalendarMapping.cs:27,102-124` |
| Фоновая синхронизация каждые 15 мин (WorkManager), только если есть вход | `Platforms/Android/SyncWorker.cs:27,53` |
| Будильники — `alarms.json`, отметки «сделано» — `done.json` во внутренней папке приложения | `Services/AlarmStore.cs:33-34`, `Services/SyncStore.cs:44` |
| Настройки (тема, режим выключения, умное пробуждение, подсказки) — `Preferences` | `Services/ThemeSettings.cs:11`, `DismissSettings.cs:11`, `SmartWakeSettings.cs:10`, `ViewModels/AlarmListViewModel.cs:111-216` |
| Выход: отзыв доступа Google, стирание Email/CalendarId/SyncToken/очереди в `sync.json`, удаление `sync.json.bad`, отмена воркера, очистка кэша токена | `GoogleAuth.cs:112-141`, `Services/SyncLauncher.cs:126-149` |
| Календарь «Chrono» и события в аккаунте при выходе НЕ удаляются (вызовов удаления календаря нет) | `Services/SyncLauncher.cs:126-149`, `GoogleCalendarClient.cs` (только `events.delete`, строка 285) |
| Резервное копирование включено; из копий исключён только файл SecureStorage. `alarms.json`, `sync.json` (с email) и Preferences в копию попадают | `Platforms/Android/AndroidManifest.xml:3`, `Resources/xml/backup_rules.xml`, `Resources/xml/data_extraction_rules.xml` |
| Серверов разработчика нет (сетевых вызовов к другим хостам нет) | поиск по `*.cs`: URL только Google (`GoogleCalendarClient.cs:20,40`; iOS-файл `Platforms/iOS/GoogleAuth.cs:22-24` не входит в Android-сборку) |

## 2. Верхний уровень анкеты

| Вопрос | Ответ | Основание |
|---|---|---|
| Собирает ли приложение обязательные типы данных пользователя | Да (см. ниже: только email и события календаря, и только при входе в Google) | см. §1 |
| Передаёт ли данные | Да, Google (провайдер календаря) по действию пользователя. По определению Play передача поставщику услуг, действующему от имени разработчика, и передача по запросу пользователя не считается «sharing» — **не уверен** (см. §6); безопасный вариант — отметить «Shared» не нужно, но указать «Collected» | — |
| Шифруются ли данные при передаче | Да (HTTPS) | `GoogleCalendarClient.cs:20,40` |
| Можно ли запросить удаление данных | Да (выход из аккаунта + отзыв доступа + удаление приложения; контакт в политике) | `GoogleAuth.cs:112-141` |
| Ссылка на политику конфиденциальности | URL на GitHub Pages: `.../privacy/` (подставить после публикации) | `docs/privacy/index.html` |
| Приложение следует Families Policy | Нет (не для детей до 13) | — |
| Безопасные практики: соответствует ли Play Families | не применимо | — |

## 3. Типы данных

Формулировка «Collected»: данные покидают устройство. Данные только на устройстве (alarms.json и др.) в анкете не указываются.

| Категория → тип | Collected | Shared | Обязательно/опционально | Цели | Эфемерно | Основание |
|---|---|---|---|---|---|---|
| Personal info → Email address | Да | Нет (получатель — сам Google) | Опционально (только при входе в Google) | App functionality; Account management | Нет (хранится в SecureStorage и `sync.json` до выхода) | `GoogleAuth.cs:94-101`, `SyncLauncher.cs:192` |
| Calendar → Calendar events | Да | Нет (Google как календарь по запросу пользователя) | Опционально (только при включённой синхронизации) | App functionality | Нет (события остаются в календаре «Chrono» в аккаунте) | `GoogleCalendarClient.cs:137-300`, `CalendarMapping.cs:102-124` |
| App activity (App interactions, In-app search history, Installed apps, Other user-generated content) | Нет. Название будильника уходит в событие календаря — это часть Calendar events, отдельно не декларировать | — | — | — | — | `CalendarMapping.cs:102` |
| App info and performance (Crash logs, Diagnostics) | Нет | Нет | — | — | — | отсутствуют крэш-репорты/аналитика, `Chrono.csproj:68-82` |
| Device or other IDs | Нет (рекламного ID и собственных идентификаторов нет) | Нет | — | — | — | поиск по коду |
| Location, Contacts, Photos/Videos, Audio, Files, Messages, Financial, Health and fitness, Web browsing | Нет | Нет | — | — | — | в релизе без `CHRONO_HEALTH` разрешений `health.*` нет; остальное не используется |

Примечание по Health and fitness: в этой сборке не декларировать. При включении `ChronoHealthConnect` в будущем релизе добавить Health info (Heart rate, Sleep), декларацию Health Connect и обновить политику.

## 4. Безопасность и удаление (раздел «Data security»)

- Encrypted in transit: Да (HTTPS к `googleapis.com`).
- Mechanism to request deletion: Да. Пользователь выходит из аккаунта (отзыв доступа, стирание локального состояния синхронизации), удаляет календарь «Chrono» в Google Календаре, удаляет приложение; запросы — на `tdavron@gmail.com`.
- Данные на серверах разработчика отсутствуют, поэтому «Delete data URL» можно указать страницу политики (раздел «Удаление данных»).
- Независимая проверка безопасности — нет.

## 5. Разрешения манифеста

Манифест: `Platforms/Android/AndroidManifest.xml`. Строки 4-14 и 16-18.

| Разрешение (строка) | Назначение в приложении | Декларация в Play Console |
|---|---|---|
| `USE_EXACT_ALARM` (4) | Точное срабатывание будильника (`SetAlarmClock`/`SetExactAndAllowWhileIdle`, `Platforms/Android/AlarmScheduler.cs:85,163`) | **Да**: Permissions declaration «Exact alarm»; основная функция — будильник, допустимое применение |
| `SCHEDULE_EXACT_ALARM`, maxSdk 32 (14) | То же для Android 12–12L (пользователь выдаёт в настройках, `ReliabilityChecks.cs:21,41`) | Декларируется вместе с USE_EXACT_ALARM; на Android 13+ уже не действует |
| `USE_FULL_SCREEN_INTENT` (6) | Экран звонка поверх блокировки (`ReliabilityChecks.cs:22`, `CanUseFullScreenIntent`) | **Да**: декларация «Full-screen intent»; допустимо для будильника |
| `FOREGROUND_SERVICE` (7) | Служба звонка `AlarmRingService` | Входит в декларацию типов служб переднего плана |
| `FOREGROUND_SERVICE_SYSTEM_EXEMPTED` (8) | Тип `systemExempted` у `AlarmRingService` и `SmartWakeService` (`AlarmRingService.cs:15`, `SmartWakeService.cs:18`) | **Да**: декларация «Foreground service permissions»; в Play это разрешение требует обоснования (будильник — допустимый сценарий `systemExempted`); **не уверен**, что ревьюер примет тип — см. §6 |
| `POST_NOTIFICATIONS` (5) | Уведомления о будильниках/напоминаниях | Нет (runtime-разрешение) |
| `RECEIVE_BOOT_COMPLETED` (9) | Пересоздание будильников и воркера после перезагрузки (`BootReceiver.cs:16-20`) | Нет |
| `INTERNET` (10) | Синхронизация с Google Календарём | Нет |
| `ACCESS_NETWORK_STATE` (11) | Проверка сети для синхронизации; **не уверен**, что вызывается в коде (поиск `ConnectivityManager`/`Connectivity` не нашёл — возможно, тянется библиотеками MAUI/Play Services) | Нет |
| `VIBRATE` (12) | Вибрация при звонке (`AlarmRingService.cs:131-144`, `ScheduleReceiver.cs:147`) | Нет |
| `WAKE_LOCK` (13) | Удержание процессора службами звонка/умного пробуждения (`SmartWakeService.cs:84`) | Нет |
| `<queries>` пакет `com.google.android.apps.healthdata` (16-18) | Видимость Health Connect (только Android 13-) | Нет разрешения, но в выпуске без Health Connect запись лишняя — **рекомендуется убрать** (см. §6) |

В релизе нет: `QUERY_ALL_PACKAGES`, `READ_CALENDAR/WRITE_CALENDAR` (Google Календарь используется по REST с OAuth), `health.*`, location, contacts.

Дополнительные декларации Play Console: «Exact alarm», «Full-screen intent», «Foreground service types» (`systemExempted`), политика конфиденциальности, форма Data safety, OAuth: Google Cloud требует верификации приложения для области календаря (проверить, чувствительная ли `calendar.app.created`; `calendarlist.readonly` — чувствительная).

## 6. Вывод про акселерометр и SmartWakeService

- `SmartWakeService` использует акселерометр независимо от Health Connect: регистрирует `SensorType.Accelerometer` (`Platforms/Android/SmartWakeService.cs:87-92`); Health Connect читается дополнительно, если он доступен (`:94-100`). Сами показания не сохраняются и не передаются: считается число «движений» за минуту (`:107-123`).
- В релизной сборке без `CHRONO_HEALTH` `SmartWake.IsSupported == false` (`Platforms/Android/SmartWake.cs:17-22`). Тумблер в UI отключён: `IsEnabled="{Binding IsSmartWakeSupported}"` (`Views/SettingsPage.xaml:114`); при чтении настройки значение гасится: `smartWake.IsSupported && SmartWakeSettings.Enabled` (`ViewModels/SettingsViewModel.cs:39`).
- **Нюанс:** `AlarmScheduler` (`AlarmScheduler.cs:160`) и `SmartWakeService` (`SmartWakeService.cs:69,150`) проверяют только `SmartWakeSettings.Enabled` (по умолчанию `false`, `SmartWakeSettings.cs:10`) и не смотрят на `IsSupported`. Значение `true` может появиться только из `OnSmartWakeEnabledChanged` (`SettingsViewModel.cs:186-188`), которое в этой сборке недоступно (тумблер выключен), либо из резервной копии/другой сборки (Preferences попадают в бэкап — например, пользователь включил на сборке с Health и поставил релиз поверх; для чистой установки из Play невозможно). Практический риск: нулевой, но защитный `&& smartWake.IsSupported` в этих местах не помешает.
- Остальной код `SmartWakeService` объявлен в манифесте как служба (`[Service]`, тип `systemExempted`), поэтому формально присутствует в пакете; во время работы использует `WAKE_LOCK` и сенсор, но не запускается.

## 7. В чём не уверен

1. Считать ли отправку событий в Google «sharing»: по правилам Play передача по инициативе пользователя в сервис, который он сам подключил, обычно не «sharing». Формулировку подтвердить по текущей справке Play при заполнении анкеты.
2. Нужна ли декларация данных «Collected» для email, который не уходит на сервер разработчика: Play требует декларировать данные, покидающие устройство (в т. ч. при передаче через Google SDK). Выбрано «Collected» осознанно (безопаснее).
3. `ACCESS_NETWORK_STATE`: прямого вызова в коде не найдено (возможно, нужен библиотекам).
4. `<queries>` Health Connect в манифесте остаётся в сборке без Health Connect; рекомендую убрать/обернуть условием перед выпуском.
5. Приёмка типа `systemExempted` для `AlarmRingService` на ревью Play и верификация OAuth-областей Google — вне кода.
6. Резервное копирование: email в `sync.json` попадает в облачную копию Android (исключён только SecureStorage). В политике это указано; при желании можно добавить `sync.json` в исключения `backup_rules.xml`/`data_extraction_rules.xml`.
7. Календарь «Chrono» в аккаунте при выходе не удаляется; в политике это сказано прямо.
