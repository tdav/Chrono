# Эталоны дизайна Apple для Chrono

Снимки экранов дизайн-системы «Chrono Business - Apple Design» (версия 4 от 2026-10-29) в светлой и тёмной теме, 804 × 1748 (2×). Это эталоны для приёмки по `DESIGN.md`: скриншот эмулятора сравнивается с картинкой того же экрана. Все четыре вида (с повышенным контрастом) и все компоненты — в полном архиве экспорта и в дизайн-системе.

- Дизайн-система: https://claude.ai/artifact/KU2VGZWGkhx5R3RTLhRDtK
- Тест-форма (живая): https://claude.ai/artifact/TMoMDdz3z6AEb9yGRS1WVU; копия для работы без сети — `test-form.html` (открыть в браузере).

Шрифт на снимках — Inter (как на Android); на iPhone тот же макет набирается SF Pro.

## Экраны

| Экран Chrono | `DESIGN.md` | Светлая | Тёмная |
|---|---|---|---|
| Будильники (`AlarmListPage`) | §4.3 | <img src="screens/ScreenAlarmList-light.png" width="180"> | <img src="screens/ScreenAlarmList-dark.png" width="180"> |
| Изменение будильника (`AlarmEditPage`) | §4.1 | <img src="screens/ScreenAlarmEdit-light.png" width="180"> | <img src="screens/ScreenAlarmEdit-dark.png" width="180"> |
| Шторка: оболочка (`BottomSheet`) | §3.1 | <img src="screens/Sheet-light.png" width="180"> | <img src="screens/Sheet-dark.png" width="180"> |
| Время (`TimeWheelSheet`) | §3.2 | <img src="screens/SheetTime-light.png" width="180"> | <img src="screens/SheetTime-dark.png" width="180"> |
| Дата — календарь в форме | §3.3 | <img src="screens/SheetCalendar-light.png" width="180"> | <img src="screens/SheetCalendar-dark.png" width="180"> |
| Повтор (`RepeatSheet`) | §3.4 | <img src="screens/SheetRepeat-light.png" width="180"> | <img src="screens/SheetRepeat-dark.png" width="180"> |
| Иконка (`IconSheet`) | §3.7 | <img src="screens/SheetIcon-light.png" width="180"> | <img src="screens/SheetIcon-dark.png" width="180"> |
| Длительность (`DurationSheet`) | §3.8 | <img src="screens/SheetDuration-light.png" width="180"> | <img src="screens/SheetDuration-dark.png" width="180"> |
| Сигнал (`RingPage`) | §4.4 | <img src="screens/ScreenRing-light.png" width="180"> | <img src="screens/ScreenRing-dark.png" width="180"> |
| Настройки (`SettingsPage`) | §4.5 | <img src="screens/ScreenSettings-light.png" width="180"> | <img src="screens/ScreenSettings-dark.png" width="180"> |
| Надёжность (`ReliabilityPage`) | §4.6 | <img src="screens/ScreenReliability-light.png" width="180"> | <img src="screens/ScreenReliability-dark.png" width="180"> |
| Распорядок (`SchedulePage`) | §4.9 | <img src="screens/ScreenSchedule-light.png" width="180"> | <img src="screens/ScreenSchedule-dark.png" width="180"> |
| Циферблат (`DialPage`) | §4.10 | <img src="screens/ScreenDial-light.png" width="180"> | <img src="screens/ScreenDial-dark.png" width="180"> |

Где эталон расходится с приложением (кнопка «Отложить», строка «Язык», число вариантов повтора и оповещения), прав `DESIGN.md` — таблица «Отличия макета от приложения» в спецификации `docs/superpowers/specs/2026-10-29-apple-design-design.md`.

## Значки: SF Symbols на iOS

Набор Chrono (`src/Chrono/Resources/Images`) остаётся на обеих платформах. Если на iOS понадобится системный вид значков, соответствие такое (в таб-баре — вариант `.fill`, в панели и строках — контурный). SF Symbols лицензированы только для платформ Apple.

| Файл | SF Symbol | Где |
|---|---|---|
| `icon_tab_alarms.svg` | `alarm.fill` / `alarm` | вкладка «Будильники» |
| `icon_tab_schedule.svg` | `calendar` | вкладка «Распорядок» |
| `icon_tab_dial.svg` | `chart.pie.fill` | вкладка «Циферблат» |
| `icon_gear.svg` | `gearshape.fill` | вкладка «Настройки» |
| `icon_back.svg` | `chevron.backward` | «назад» |
| `icon_next.svg`, `icon_chevron.svg` | `chevron.forward` | переход в строке, листание месяца |
| `icon_check.svg` | `checkmark` | ✓ «Готово», выбранная строка |
| `icon_plus.svg` | `plus` | «Новый будильник» |
| `icon_trash.svg` | `trash` | удаление |
| `bell.svg` | — (фирменный колокол) | значок приложения, сигнал |
| `icon_item_bell.svg` | `bell` | будильник |
| `icon_item_yoga.svg` | `figure.yoga` | йога |
| `icon_item_shower.svg` | `shower` | душ |
| `icon_item_coffee.svg` | `cup.and.saucer` | кофе |
| `icon_item_food.svg` | `fork.knife` | еда |
| `icon_item_bike.svg` | `bicycle` | велосипед |
| `icon_item_work.svg` | `briefcase` | работа |
| `icon_item_book.svg` | `book` | чтение |
| `icon_item_pill.svg` | `pills` | лекарство |
| `icon_item_phone.svg` | `iphone` | звонок |
| `icon_item_walk.svg` | `figure.walk` | прогулка |
| `icon_item_sleep.svg` | `moon` | сон |
