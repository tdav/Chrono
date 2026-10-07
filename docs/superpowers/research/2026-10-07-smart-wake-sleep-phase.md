# Умное пробуждение (фаза лёгкого сна): исследование источников данных

Дата исследования: 2026-10-07. Проект: Chrono (MAUI, `net11.0-android;net11.0-ios`, Android minSdk 26, iOS 15; в манифесте уже есть `USE_EXACT_ALARM`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_SYSTEM_EXEMPTED`).

О цитировании: дословно приведены только идентификаторы (имена API, констант, разрешений, intent-действий) и очень короткие фрагменты. Остальное — точный пересказ с указанием страницы-источника и раздела. Утверждения, которые я не смог подтвердить первоисточником, помечены **[НЕ ПОДТВЕРЖДЕНО]** или вынесены в раздел 10.

---

## 0. Краткий вывод

1. **Ни один штатный канал «часы -> телефон» не отдаёт ТЕКУЩУЮ фазу сна в реальном времени**: ни Health Connect, ни HealthKit, ни Samsung Health Data SDK, ни Google Sleep API. Стадии сна всегда рассчитываются и записываются по итогам сна (или с задержкой).
2. Реальное время даёт только код, исполняющийся **на часах** (Wear OS Health Services, Samsung Health Sensor SDK, watchOS smart alarm session) либо собственные датчики телефона (акселерометр).
3. Поэтому реалистичная первая версия на MAUI: **собственная актиграфия на телефоне** (акселерометр в foreground-сервисе) в окне N минут перед будильником + жёсткий резервный будильник на заданное время. Health Connect (ЧСС/ВСР) - опциональное усиление с неизвестной задержкой, а не основа.
4. Окно 30 минут - де-факто стандарт отрасли (Google Smart Wake, Sleep as Android, Sleep Cycle, Apple smart alarm session — максимум 30 минут).
5. iOS без собственного watchOS-приложения: умного пробуждения в реальном времени не получить. .NET watchOS-приложения не собирает.

---

## 1. Health Connect (Android)

### 1.1 Типы записей и стадии сна

- `SleepSessionRecord` содержит `startTime`, `endTime`, список `stages` (`SleepSessionRecord.Stage`). Константы стадий: `STAGE_TYPE_UNKNOWN=0`, `AWAKE=1`, `SLEEPING=2` (спит, стадия неизвестна), `OUT_OF_BED=3`, `LIGHT=4`, `DEEP=5`, `REM=6`, `AWAKE_IN_BED=7`.
  Источник: https://developer.android.com/reference/androidx/health/connect/client/records/SleepSessionRecord (раздел Constants). Перечень из восьми стадий также в https://developer.android.com/health-and-fitness/health-connect/features/sleep-sessions (раздел Sleep sessions); там же: запись стадий «optional, but recommended» — то есть источник вправе записать сессию без стадий.
- Записи, нужные для оценки фазы: `HeartRateRecord` (серия `samples`), `HeartRateVariabilityRmssdRecord` (мгновенная, поле `heartRateVariabilityMillis`), `StepsRecord`.
  Источник: https://developer.android.com/health-and-fitness/health-connect/data-types (таблица Health Connect data types).

### 1.2 Когда часы пишут данные - в реальном времени или после сна

Что подтверждено первоисточниками:

- **Android-руководство по сну прямо требует писать `SleepSessionRecord` только после завершения сна.** Пересказ: детальные данные вроде пульса можно писать в течение сессии, но сама `SleepSessionRecord` «must only be written ... once the session has finished» (когда пользователь проснулся); для неё нужен `endTime` позже `startTime`.
  Источник: https://developer.android.com/health-and-fitness/health-connect/experiences/sleep (раздел Implement a sleep session -> Write a session).
- Рекомендация писателям для активного трекинга: писать данные по мере появления, но **не реже чем раз в 15 минут**, батчами через WorkManager.
  Источники: https://developer.android.com/health-and-fitness/health-connect/experiences/sleep (раздел Best practices), https://developer.android.com/health-and-fitness/health-connect/experiences/vitals (раздел про частые обновления от датчиков, тот же интервал до 15 минут).
  Следствие: даже в лучшем случае ЧСС из Health Connect отстаёт на единицы-десятки минут; гарантий нет, это лишь рекомендация авторам приложений-источников.
- **Samsung** (официально): сон «создаётся на часах, когда пользователь просыпается», затем данные передаются на телефон, Samsung Health создаёт запись сна и синхронизирует её в Health Connect; передача и синхронизация могут задерживаться в зависимости от доступности процессора.
  Источник: https://developer.samsung.com/health/blog/en/managing-sleep-data-with-samsung-health-and-health-connect (раздел Sleep data synchronization with Health Connect).
- Samsung FAQ: часы -> Samsung Health идут «по собственной политике» синхронизации ради батареи; Samsung Health пишет в Health Connect, как только данные созданы или изменены. Health Connect не поддерживает устройства Wear OS.
  Источник: https://developer.samsung.com/health/health-connect-faq.html
- **Pixel Watch / Fitbit (приложение Google Health)**: официальная справка подтверждает двусторонний обмен через Health Connect сон-сессиями, стадиями сна, пульсом, ВСР, шагами, но **не называет частоту и задержку записи**.
  Источник: https://support.google.com/googlehealth/answer/14506680 (раздел FAQ, таблица типов данных).
  Сторонний сайт FitMesh утверждает задержку цепочки Pixel Watch -> Fitbit -> Health Connect 15-30 минут; это не первоисточник, достоверность низкая.
- **Garmin**: по обсуждениям на официальном форуме Garmin Connect для Android начал отдавать данные в Health Connect с лета 2025 (релиз 5.14.1, июль 2025); сама справочная статья Garmin на форуме отсутствовала, содержимое не получено. https://forums.garmin.com/sports-fitness/running-multisport/f/forerunner-265-series/412657/garmin-sync-with-google-health-connect-june-2025
- **Zepp/Amazfit/Mi Band**: официальной статьи о Health Connect не нашёл; есть только сторонние сайты. **[НЕ ПОДТВЕРЖДЕНО первоисточником]**

### 1.3 Можно ли узнать ТЕКУЩУЮ фазу в 06:10

**Нет, если опираться на стадии.** Во время сна `SleepSessionRecord` по документации ещё не существует (пишется по завершении), поэтому список `stages` текущей ночи прочитать нельзя. Единственное, что может быть доступно в 06:10 - отдельные отсчёты `HeartRateRecord` / `HeartRateVariabilityRmssdRecord` / `StepsRecord`, если конкретный источник пишет их ночью и с неизвестной задержкой. Это нужно мерить эмпирически на реальных часах (см. раздел 9.4).

### 1.4 Разрешения и их запрос

Полные имена (страница https://developer.android.com/health-and-fitness/health-connect/data-types):
- `android.permission.health.READ_SLEEP` (и `WRITE_SLEEP`)
- `android.permission.health.READ_HEART_RATE`
- `android.permission.health.READ_HEART_RATE_VARIABILITY`
- `android.permission.health.READ_STEPS`
- Дополнительные, объявляются отдельно от типов данных: `android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND` (чтение в фоне) и `android.permission.health.READ_HEALTH_DATA_HISTORY` (чтение старше 30 дней).

Объявление в манифесте через `<uses-permission>`; набор должен совпадать с заявленным в Play Console. Источник: https://developer.android.com/health-and-fitness/health-connect/get-started (Step 3 -> Declare permissions).

Запрос: сначала `getGrantedPermissions`, затем `PermissionController.createRequestPermissionResultContract(...)`; набор строится через `HealthPermission.getReadPermission(<класс записи>)`. Источник: тот же get-started (Step 4).

Практика на .NET (из исходников Shiny.Health): на Android 14+ контракт возвращает псевдо-intent `androidx.activity.result.contract.action.REQUEST_PERMISSIONS`, права там - обычные runtime-разрешения, их можно запросить стандартным способом; на Android 13 и ниже контракт возвращает intent настоящей activity приложения Health Connect, нужен `StartActivityForResult`.
Источник: https://raw.githubusercontent.com/shinyorg/health/v2/Shiny.Health/Platforms/Android/HealthService.cs (метод `RequestPermissions`).

Экран rationale / privacy policy (в манифесте):
- `<activity>` с `intent-filter` действия `androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE` (Android до 13);
- `<activity-alias>` с `android:permission="android.permission.START_VIEW_PERMISSION_USAGE"`, действием `android.intent.action.VIEW_PERMISSION_USAGE` и категорией `android.intent.category.HEALTH_PERMISSIONS` (Android 14+).
  Источник: get-started (Step 3 -> Show your app's privacy policy dialog).
- Необязательный onboarding: `androidx.health.ACTION_SHOW_ONBOARDING` и alias с `android.health.connect.action.SHOW_ONBOARDING` (get-started, Step 4 -> Onboard users).
- Замечание по .NET (мои инженерные знания, не из документации): атрибутами `[Activity]` в .NET for Android `activity-alias` не описать; его придётся писать вручную в `Platforms/Android/AndroidManifest.xml`. **[проверить при реализации]**

### 1.5 Чтение в фоне, история, лимиты

- Чтение в фоне требует `READ_HEALTH_DATA_IN_BACKGROUND`; наличие функции проверяется `healthConnectClient.features.getFeatureStatus(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_IN_BACKGROUND)`. Пример в документации - периодический `WorkManager` (шаг 1 час в примере, у WorkManager минимальный период 15 минут).
  Источники: https://developer.android.com/health-and-fitness/health-connect/read-data (Background read example), get-started (Check feature availability).
- `READ_HEALTH_DATA_HISTORY` нам **не нужен**: по умолчанию читается до 30 дней назад от момента первой выдачи любого разрешения (read-data, раздел Read data older than 30 days).
- **Есть периодические и суточные лимиты на чтение, в фоне строже, чем на переднем плане.** Точные числа на странице не указаны. Рекомендуется использовать changelog (токены), а не частый raw-read. Источник: https://developer.android.com/health-and-fitness/health-connect/rate-limiting
  Для changelog: `getChangesToken` / `getChanges`, токен действует 30 дней (результаты поиска по https://developer.android.com/health-and-fitness/health-connect/sync-data; страницу целиком не разбирал).
- Для foreground-сервиса типа `health`: на API 36 запуск из фона возможен при наличии `READ_HEALTH_DATA_IN_BACKGROUND` (для сенсорных разрешений действуют while-in-use ограничения). Источник: https://developer.android.com/develop/background-work/services/fgs/service-types (раздел health).

### 1.6 Политика Google Play

- Нужна декларация использования данных и доступа к типам данных Health Connect в Play Console; без неё приложение не получит доступ к типам, требующим спецодобрения (https://developer.android.com/health-and-fitness/health-connect/data-types, вводный раздел).
- Политика: https://support.google.com/googleplay/android-developer/answer/12991134
  - доступ допустим только в рамках одобренных сценариев; «Fitness, wellness and coaching» прямо включает анализ сна («analyzing sleep health and patterns»). Подходит ли «будильник с умным пробуждением» под этот сценарий — решает ревью **[НЕ ПОДТВЕРЖДЕНО, суждение]**;
  - все запросы доступа проходят ревью; типичные причины отказа: расплывчатое обоснование, несоответствие сценариям, запрос «оптом» лишних разрешений, недостаточное описание сбора/использования данных;
  - обязательна подробная политика конфиденциальности;
  - **запрещён доступ к данным в приложениях без интерфейса (headless)**: нужна заметная иконка, настройки в приложении и т.п.;
  - политика распространяется и на приложения Wear OS.
- Для многоисточниковых приложений рекомендуют Matchmaking API (в SDK `1.2.0-alpha04`: `checkIfMatchmakingIsPossible`, `createMatchmakingIntent`). Это alpha: https://developer.android.com/jetpack/androidx/releases/health-connect
- Отдельных требований именно к `READ_HEALTH_DATA_IN_BACKGROUND` на найденных страницах не обнаружил. **[НЕ ПОДТВЕРЖДЕНО]**

### 1.7 Версии Android

- Health Connect встроен в платформу с Android 14 (API 34); на Android 13 и ниже - отдельное приложение из Google Play (https://developer.android.com/health-and-fitness/health-connect/get-started, Step 1).
- SDK поддерживает Android 8 (API 26)+, само приложение Health Connect - Android 9 (API 28)+ (get-started, Step 2, примечание). У Chrono minSdk 26, то есть на API 26-27 Health Connect недоступен.
- Стабильный SDK `connect-client` 1.1.0 (2025-10-08); последний alpha `1.2.0-alpha06` (2026-08-26), в `alpha05` minSdk библиотеки поднят до 24. Источник: https://developer.android.com/jetpack/androidx/releases/health-connect

---

## 2. .NET-биндинги

Проверено через API nuget.org (2026-10-07).

| Пакет | Версия | Опубликован | TFM | Примечание |
|---|---|---|---|---|
| `Xamarin.AndroidX.Health.Connect.ConnectClient` | 1.1.0.4 | 2026-06-27 | `net10.0-android36.0`, `net9.0-android35.0` | биндинг `androidx.health.connect:connect-client` 1.1.0; репозиторий dotnet/android-libraries |
| `Xamarin.AndroidX.Health.Connect.ConnectClientProto` | 1.1.0.3 | - | - | обязательная зависимость |
| `Xamarin.AndroidX.Health.Connect.ConnectClientExternalProtobuf` | 1.1.0.3 | - | - | сопутствующий |
| `Xamarin.AndroidX.Health.ServicesClient` | 1.1.0-alpha03 | 2024-07-30 | только `net8.0-android34.0` | **устарел**: апстрим Health Services 1.1.0 стабилен с 2026-09-23 |
| `Xamarin.GooglePlayServices.Location` | 121.4.0.1 | 2026-09-02 | net9 / net10 android | содержит `ActivityRecognitionClient` (Sleep API) |
| `Xamarin.GooglePlayServices.Wearable` | 120.0.1.2 | 2026-09-02 | net9 / net10 android | Data Layer |
| `Xamarin.AndroidX.Wear` | 1.4.0.2 | 2026-06-27 | net9 / net10 android | UI для часов |
| `Shiny.Health` | 2.0.1 | - | `net10.0`, `net10.0-android36.0`, `net10.0-ios26.0` | кроссплатформенная обёртка; зависит от ConnectClient 1.1.0.2 |

Про совместимость:
- ConnectClient 1.1.0.4 нацелен на `net10.0-android36.0`; проект Chrono — `net11.0-android`. По правилам совместимости TFM актив `net10.0-android36.0` должен подойти для `net11.0-android`, но сборкой я это **не проверял** **[НЕ ПОДТВЕРЖДЕНО]**.
- Версия 1.2.0-alpha (Matchmaking и пр.) в биндинге отсутствует (список версий пакета заканчивается на 1.1.0.4).
- Неофициальный пакет `SamsungHealthDataApi` 1.1.0.22 (net9.0-android35.0, автор «Your Name», описание на китайском) найден на nuget.org; это не официальный биндинг Samsung, доверять не стоит. Официального NuGet-биндинга Samsung Health Data SDK / Sensor SDK не нашёл.

Kotlin suspend-функции из C#:
- Биндинг показывает suspend-методы как методы с дополнительным параметром `IContinuation` (в Shiny: `client.ReadRecords(request, cont)`, `client.InsertRecords(records, cont)`, `client.PermissionController.GetGrantedPermissions(cont)`).
- Готовый рабочий приём из Shiny.Health (`CallSuspendAsync`): создать `TaskCompletionSource`, реализовать `IContinuation` (`Context => EmptyCoroutineContext.Instance`, `ResumeWith(Java.Lang.Object)`), вызвать метод; если вернулось не «`CoroutineSingletons`» (признак COROUTINE_SUSPENDED), результат синхронный.
- Подводные камни, описанные в том же коде: (1) при ошибке `Result` приходит как `kotlin.Result$Failure` с Throwable внутри - его нужно распаковать, иначе получите `InvalidCastException` или падение JNI; (2) возвращаемые Kotlin-списки имеют классы без биндинга (`Arrays$ArrayList`, `SingletonList`, `EmptyList`), приведение к `System.Collections.IList` падает - оборачивать через `JavaList(handle, DoNotTransfer)`; (3) `pageSize` в `ReadRecordsRequest` должен быть 1-5000.
- Статус SDK: `HealthConnectClient.GetSdkStatus(context)` == `SdkAvailable`, иначе предложить установить/обновить Health Connect.
- Ограничение готовой обёртки: Shiny.Health читает по сну **только суммарную длительность** (`SleepSessionRecord.SleepDurationTotal`), стадий не отдаёт; для стадий понадобится свой код поверх биндинга. Источник: https://github.com/shinyorg/health (readme и `HealthService.cs`).

---

## 3. Wear OS Health Services

- Health Services - сервис на самих часах (Wear OS 3+); клиенты `PassiveMonitoringClient`, `MeasureClient`, `ExerciseClient`. Источник: https://developer.android.com/health-and-fitness/health-services
- `PassiveMonitoringClient`: приложение на часах реализует `PassiveListenerService`; подходит для длительных сценариев (часы-сутки) и когда приложение не запущено. Источник: https://developer.android.com/health-and-fitness/health-services/monitor-background
- **Сна и стадий сна в Health Services нет.** Есть только состояние `UserActivityState.USER_ACTIVITY_ASLEEP` («пользователь спит»), а также `USER_ACTIVITY_PASSIVE`, `EXERCISE`, `UNKNOWN`. Страница `DataType` (проверил по тексту) содержит `HEART_RATE_BPM`, шаги и т.п., но ни сна, ни ВСР. Источники: https://developer.android.com/reference/androidx/health/services/client/data/UserActivityState, https://developer.android.com/reference/androidx/health/services/client/data/DataType
- Как получать ASLEEP: разрешение `ACTIVITY_RECOGNITION`, `setShouldUserActivityInfoBeRequested(true)` в `PassiveListenerConfig`, переопределить `onUserActivityInfoReceived()`; `stateChangeTime` «may be in the past» (момент перехода может быть в прошлом). Пассивный пульс — `DataType.HEART_RATE_BPM`. Источник: monitor-background.
- **Данные пакетируются** (хранятся на MCU и отдаются пачками по условиям системы), порядок определять по меткам времени внутри данных. То есть и на часах это не «мгновенный» поток. Источник: monitor-background (раздел про batching).
- Нужны: `ACTIVITY_RECOGNITION`, `RECEIVE_BOOT_COMPLETED` (восстановление регистрации), `com.google.android.wearable.healthservices.permission.PASSIVE_DATA_BINDING` на сервисе (monitor-background).
- Версии: `androidx.health:health-services-client` 1.1.0 стабилен с 2026-09-23. Источник: https://developer.android.com/jetpack/androidx/releases/health

**Получить данные часов на телефон в реальном времени без своего приложения на часах нельзя**: Health Services исполняется только на часах; Health Connect получает данные от приложений-источников батчами (раздел 1.2). Вывод по документам; прямого утверждения «нельзя» в первоисточнике нет, это логика из архитектуры.

Если нужно приложение на часах:
- Связь - Wearable Data Layer API (`DataClient`, `MessageClient`, `NodeClient`) из Google Play services; работает только между Wear OS и парным **Android**-телефоном; для Wear OS + iPhone Data Layer недоступен (только облачные API). Приложения на обоих устройствах должны иметь одинаковые имя пакета и подпись. Источник: https://developer.android.com/training/wearables/data/overview
- **.NET**: Wear OS-приложение собирается как обычное .NET Android-приложение (не MAUI), по свидетельству стороннего блога: https://www.saboit.de/blog/net-maui-android-watch-application-showcase-part-2 (там же: оба APK подписаны одним keystore; DataClient шлёт только изменившиеся данные, нужен timestamp). Официальной документации Microsoft по Wear OS не нашёл **[НЕ ПОДТВЕРЖДЕНО официально]**. NuGet-пакеты `Xamarin.AndroidX.Wear` и `Xamarin.GooglePlayServices.Wearable` для net10 есть (таблица выше), а биндинг Health Services устарел (net8, alpha) - практичнее писать часть для часов на Kotlin.
- Пример готового продукта: Sleep as Android требует отдельного приложения на часах Wear OS (3.0/4.0 и старше), также упоминает, что системный Bedtime Mode может подавлять вибрацию сторонних будильников. Источник: https://sleep.urbandroid.org/docs/devices/wearos.html (по пересказу).

---

## 4. Samsung

**Samsung Health Data SDK (на телефоне).**
- Читает записи Samsung Health, включая `Sleep` и `Heart rate`; стадии сна: Light, REM, Deep, Awake (кодлаб), чтение через `ReadDataRequest` с `DataTypes.SLEEP`, разрешение `Permission.of(DataTypes.SLEEP, AccessType.READ)`. Источники: https://developer.samsung.com/health/data/overview.html, https://developer.samsung.com/codelab/health/sleep-data.html
- Требования: Samsung Health 6.30.2+, Android 10+, не работает на эмуляторе, только для fitness/wellness (overview, Limitations).
- Данные с Galaxy Watch попадают в Samsung Health на телефоне по политике синхронизации; сон формируется на часах при пробуждении (раздел 1.2). Значит стадии текущей ночи в 06:10 не появятся. Прямого заявления Samsung «SDK отдаёт сон только после пробуждения» не нашёл; вывод опирается на описание потока синхронизации.
- Доступ: приложение работает только при включённом режиме разработчика (для тестов); для распространения нужна **заявка партнёра** (регистрация пакета и подписи SHA-256 в системе Samsung Health). Источник: https://developer.samsung.com/health/data/process.html. Сроки рассмотрения не указаны.

**Samsung Health Sensor SDK (на часах).**
- Работает на Galaxy Watch4 и новее с Wear OS powered by Samsung; приложение на часах получает акселерометр, PPG, ЭКГ, пульс **с межударными интервалами (IBI)**; непрерывные данные приходят событиями с заданным периодом, без пробуждения CPU, рассчитано на круглосуточное отслеживание. Источник: https://developer.samsung.com/health/sensor/overview.html
- Распространение - только после партнёрской заявки (пакет + SHA-256), иначе лишь режим разработчика. Источник: https://developer.samsung.com/health/sensor/process.html
- Это даёт сырые данные в реальном времени, но требует отдельного приложения на часах и партнёрства.

**Умный будильник Samsung.** Нативной функции «Smart alarm / Smart wake» на Galaxy Watch в официальных источниках не нашёл; в темах сообщества Samsung пользователи просят такую функцию, а ответы говорят об её отсутствии (страницы сообщества отдавали 403, пересказ из поиска). Новостные материалы One UI Watch 8/9 и Galaxy Watch9 её не упоминают (проверил). Утверждение из постановки задачи о «Smart alarm» на Galaxy Watch **не подтверждено**.

---

## 5. Google Sleep API (`ActivityRecognitionClient`)

Источники: https://developers.google.com/android/reference/com/google/android/gms/location/ActivityRecognitionClient, `.../SleepClassifyEvent`, `.../SleepSegmentEvent`; обучающая статья https://www.kodeco.com/24765589-android-sleep-api-tutorial-getting-started

- `requestSleepSegmentUpdates(PendingIntent, SleepSegmentRequest)` подписывает на `SleepSegmentEvent` (итог сна) и/или `SleepClassifyEvent` (периодическая классификация). Рекомендуется перерегистрировать после `BOOT_COMPLETED` и `MY_PACKAGE_REPLACED`.
- `SleepClassifyEvent`: метка времени, `getConfidence()` 0-100 (уверенность, что человек спит), `getMotion()` 1-6 (больше = больше движения телефона, по акселерометру), `getLight()` (освещённость). Приходят «at a regular interval, for example, every 10 minutes».
- `SleepSegmentEvent`: приходит **после пробуждения**, содержит начало и конец сна за сутки (`STATUS_SUCCESSFUL`, `STATUS_MISSING_DATA`, `STATUS_NOT_DETECTED`).
- Разрешение: `android.permission.ACTIVITY_RECOGNITION` (Android 10+); для Android 9 и ниже `com.google.android.gms.permission.ACTIVITY_RECOGNITION`.
- **Не поддерживается на носимых устройствах** (`FEATURE_WATCH`). Режимы энергосбережения могут ухудшать качество и интервалы.
- Нужен физический телефон на Android 10+ с Google Play services (kodeco).
- Статус: пометок об устаревании Sleep API на проверенных страницах не обнаружил. Awareness API объявлен устаревшим (другой API). **[полнота проверки ограничена]**

Как запасной источник без часов: даёт лишь «уверенность во сне» и грубое движение каждые ~10 минут; в окне 30 минут это 3-4 отсчёта, light/deep не различает. Годится как дешёвая подстраховка («пользователь уже явно бодрствует: motion высокое, confidence низкое»), но не как основной датчик фазы.

---

## 6. iOS / HealthKit

- `HKCategoryValueSleepAnalysis`: стадии `asleepCore` (лёгкий/промежуточный сон, N1+N2), `asleepDeep` (N3), `asleepREM`, `asleepUnspecified`, `awake`, `inBed`; стадии - с iOS 16. Отсчёты Apple Watch содержат `awake` только между отсчётами сна. Источники: https://developer.apple.com/documentation/healthkit/hkcategoryvaluesleepanalysis и подстраницы `asleepcore`, `asleepdeep`, `asleeprem`.
- **Когда Apple Watch записывает стадии:** в документации Apple для разработчиков момента записи не нашёл. Пользовательская справка Apple: после пробуждения в приложении Sleep видно итоги, история и стадии - в Health. Источник: https://support.apple.com/en-us/108906. Классификатор оценивает каждые 30-секундные эпохи по сигналу акселерометра (в том числе дыхательные микродвижения), согласие по четырём стадиям: средняя каппа 0,63. Источник: https://www.apple.com/health/pdf/Estimating_Sleep_Stages_from_Apple_Watch_Oct_2025.pdf. Записываются ли стадии потоково ночью - **не документировано**. **[НЕ ПОДТВЕРЖДЕНО]**
- Фоновая доставка: `enableBackgroundDelivery(for:frequency:withCompletion:)` + `HKObserverQuery`; с iOS 15 нужен entitlement `com.apple.developer.healthkit.background-delivery`; система будит приложение не чаще указанной частоты, для части типов предел — раз в час (в доке приведён пример `stepCount` на iOS; для `sleepAnalysis` значение не указано); на watchOS общий бюджет с фоновыми задачами (4 пробуждения в час). Источник: https://developer.apple.com/documentation/healthkit/hkhealthstore/enablebackgrounddelivery(for:frequency:withcompletion:)
- **Хранилище HealthKit шифруется, когда устройство заблокировано, поэтому в фоне приложение может не прочитать данные.** Запись возможна, чтение - нет. Источник: https://developer.apple.com/documentation/healthkit/protecting-user-privacy. Это ключевое ограничение для будильника: ночью телефон заблокирован.
- Приложение не знает, отказал ли пользователь в чтении: данные выглядят так, будто их нет (https://developer.apple.com/documentation/healthkit/hkhealthstore/authorizationstatus(for:)).
- **Узнать текущую фазу без своего watchOS-приложения на iOS нельзя** - по совокупности пунктов выше (нет потока стадий, фоновое чтение ограничено блокировкой).
- iOS не выполняет ваш код в фоне в момент будильника по требованию: `BGAppRefreshTask`/`BGProcessingTask` запускаются по усмотрению системы, processing - только когда устройство простаивает (https://developer.apple.com/documentation/backgroundtasks/bgprocessingtask).
- **AlarmKit (iOS 26+)**: будильник на заранее заданное время или таймер, переопределяет Focus и беззвучный режим; запрашивает авторизацию; нужен ключ `NSAlarmKitUsageDescription`. Динамического решения «звонить сейчас» без работающего кода он не даёт. Источники: https://developer.apple.com/documentation/alarmkit, https://developer.apple.com/documentation/alarmkit/scheduling-an-alarm-with-alarmkit. Chrono на iOS 15+, AlarmKit только iOS 26+.
- **watchOS: официальный механизм умного будильника — `WKExtendedRuntimeSession` типа «Smart alarm»**: «окно времени» для мониторинга пульса и движения; приложение само решает момент сигнала; планируется через `start(at:)` на срок до 36 часов вперёд; работает в фоне; лимит сессии 30 минут; сигнал подаётся вызовом `notifyUser(hapticType:repeatHandler:)`. Источники: https://developer.apple.com/documentation/watchkit/using-extended-runtime-sessions, https://developer.apple.com/documentation/watchkit/wkextendedruntimesession/start(at:)
- **.NET и watchOS:** `dotnet/macios` перечисляет iOS, Mac Catalyst, macOS, tvOS (https://github.com/dotnet/macios); поддержка Xamarin.iOS/Mac прекращена 1 мая 2024, поиск также указывает на удаление сборки watchOS в октябре 2024 (по пересказу поисковика, первоисточник не открыт). Практический вывод: watchOS-приложение пишется на Swift/Xcode отдельно. **[НЕ ПОДТВЕРЖДЕНО первоисточником]**
- Собственный будильник Apple умного пробуждения не имеет (страница https://support.apple.com/en-us/108906: обычный будильник со звуком и тактильной отдачей).

---

## 7. Как это делают существующие приложения

| Продукт | Окно | Источник данных | Если данных нет | Источник |
|---|---|---|---|---|
| Sleep as Android | по умолчанию **30 мин**; настраивается от 5 минут и больше, можно выключить | движение с датчиков телефона или носимого устройства; опция чувствительности (низкая/средняя/высокая); «Not before my sleep goal» | сигнал в заданное время | https://sleep.urbandroid.org/docs/sleep/smart_wake_up.html |
| Sleep Cycle | рекомендовано 30 мин, выбор **10-30 мин** | звук/движения в кровати (микрофон), также акселерометр | не описано отдельно; статья существует отдельно про «будильник всегда в конце окна» | https://support.sleepcycle.com/hc/en-us/articles/7858323091356-What-is-the-Smart-Alarm-Clock |
| Fitbit / Google Smart Wake | **30 мин** до времени будильника | пульс и мелкие движения (по новости); избегает глубокого сна | если не нашёл лучший момент - сигнал в установленное время | https://support.google.com/googlehealth/answer/14226604, https://www.androidauthority.com/pixel-watch-smart-wake-older-models-3711926/ |
| Pixel Watch | как выше; появилась на Pixel Watch 5, в сентябре 2026 (Wear OS 7) пришла на Pixel Watch 2, 3, 4 | на часах | как выше | AndroidAuthority (2026-09-16) |
| Apple (3rd party) | сессия smart alarm - не более 30 мин | пульс и движение на часах | приложение решает само | Apple docs (раздел 6) |
| Samsung Galaxy Watch | нативной функции не найдено | - | - | раздел 4 |

Детали Sleep as Android: Wear OS-часы подключаются через отдельное приложение на часах; при общей кровати возможны ложные срабатывания без Pair Tracking или персонального носимого датчика; пульс повышает точность определения пробуждений (https://sleep.urbandroid.org/docs/sleep/heart_rate.html). Интеграция с Health Connect у них - импорт/экспорт, в документации указана бета; использование HC для умного пробуждения в реальном времени не описано (https://sleep.urbandroid.org/docs/services/health_connect.html, по пересказу).

Алгоритмы коммерческих продуктов не раскрыты; известно лишь: Sleep as Android - движение с порогом чувствительности, Sleep Cycle - звук/движения, Google - пульс + движение. Какой именно классификатор в них - **не опубликовано**.

---

## 8. Алгоритмы определения лёгкого/глубокого сна

Что показывают первоисточники:

1. **Актиграфия (движение запястья/датчика) надёжно разделяет только сон и бодрствование, не лёгкий и глубокий сон.**
   - Cole-Kripke 1992: согласие со сном по полисомнографии около 88% (41 испытуемый). https://pubmed.ncbi.nlm.nih.gov/1455130/
   - Sadeh 1994: согласие 91-93%, оценка сна точнее оценки бодрствования. https://pubmed.ncbi.nlm.nih.gov/7939118/
   - Формула Cole-Kripke (реализация по руководству ActiGraph, пакет `actigraph.sleepr`): эпохи 60 секунд; счёт делится на 100 и ограничивается 300; `SI = 0.001 * (106*A[-4] + 54*A[-3] + 58*A[-2] + 76*A[-1] + 230*A[0] + 74*A[+1] + 67*A[+2])`; бодрствование, если `SI < 1`; окно 7 минут (4 прошлых + 2 будущих эпохи, то есть задержка решения ~2 минуты); разработан на взрослых 35-65 лет. https://cran.rstudio.com/web/packages/actigraph.sleepr/vignettes/detect-sleep.html. Коэффициенты рассчитаны под конкретные «счёты» Actiwatch/ActiGraph; для акселерометра телефона их нужно калибровать (самое осторожное: не переносить 1:1).
2. **Стадии точнее с пульсом/вариабельностью и циркадными признаками.** Oura (440 ночей, 106 человек): 4 стадии (лёгкий, глубокий, REM, бодрствование) точность 57% по акселерометру и 79% с вегетативными признаками (пульс/ВСР/температура) и циркадными; сон/бодрствование 94% и 96%. https://pubmed.ncbi.nlm.nih.gov/34201861/
3. Apple Watch, сырое ускорение + локальное СКО пульса + «clock proxy»: сон/бодрствование 90% (специфичность бодрствования 59,6%), три класса (бодрствование/NREM/REM) около 72%. Walch 2019. https://pubmed.ncbi.nlm.nih.gov/31579900/
4. Физиология ВСР: углубление сна связано с ростом парасимпатической модуляции, REM - с ростом симпатической; максимум парасимпатической активности в медленном сне около 02:00, максимум симпатической в REM - рано утром (Boudreau 2013, https://pubmed.ncbi.nlm.nih.gov/24293767/). В NREM баланс сдвигается от симпатического к парасимпатическому, в REM похож на бодрствование (Trinder 2001, https://pubmed.ncbi.nlm.nih.gov/11903855/). Это обосновывает признаки «пульс ниже и ВСР выше при глубоком сне».
5. Потребительские трекеры в целом плохо оценивают REM и глубокий сон (обзор Chest 2026, https://pubmed.ncbi.nlm.nih.gov/41811282/). То есть даже готовая стадия из Health Connect - это оценка с погрешностью.
6. Зачем вообще избегать глубокого сна: пробуждение из медленного сна даёт больше инерции сна, чем из стадий 1-2, REM - промежуточно; инерция обычно не превышает 30 минут (Tassi, Muzet 2000, https://pubmed.ncbi.nlm.nih.gov/12531174/). Прямых клинических испытаний выигрыша именно от «умных будильников» среди найденных источников нет. **[НЕ НАЙДЕНО]**

Мой инженерный вывод (предложение, не цитата): в окне 30 минут перед утренним временем глубокого сна обычно меньше, чем в начале ночи (общее знание о архитектуре сна; прямого источника в этом исследовании не искал **[НЕ ПОДТВЕРЖДЕНО]**), поэтому реалистичная цель v1 - не «поймать лёгкий сон», а «не звонить в момент устойчивой неподвижности и упавшего пульса, звонить при первых признаках движения/пробуждения». Это в точности и делает Sleep as Android (движение + чувствительность).

Предлагаемый простой алгоритм v1 (все пороги - настраиваемые параметры, требуют калибровки на живых ночах):
- Эпоха 60 секунд. Активность эпохи = сумма `|a - g|` или число пересечений порога по акселерометру телефона.
- Состояние «бодр/лёгкий сон» = Cole-Kripke-подобный `SI < 1` ИЛИ всплеск активности выше порога чувствительности после как минимум M минут покоя.
- Если есть свежие (моложе T минут) отсчёты ЧСС из Health Connect: дополнительное условие «пульс за последние 5 минут выше медианы за ночь на δ» - признак выхода из глубокого сна.
- Не звонить раньше `T_deadline - W`; при любой ошибке/нехватке данных звонить в `T_deadline`.
- Журналировать входные признаки и решение (для разбора на реальных ночах).

---

## 9. Итоговая рекомендация

### 9.1 Что реализуемо в версии 1 (Android, без часов)

Принцип: **жёсткий будильник не зависит от «умной» части**.

1. `setAlarmClock` / точный будильник на `T_deadline`: срабатывает в Doze («system never adjusts their delivery time»), не подпадает под ограничения запуска foreground-сервисов. Источник: https://developer.android.com/develop/background-work/services/alarms. `USE_EXACT_ALARM` уже есть в манифесте.
2. Второй точный будильник на начало окна `T_deadline - W` (W = 15/30/45 мин, по умолчанию 30). По нему запускается **foreground-сервис**, который в окне читает акселерометр телефона (с Android 9 фоновым приложениям события датчиков не приходят, нужен foreground service: https://developer.android.com/about/versions/pie/android-9.0-changes-all) и раз в 1 минуту считает эпоху.
   - Тип сервиса: у Chrono сейчас `SYSTEM_EXEMPTED`. Тип `health` требует одно из: `HIGH_SAMPLING_RATE_SENSORS`, `ACTIVITY_RECOGNITION`, `READ_HEART_RATE` и др. (https://developer.android.com/develop/background-work/services/fgs/service-types). Выбор типа и принятие Play нужно проверить отдельно **[открытый вопрос]**.
3. Сработал признак лёгкого сна/движения -> отменить будильник `T_deadline` и позвонить сейчас; иначе звонок в `T_deadline`.
4. Опционально усилить признаком из Health Connect (ЧСС/ВСР), только если данные свежие (раздел 1.2: задержка неизвестна, лимиты чтения). Чтение раз в K минут (K >= 5) из foreground-сервиса при выданном `READ_HEALTH_DATA_IN_BACKGROUND`; через changelog, не повторным raw-read (rate-limiting).
5. Google Sleep API как опциональная подстраховка (грубо, раз в ~10 минут); можно не делать в v1.
6. Если данных нет совсем - звонить в `T_deadline` (то же поведение у Sleep as Android и Google Smart Wake).

### 9.2 Разрешения при первом включении опции (мастер разрешений)

| Что | Нужно ли в v1 | Примечание |
|---|---|---|
| `POST_NOTIFICATIONS` (Android 13+) | да, для foreground-сервиса и будильника | |
| точные будильники (`USE_EXACT_ALARM`) | уже есть | |
| `ACTIVITY_RECOGNITION` | только если выбран тип `health` или Sleep API | runtime, Android 10+ |
| `READ_HEART_RATE`, `READ_HEART_RATE_VARIABILITY`, `READ_HEALTH_DATA_IN_BACKGROUND` | только если делаем усиление через HC | каждое разрешение увеличивает риск отказа в Play и объём политики конфиденциальности; «запрос оптом» - типичная причина отказа |
| `READ_SLEEP` | не нужен для реального времени | пригодится позже для статистики прошлых ночей |
| игнор оптимизации батареи | желательно | у Sleep as Android это обязательная рекомендация для часов |

Если HC не используется, Play-декларация Health Connect и политика health-данных не нужны вообще - это существенное упрощение v1.

### 9.3 Что требует отдельного приложения на часах (версия 2)

- **Wear OS**: приложение на часах с `PassiveMonitoringClient` (пульс, `ASLEEP`) и акселерометром, решение принимается на часах, сигнал - вибрацией часов и/или сообщением на телефон через `MessageClient`. Лучше Kotlin-модуль (биндинг Health Services устарел). Для Pixel Watch 2/3/4/5 и Fitbit эту функцию уже даёт нативная «Smart Wake» самих часов: приложение может лишь подсказать включить её. Нужна Play-декларация и для Wear-приложения.
- **Galaxy Watch**: Samsung Health Sensor SDK (сырой пульс/IBI/акселерометр в реальном времени), но только после партнёрской заявки Samsung.
- **Apple Watch**: нативное watchOS-приложение со smart-alarm extended runtime session (Swift), триггер на iPhone не нужен: сигнал подаётся на часах; планирование из iOS-приложения - через связь iPhone-часы (в этом исследовании не разбиралась).
- **Garmin / Mi Band**: по общим хабам (Health Connect) данные придут батчами, не в реальном времени. Garmin Health SDK (Companion SDK: пульс, акселерометр в реальном времени) доступен только корпоративным партнёрам (https://developer.garmin.com/health-sdk/overview/). Сторонние плагины Sleep as Android для Garmin/Mi Band (Notify) существуют, но как они работают, не разбиралось.

### 9.4 Какие источники НЕ дадут фазу в реальном времени (честно)

- `SleepSessionRecord` в Health Connect от любого источника (по документации пишется после завершения сна; у Samsung формируется после пробуждения).
- Samsung Health Data SDK (сон после обработки, синхронизация с задержкой).
- HealthKit `sleepAnalysis` (стадии видны после пробуждения; в фоне при заблокированном телефоне чтение невозможно).
- Google Sleep API: `SleepSegmentEvent` - после пробуждения; `SleepClassifyEvent` - лишь уверенность во сне раз в ~10 минут.
- Garmin / Zepp / Mi Band через Health Connect (батч-синхронизация, задержка неизвестна).
- Health Services на Wear OS - пакетная доставка и только состояние `ASLEEP` без стадий.

### 9.5 iOS

- v1: умного пробуждения на iOS нет. Предложить фиксированное время (будильник на конец окна); на iOS 26+ можно использовать AlarmKit, на более старых - обычный будильник/уведомление, как сейчас в Chrono.
- v2: только через собственное watchOS-приложение (Swift).
- Режим «iPhone всю ночь в фоне с аудио/микрофоном» (так, судя по описаниям, работает Sleep Cycle) - в первоисточниках Apple и Sleep Cycle не подтверждён; риск отклонения в App Review не оценён. **[НЕ ПОДТВЕРЖДЕНО]**

### 9.6 Эксперимент до начала разработки

Перед тем как закладывать Health Connect в архитектуру, замерить задержку на реальных часах (Pixel Watch, Galaxy Watch, Garmin, Amazfit): в тестовом приложении читать `HeartRateRecord` каждые 5 минут ночью и сравнивать время последнего отсчёта с моментом чтения (и с `metadata.lastModifiedTime`, если он есть в используемой версии SDK **[проверить]**). Если стабильная задержка меньше ~5-10 минут - HC можно использовать как усиление; иначе оставить только собственную актиграфию.

---

## 10. Что не нашёл / не подтверждено (сводка)

1. Официальные частота и задержка записи пульса/ВСР в Health Connect у Pixel Watch / Fitbit / Galaxy Watch / Garmin / Zepp в течение ночи.
2. Фактические числа rate limits Health Connect (страница лимитов их не приводит).
3. Особые требования Play к `READ_HEALTH_DATA_IN_BACKGROUND` и допустимость сценария «будильник» как approved use case.
4. Нативная функция Smart alarm у Galaxy Watch; официальная справка Zepp/Mi по Health Connect; полный текст справки Garmin.
5. Официальная документация Microsoft по Wear OS-приложениям на .NET; первоисточник про снятие поддержки watchOS в .NET.
6. Момент записи стадий Apple Watch в HealthKit (в реальном времени или после пробуждения) и максимальная частота background delivery именно для `sleepAnalysis`.
7. Сборка `ConnectClient` 1.1.0.4 под `net11.0-android` (не проверялось).
8. Раскрытые алгоритмы Sleep as Android / Sleep Cycle / Google Smart Wake.
9. Прямое исследование «умный будильник уменьшает сонную инерцию» (найдено только общее исследование Tassi, Muzet о роли стадии при пробуждении).
10. Рекомендации по размещению телефона (на матрасе) для акселерометра: страница Sleep as Android про датчики не загрузилась целиком.

## Список использованных источников (первичные)

- Health Connect: https://developer.android.com/health-and-fitness/health-connect/get-started, .../data-types, .../read-data, .../experiences/sleep, .../experiences/vitals, .../features/sleep-sessions, .../rate-limiting, .../sync-data, https://developer.android.com/reference/androidx/health/connect/client/records/SleepSessionRecord, https://developer.android.com/jetpack/androidx/releases/health-connect
- Play policy: https://support.google.com/googleplay/android-developer/answer/12991134
- Wear OS / Health Services: https://developer.android.com/health-and-fitness/health-services, .../monitor-background, https://developer.android.com/reference/androidx/health/services/client/data/UserActivityState, https://developer.android.com/training/wearables/data/overview, https://developer.android.com/jetpack/androidx/releases/health
- Android platform: https://developer.android.com/develop/background-work/services/alarms, .../fgs/service-types, https://developer.android.com/about/versions/pie/android-9.0-changes-all
- Google Sleep API: https://developers.google.com/android/reference/com/google/android/gms/location/ActivityRecognitionClient, `SleepClassifyEvent`, `SleepSegmentEvent`
- Samsung: https://developer.samsung.com/health/data/overview.html, .../process.html, https://developer.samsung.com/health/sensor/overview.html, .../process.html, https://developer.samsung.com/health/health-connect-faq.html, https://developer.samsung.com/health/blog/en/managing-sleep-data-with-samsung-health-and-health-connect, https://developer.samsung.com/codelab/health/sleep-data.html
- Apple: https://developer.apple.com/documentation/healthkit/hkcategoryvaluesleepanalysis, .../hkhealthstore/enablebackgrounddelivery(for:frequency:withcompletion:), .../protecting-user-privacy, https://developer.apple.com/documentation/watchkit/using-extended-runtime-sessions, .../wkextendedruntimesession/start(at:), https://developer.apple.com/documentation/alarmkit, https://support.apple.com/en-us/108906, https://www.apple.com/health/pdf/Estimating_Sleep_Stages_from_Apple_Watch_Oct_2025.pdf
- Приложения: https://sleep.urbandroid.org/docs/sleep/smart_wake_up.html, https://support.sleepcycle.com/hc/en-us/articles/7858323091356-What-is-the-Smart-Alarm-Clock, https://support.google.com/googlehealth/answer/14226604
- Garmin: https://developer.garmin.com/health-sdk/overview/
- NuGet: https://www.nuget.org/packages/Xamarin.AndroidX.Health.Connect.ConnectClient, https://github.com/shinyorg/health
- Научные: PubMed 1455130, 7939118, 31579900, 34201861, 24293767, 11903855, 12531174, 41811282
