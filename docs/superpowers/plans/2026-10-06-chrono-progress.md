# Chrono — прогресс исполнения плана

План: `docs/superpowers/plans/2026-10-06-chrono-alarm.md`. Ветка реализации `feature/chrono-v1` влита в `main` 2026-10-07 по решению владельца.

## Статус задач

| Волна | Задача | Статус | Коммиты | Ревью |
|---|---|---|---|---|
| 1 | 1. Каркас и ресурсы | готово | 339b49d | чисто |
| 2 | 2. Ядро + тесты | готово | 812a3a8 | 1 раунд (доказательства); мутации M1–M7 перепроверены координатором |
| 2 | 3. Мелодии | готово | f5e3898 | чисто |
| 3 | 4. Android | готово | 6d582d6, ccf9604 | 1 раунд, 3 исправления |
| 3 | 5. iOS | готово | 6d3c26d | чисто |
| 3 | 6. ViewModels и страницы | готово | a877de0, b0d47ed | 1 раунд, 1 исправление |
| 4 | 7. Интеграция | готово | 7189ce9, e7ff6b4 | 1 раунд, 4 исправления; повторное ревью раунда не проводилось, проверено сборкой, тестами и визуально владельцем |
| 5 | 8. Приёмка на эмуляторе | **не выполнена** | — | — |
| 6 | 9. Финальное ревью ветки | **не выполнено** | — | — |

Итог на момент слияния: `dotnet build Chrono.slnx` — 0 предупреждений, 0 ошибок (Android и iOS); `dotnet test` — 21/21.

## Отступления кода от текста плана (исправления по ревью)

План не переписывался; код отличается от него в этих местах:
- `Platforms/Android/AlarmScheduler.cs` — `showIntent` использует requestCode 3 (при 0 совпадал с full-screen intent сервиса).
- `Platforms/Android/AlarmRingService.cs` — wake lock перезахватывается на каждом старте; `soundPlayer.Play` в try/catch с логом.
- `Platforms/Android/SoundPlayer.cs` — `Stop` перехватывает `IllegalStateException` перед `Release`.
- `ViewModels/RingViewModel.cs` — при замене сигнала `scheduler.Cancel(предыдущий)`.
- `Platforms/Android/MainActivity.cs` — `RingLauncher.Request` до `base.OnCreate` (холодный старт без splash).
- `AppShell.xaml.cs`, `MauiProgram.cs` — `AppShell` transient, маршруты в статическом конструкторе.
- `Views/SplashPage.cs` — колокол 72×82, волны 160, подпись по центру со сдвигом 80.
- `Views/AlarmListPage.xaml` — скрыт скроллбар списка.

## Что осталось

1. Приёмка на эмуляторе — сценарии A1–A13 задачи 8 (убитый процесс, перезагрузка, Doze, пояс, удалённый будильник, два в одну минуту, включение прошедшего, локали, дизайн-ревью).
2. Финальное ревью ветки (задача 9).
3. Отложенные мелкие замечания ревью — журнал `.superpowers/sdd/2026-10-06-chrono-alarm/progress.md` (локальный, не в git). Главные:
   - `Directory.Build.props` использует `$(TEMP)` — на macOS пусто, для сборки на Mac нужен `$([System.IO.Path]::GetTempPath())`;
   - `SetAlarmClock` `SecurityException` (API 31–32) не перехвачен;
   - литералы цвета в баннере списка и на SplashPage;
   - ошибки iOS-планирования пишутся через `Debug.WriteLine` (в Release не видны);
   - системная «Назад» на экране сигнала не останавливает звук (проверить в приёмке).
4. Перед публикацией: заменить `ApplicationId` `com.chrono.alarm`; включить time-sensitive в App ID; декларации `USE_EXACT_ALARM` и `USE_FULL_SCREEN_INTENT` в Play Console.
