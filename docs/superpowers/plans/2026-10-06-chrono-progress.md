# Chrono — прогресс исполнения плана

План: `docs/superpowers/plans/2026-10-06-chrono-alarm.md`. Ветка реализации: `feature/chrono-v1`.

## Статус задач

| Волна | Задача | Модель | Статус | Коммит | Примечания |
|---|---|---|---|---|---|
| 1 | 1. Каркас и ресурсы | sonnet | не начата | | |
| 2 | 2. Ядро + тесты | haiku | не начата | | worktree |
| 2 | 3. Мелодии | haiku | не начата | | worktree |
| 3 | 4. Android | haiku | не начата | | worktree |
| 3 | 5. iOS | haiku | не начата | | worktree |
| 3 | 6. ViewModels и страницы | haiku | не начата | | worktree |
| 4 | 7. Интеграция | sonnet | не начата | | |
| 5 | 8. Приёмка | sonnet | не начата | | A2/A9 — согласие на чужом эмуляторе |
| 6 | 9. Финальное ревью | opus | не начата | | |

## Выводы, меняющие следующие задачи

Выводы спайка 2026-10-06 (уже внесены в план):
- Android не собирается в пути с кириллицей (`APT2265`) → `Directory.Build.props` выносит `obj`/`bin` в `%TEMP%\chrono-build\<хеш>`.
- `UNNotificationInterruptionLevel.TimeSensitive` = 3 = Critical у iOS → только `TimeSensitive2`.
- `DateTime.Today + TimeSpan` даёт `Kind=Local` → JSON со смещением → `AlarmService.Save` приводит к `Unspecified`.
- `Page.Window` в `OnAppearing` у Shell-страницы = null → `Application.Current.Windows`.
- `pm clear` стирает сборки fast deployment → для эмулятора `-p:EmbedAssembliesIntoApk=true`.
- Возврат мутации копированием оставляет старый mtime → пересборка `--no-incremental`.
- Вибрация без атрибутов = `UNKNOWN` → `VibrationAttributesUsageType.Alarm`.
