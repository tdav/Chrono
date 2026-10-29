# Дизайн Apple (iOS 26) для Chrono — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** перевести Chrono с темы «Шлифованный металл · Кость» на язык Apple iOS 26 по `DESIGN.md`: системные цвета в четырёх видах, SF Pro / Inter с Dynamic Type, сгруппированные списки, шторки iOS, стекло только у панелей.

**Architecture:** цвета, текст и размеры — из `DESIGN.json` через генератор (`tools/gen_design.py` → `ChronoColors.g.cs`, `TextStyles.g.cs`, `Colors.xaml`, `Metrics.xaml`, `Typography.xaml`). `ThemeService` раскладывает цвета текущего вида в ресурсы приложения; разметка берёт их только через `DynamicResource`. Стили ролей — `Styles.xaml`; старые ключи «металла» временно остаются там же уже в новом виде, чтобы экраны переносились по одному. Свои компоненты — в `Controls/` (шапка, шторка, барабан, календарь, сегменты, меню); нативные — тумблер, ползунок, поле, степпер. Основа (задача 1) уже сделана и проверена — коммит «Дизайн Apple: основа темы».

**Tech Stack:** .NET SDK 11.0.100-rc.3, .NET MAUI 11 rc.2 (`net11.0-android;net11.0-ios`), CommunityToolkit.Mvvm 8.4.2, xunit.v3 4.0.1 на Microsoft.Testing.Platform, Android min API 26 / target 37.

**Spec:** `docs/superpowers/specs/2026-10-29-apple-design-design.md` — исполнитель читает спецификацию и `DESIGN.md` вместе с задачей.

**Эталоны:** `docs/design/apple/screens/<Экран>-light.png` и `-dark.png`; живые — дизайн-система и тест-форма (ссылки в спецификации).

**Прогресс:** `docs/superpowers/plans/2026-10-29-apple-design-progress.md` — координатор отмечает задачи и записывает выводы, которые меняют следующие задачи.

## Global Constraints

- Язык: комментарии в коде и сообщения коммитов — на русском; идентификаторы — на английском.
- Приватные поля без `_`, обращение через `this.`: `private readonly ThemeService themes;` → `this.themes`.
- Не создавать private-методы-помощники без необходимости (допустимо, когда у метода ≥ 2 вызывающих и он несёт логику, а не обёртку).
- Индексировать только точные пути файлов: `git add -- <файл> <файл>`; запрещены `git add -A`, `git add .`, папки. Коммит — `git commit -m "..." -- <те же пути>`.
- Не убивать чужие процессы (`taskkill /IM dotnet.exe` и т. п. запрещены).
- Новых NuGet-пакетов не добавлять.
- Сборка Android: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0 ошибок, 0 предупреждений. Полное решение: `dotnet build Chrono.slnx`. Тесты: `dotnet test --solution Chrono.slnx`.
- **Цвет** — только `{DynamicResource Ключ}` из `DESIGN.json` (в C# — `SetDynamicResource` или `Application.Current.Resources`, перечитываемые по `ThemeService.Changed`). Литералы цветов (`#…`, `Colors.*`) и `StaticResource` для цветов в перенесённых экранах запрещены.
- **Текст** — только стили `Text*` и ролей (`RowTitle`, `SectionHeader` …). `FontSize`, `FontFamily`, `FontAttributes` в разметке перенесённых экранов не задаются.
- **Размеры** — из `Metrics.xaml` (`{StaticResource SizeRow}` …) или из таблиц `DESIGN.md`; область нажатия ≥ 44 × 44.
- **Значки** — `design:IconTint.Color="{DynamicResource …}"` по роли (`DESIGN.md` §2.8); у кнопок-символов — `SemanticProperties.Description`.
- Сгенерированные файлы (`*.g.cs`, `Colors.xaml`, `Metrics.xaml`, `Typography.xaml`) руками не правятся: правка — в `DESIGN.json`, затем `python tools/gen_design.py`, затем `DesignTokensTests`.
- Анимации учитывают `ThemeService.ReduceMotion` (растворение вместо пружины, без волн).
- Неделя везде с понедельника. ✕ «Отменить» — слева, ✓ «Готово» — справа; отмена в диалогах — «Отменить».
- Корневой layout страницы с `Entry` — `SafeAreaEdges="Container, Container, Container, All"` (режим окна `Resize` уже включён в `App.xaml`). Перед правкой формы с полем ввода — прочитать `~/.claude/references/fix-user-experience.md`.
- Окружение исполнителя: Windows 11, Git Bash; БД и Docker не нужны; эмулятор задачам **не нужен** — запуск на эмуляторе делает координатор после каждой волны.
- Каждый агент работает в **своём git worktree** (параллельные сборки MAUI в одном дереве блокируют `obj`). Пути worktree — ASCII.

## Review Focus

1. **Смена вида на лету** (после задачи 13): переключение тёмной темы и контраста при открытом приложении перекрашивает все экраны, включая открытую шторку и циферблат; в `src/Chrono/Views` и `Controls` нет `StaticResource` на цвета (проверка `grep`).
2. **Крупный текст**: при `font_scale 2.0` строки растут, ничего не обрезано, значение уходит под название; время в строке не больше 72.
3. **Шторка**: системная «Назад» закрывает шторку или возвращает на шаг; закрытие с правками спрашивает «Не сохранять / Сохранить / Отменить»; ✓ неактивна при ошибке.
4. **Барабан после «флинга»**: значение в полосе = сноска «через …» = сохранённое время; после 59 идёт 00.
5. **Контраст Android 14** (`Контрастность цветов` → высокая): вторичный текст темнеет без перезапуска.

---

## Волны и исполнители

| Волна | Задачи (параллельно) | Модель | Навыки |
|---|---|---|---|
| 0 | 1 основа — проверка готового коммита | координатор | superpowers:verification-before-completion |
| 1 | 2 шапка и навигация · 3 шторка · 4 барабан · 5 календарь · 6 графика | sonnet ×5 | — |
| 2 | 7 шаги шторки · 8 будильники · 9 сигнал и заставка · 10 настройки и «Надёжность» | sonnet ×4 | 7 — прочитать `fix-user-experience.md` |
| 3 | 11 экран будильника · 12 распорядок и циферблат | sonnet ×2 | 11 — прочитать `fix-user-experience.md` |
| 4 | 13 чистка и полная приёмка на эмуляторе | координатор | superpowers:verification-before-completion |
| финал | ревью всей ветки | opus | superpowers:requesting-code-review |

После каждой задачи — ревью задачи (sonnet) по диффу **файлом**; после волны координатор вливает ветки worktree в `feature/apple-design`, собирает решение, гоняет тесты, ставит приложение на эмулятор `chrono_api36` и снимает скриншоты затронутых экранов в светлой и тёмной теме рядом с эталонами `docs/design/apple/screens/`.

---

### Task 1: Основа темы (готова, проверка)

Коммит «Дизайн Apple: основа темы» уже содержит основу. Проверено до передачи: общий код и XAML собраны с MAUI 11 rc.2 (net11.0) — 0/0; платформенный код собран компилятором против `Mono.Android` 37 и `Microsoft.iOS` 27 с анализаторами платформ — 0/0; дымовой тест: стили грузятся, `ThemeService` перекрашивает живые элементы при смене вида; `DesignTokensTests` 14/14, пять мутаций (правка цвета руками, `StaticResource` на несуществующий ключ, опечатка в `DynamicResource`, `DESIGN.json` без генератора, вес стиля руками) ловятся. **Не проверено:** сборка всего приложения и запуск на устройстве — это и есть задача.

**Files (в коммите):**
- Create: `tools/gen_design.py`, `src/Chrono/Design/*` (11 файлов), `src/Chrono/Platforms/Android/Design/*` (4), `src/Chrono/Platforms/iOS/Design/*` (3), `src/Chrono/Resources/Styles/Metrics.xaml`, `Typography.xaml`, `src/Chrono/Resources/Fonts/Inter-*.ttf` (5), `licenses/Inter-OFL.txt`, `tests/Chrono.Tests/DesignTokensTests.cs`, `src/Chrono/Platforms/Android/Resources/values-night/colors.xml`
- Modify: `src/Chrono/Resources/Styles/Colors.xaml`, `Styles.xaml`, `src/Chrono/App.xaml`, `App.xaml.cs`, `AppShell.xaml`, `AppShell.xaml.cs`, `MauiProgram.cs`, `src/Chrono/Platforms/Android/Resources/values/colors.xml`, `tests/Chrono.Tests/Chrono.Tests.csproj`

**Interfaces (Produces):**
- `Chrono.Design.ThemeService` (singleton): `Attach(Application)`, `Refresh()`, `Current : Appearance`, `ReduceMotion`, `ReduceTransparency`, `event Changed`
- `Chrono.Design.Typography.Style` (attached, `TextStyle`), `Chrono.Design.IconTint.Color` (attached, `Color`)
- `MauiAppBuilder.UseChronoDesign()`
- Ресурсы: 60 цветов (`Label`, `SecondaryLabel`, `Tint`, `TintFill` …), `HairlineWidth`, метрики (`Space4`, `RadiusCard`, `SizeRow` …), стили `Text*` и ролей (`DESIGN.md` §3)

- [ ] `git am -3 0002-*.patch` (или коммит уже в ветке) на `feature/apple-design`
- [ ] `dotnet build Chrono.slnx` — 0/0; при предупреждениях в новых файлах — исправить и записать вывод в прогресс
- [ ] `dotnet test --solution Chrono.slnx` — все тесты, включая 14 `DesignTokensTests`
- [ ] Эмулятор: приложение стартует, все экраны открываются (старые экраны уже в новых цветах и шрифте), шторки открываются, нет `XamlParseException` в logcat
- [ ] Записать в прогресс: замечания по старым экранам (обрезки из-за кегля 17 вместо 14 и т. п.) — это вход для задач 8–12

### Task 2: Шапка и навигация

**Files:**
- Create: `src/Chrono/Controls/GlassIconButton.xaml(.cs)`, `src/Chrono/Controls/LargeTitleHeader.xaml(.cs)`, `src/Chrono/Controls/NavBar.xaml(.cs)`
- Modify: `src/Chrono/AppShell.xaml(.cs)` (при необходимости), `src/Chrono/Platforms/Android/MainActivity.cs` (иконки системных панелей по теме)

**Interfaces (Produces):**
- `GlassIconButton`: `Source` (ImageSource), `Text` (капсула вместо круга), `IsProminent`, `Command`, `CommandParameter`, `Description` (→ `SemanticProperties.Description`)
- `LargeTitleHeader`: `Title`, `Subtitle`, `LeadingContent`, `TrailingContent`, `ScrollView` (для сворачивания); `NavBar`: `Title`, `LeadingContent`, `TrailingContent`

- [ ] `GlassIconButton`: `Border Style=GlassButton` (или `GlassButtonProminent`) 44 × 44, `Image` 20–22 с `IconTint` (`Label` / `OnTint`), нажатие — масштаб 1.08 пружиной (без анимации при `ReduceMotion`); текстовый вариант — капсула 44 с `body`, `Label`
- [ ] `LargeTitleHeader` (`DESIGN.md` §3 › Шапка): строка кнопок 44, под ней Large Title (`LargeTitle`) и подстрока (`LargeSubtitle`), поля 16; при прокрутке на высоту Large Title — inline-заголовок `NavTitle` по центру строки кнопок (прозрачность 0→1), Large Title уходит вверх; подложки нет, под строкой кнопок — затухание контента к `SystemGroupedBackground` 24 pt (scroll edge)
- [ ] `NavBar`: 44 + поля, заголовок `NavTitle`, «назад» — `GlassIconButton` с `icon_back`, описание «Назад»
- [ ] `MainActivity`: статус-бар и панель навигации прозрачные; иконки тёмные в светлой теме и светлые в тёмной (`WindowInsetsControllerCompat.AppearanceLightStatusBars`), перечитываются по `ThemeService.Changed`
- [ ] Приёмка: страница-образец (временная, не коммитится) с прокруткой — Large Title сворачивается; в тёмной теме кнопки и заголовки читаются

### Task 3: Шторка (`Controls/BottomSheet`)

**Files:**
- Modify: `src/Chrono/Controls/BottomSheet.xaml(.cs)`
- Modify: `src/Chrono/Views/Sheets/*.xaml` — только обёртка (заголовок, кнопки ✕/✓ вместо «Отмена/Окей»), содержимое — задачи 4, 5, 7

**Interfaces (Produces):**
- `BottomSheet`: `Detent` (`Medium` | `Large`), `AllowedDetents`, `Title`, `IsDirty`, `CanConfirm`, `ShowGrabber`, `Push(View step, string title)`, `Pop()`, события `Confirmed`, `Cancelled`; системная «Назад» — `HandleBack() : bool`

- [ ] Слой: `Scrim` на весь экран (нажатие = ✕), панель по `DESIGN.md` §3.1: средний детент — `GlassFillLarge`, отступ 8, углы 47, тень; большой — `ElevatedGroupedBackground`, верхние углы 38, страница позади масштаб 0.92 и сдвиг 46
- [ ] Граббер 36 × 5 `TertiaryLabel`: касание переключает детенты; свайп вниз (PanGesture) закрывает, вверх — большой детент
- [ ] Панель: `GlassIconButton` ✕ «Отменить» слева, `NavTitle` по центру, `GlassIconButton` ✓ «Готово» (`IsProminent`, `IsEnabled = CanConfirm`) справа; во вложенном шаге — «назад» слева и ничего справа
- [ ] Закрытие при `IsDirty` (✕, свайп, системная «Назад») — `DisplayActionSheet` из ✕: «Не сохранять» / «Сохранить» / «Отменить»
- [ ] Анимации: появление и смена детента — пружина 0.5 с; при `ReduceMotion` — растворение 0.2 с
- [ ] Перевести оболочки `Views/Sheets/*` на новую панель (✓ = прежний «Окей», ✕ = «Отмена»)
- [ ] Приёмка на эмуляторе: «Назад» с открытой шторкой закрывает её; с правками — диалог; детенты переключаются

### Task 4: Барабан времени

**Files:**
- Create: `src/Chrono/Controls/WheelPicker.xaml(.cs)`
- Modify: `src/Chrono/Views/Sheets/TimeWheelSheet.xaml(.cs)`, `src/Chrono/ViewModels/TimeSheetModel.cs` (если нужно)

- [ ] `WheelPicker` (`DESIGN.md` §3.2): высота `SizeWheel` 216, строка `SizeWheelRow` 34; значения `TextTitle2` табличными цифрами; выбранное — `Label`, соседние — масштаб 0.92 / 0.84 и `SecondaryLabel`; затухание сверху и снизу к фону (`FadeDownBrush` / `FadeUpBrush` заменить на кисти из `ThemeService` по цвету фона шторки); полоса выбора `TertiarySystemFill`, радиус 8
- [ ] Бесконечная прокрутка (5 копий значений, перецентровка после остановки), снап по центру, нажатие на число прокручивает к нему, щелчок `tick.wav` и `HapticFeedback` на каждую строку
- [ ] `TimeWheelSheet`: два барабана и «:», под ними кнопки-переключатели «06:30», «07:00», «08:00», «Сейчас» (`ToggleChip`), сноска «Сработает … — через …» (`SectionFooter`)
- [ ] Тест: текст сноски для сегодня / завтра / прошедшего времени (`AlarmSchedule`)
- [ ] Приёмка: флинг → значение в полосе = сноска; 23 → 00 по кругу

### Task 5: Календарь в форме

**Files:**
- Create: `src/Chrono/Controls/InlineCalendar.xaml(.cs)`
- Modify: `src/Chrono/ViewModels/CalendarSheetModel.cs` (переиспользовать сетку месяца `AlarmSchedule.Month`)

- [ ] Чип строки (`DESIGN.md` §3.3): капсула `TertiarySystemFill`, значение `Tint`; раскрытый — `TintFill`; повторное нажатие сворачивает
- [ ] Календарь: месяц `TextHeadline` + шеврон `Tint`, ‹ › 44 × 44 `Tint`; дни недели с понедельника `TextCaption1Emphasized` `TertiaryLabel`; числа `TextTitle3`; сегодня — `Tint` Semibold; выбранный — круг 40 `TintFill` с числом `Tint`; выбранное «сегодня» — круг `Tint`, число `OnTint`; прошедшие — `TertiaryLabel`, не нажимаются (кроме «Каждый год»); выходные не выделяются
- [ ] Под календарём — `PlainButton` «Ближайшее» и «Сегодня»; раскрытие и сворачивание — 0.5 с (растворение при `ReduceMotion`)
- [ ] `CalendarSheet` больше не открывается как шторка — его модель обслуживает `InlineCalendar`

### Task 6: Графика

**Files:**
- Modify: `src/Chrono/Resources/AppIcon/appicon.svg`, `appiconfg.svg`, `src/Chrono/Resources/Images/bell.svg`, `src/Chrono/Resources/Splash/splash.svg`, `src/Chrono/Chrono.csproj` (`MauiSplashScreen Color`)
- Modify: значки `icon_*.svg` — только если `IconTint` не перекрашивает (многоцветные заливки)

- [ ] Значок (`DESIGN.md` §4.8): белый колокол на градиенте `#4AA8FF → #0A6CF0`; передний план — колокол
- [ ] `bell.svg` — колокол одним цветом (для `IconTint`); заставка `MauiSplashScreen Color="#0088FF"`, колокол белый
- [ ] Проверить, что все `icon_*.svg` одноцветные (иначе `IconTint` красит их целиком) — иначе привести к одному цвету
- [ ] Приёмка: значок на рабочем столе эмулятора, нативный splash

### Task 7: Шаги шторки — повтор, оповещение, длительность, иконка, звук

**Files:**
- Create: `src/Chrono/Controls/DayToggles.xaml(.cs)`, `src/Chrono/Controls/PopupMenu.xaml(.cs)`
- Modify: `src/Chrono/Views/Sheets/RepeatSheet.xaml(.cs)`, `AlertSheet.xaml(.cs)`, `DurationSheet.xaml(.cs)`, `IconSheet.xaml(.cs)`, `SoundSheet.xaml(.cs)` и их модели в `ViewModels/`

- [ ] Списки выбора (`DESIGN.md` §3.4, 3.6, 3.8): секция, строки `RowTitle`, галочка `icon_check` с `IconTint` `Tint` справа; выбранная строка фон не меняет
- [ ] Повтор: 6 вариантов; «По дням недели» показывает `DayToggles` (круги 40 в 44, `Tint`/`OnTint` и `TertiarySystemFill`/`Label`, `TextFootnoteEmphasized`) и сноску-итог; пустой набор → ошибка, ✓ неактивна
- [ ] Оповещение: 4 варианта
- [ ] Длительность: 6 вариантов + строка «Своё, мин» со `Stepper` (5…720, шаг 5), значение в `RowSubtitle`; тест границ и округления
- [ ] Иконка: превью 88 и сетка по 6 (круги 46 `TertiarySystemFill`, значок 24 `Label`; выбранный — `Tint`, значок `OnTint`, кольцо `Tint` через просвет 2), все 46 `icon_item_*`, `SemanticProperties.Description` из названия
- [ ] Звук: `PopupMenu` от строки «Звук» — стекло `GlassFillLarge`, радиус 26, пункт 44, галочка в ведущей колонке; после выбора мелодия играет один раз, «Без звука» останавливает
- [ ] Все пять — шаги внутри шторки (`BottomSheet.Push`), а не отдельные шторки

### Task 8: Будильники (`AlarmListPage`)

**Files:**
- Modify: `src/Chrono/Views/AlarmListPage.xaml(.cs)`, `src/Chrono/ViewModels/AlarmListViewModel.cs`

- [ ] `LargeTitleHeader`: «Изменить» (капсула) слева, «+» справа, Large Title «Будильники», подстрока `NextInHours`/`NextInMinutes`
- [ ] `Tip` о надёжности (`DESIGN.md` §4.3) вместо баннера; ✕ скрывает до следующей проблемы
- [ ] Строки будильников в одной секции: `TimeRow`, подпись `RowSubtitle` «Метка · повтор», `Switch`; выключенный — `SecondaryLabel`; разделители `RowSeparator`
- [ ] Свайп влево — `SwipeItem` «Удалить» `Destructive` с корзиной (`IconTint` белый); режим «Изменить» — красный круг «−» слева, «Готово» вместо «Изменить»
- [ ] Пусто — `EmptyList` (`TextCallout`, `SecondaryLabel`) по центру
- [ ] Удалить FAB «+» и старые ключи со страницы

### Task 9: Сигнал и заставка

**Files:**
- Modify: `src/Chrono/Views/RingPage.xaml(.cs)`, `src/Chrono/Views/SplashPage.cs`

- [ ] Сигнал (`DESIGN.md` §4.4): `PlainPage`; сверху дата `TextSubheadline` `SecondaryLabel`, `TimeHero`, название `TextTitle3Emphasized`; ниже колокол 48 × 54 `OnTint` на круге `Tint` 112 в поле 176, две волны (кольца 176 и 132, `Tint`, 1.5 pt) — 2.4 с, без анимации при `ReduceMotion`; «Стоп» — `FilledButton` во всю ширину внизу
- [ ] Пример: подпись `TextSubheadline`, пример `TextTitle1` табличными цифрами, ответы 2 × 2 `GrayButton`, ошибка `ErrorText`
- [ ] Заставка (`DESIGN.md` §4.7): `SystemBackground`, колокол на круге `Tint` 96, «Chrono» `LargeTitle`; качание без `ReduceMotion`; убрать `MetalBrush`/`GoldEdgeBrush`

### Task 10: Настройки и «Надёжность»

**Files:**
- Create: `src/Chrono/Controls/StatusIcon.xaml(.cs)`
- Modify: `src/Chrono/Views/SettingsPage.xaml(.cs)`, `src/Chrono/Views/ReliabilityPage.xaml(.cs)`, их модели

- [ ] Настройки (`DESIGN.md` §4.5): `LargeTitleHeader` «Настройки»; секции «Умное пробуждение», «Отключение сигнала» (тумблер вместо флажка), «Google Календарь», «Система» (строка «Надёжность» с красным бейджем-числом и шевроном); значки строк — `StatusIcon` 30 радиус 7 цветом роли; статусы — символ и слова; сноска — версия
- [ ] «Надёжность» (`DESIGN.md` §4.6): `NavBar` с «назад» и «Надёжность», пояснение `TextCallout` `SecondaryLabel`, строки со `StatusIcon` (`Success` ✓ / `Warning` !), подзаголовок словами, `TintedButtonS` «Исправить» только у проблемных, сноска о проверке
- [ ] «Назад» из «Надёжности», открытой из «Настроек», возвращает на вкладку «Настройки»

### Task 11: Экран будильника — модальная шторка

**Files:**
- Modify: `src/Chrono/Views/AlarmEditPage.xaml(.cs)`, `src/Chrono/ViewModels/AlarmEditViewModel.cs`, `src/Chrono/AppShell.xaml.cs` (маршрут `edit` — модальный)

- [ ] Маршрут `edit` — `Shell.PresentationMode="ModalAnimated"`; страница — шторка на большом детенте (`DESIGN.md` §4.1): панель ✕ / «Будильник» или «Изменение» / ✓ (`CanConfirm` = нет ошибок)
- [ ] Сверху `WheelPicker` и сноска; секция «Повтор / Дата / Окончание / Название / Иконка»; секция «Сигнал»: «Оповещение», «Звук» и «Вибрация» (только при «Звонить как будильник»), «Длительность»; сноска «Изменения сохраняются по кнопке ✓.»; ошибки — `ErrorText` под своей секцией
- [ ] Секция «Удалить будильник» (`DestructiveRowAction`) только у существующей записи; подтверждение — `DisplayActionSheet` из строки: «Удалить будильник» / «Отменить»
- [ ] Закрытие с правками (✕, свайп, «Назад») — «Не сохранять» / «Сохранить» / «Отменить»
- [ ] Значения по умолчанию нового будильника — как раньше; поле «Название» + клавиатура не перекрывают ✓
- [ ] Удалить плашку времени и нижние кнопки «Удалить»/«Сохранить»; новые строки — в `AppResources.resx` и `.ru.resx`

### Task 12: Распорядок и циферблат

**Files:**
- Create: `src/Chrono/Controls/SegmentedControl.xaml(.cs)`, `src/Chrono/Controls/DoneToggle.xaml(.cs)`
- Modify: `src/Chrono/Views/SchedulePage.xaml(.cs)`, `src/Chrono/Views/DialPage.xaml(.cs)`, `src/Chrono/Views/DialDrawable.cs`, `src/Chrono/Services/DialLayout.cs`, модели; `tests/Chrono.Tests/DialLayoutTests.cs`

- [ ] Распорядок (`DESIGN.md` §4.9): шапка «Сегодня» / «+»; неделя с понедельника (до 3 точек `TertiaryLabel`), сегодня — `Tint`, выбранный — круг `Label`, выбранное «сегодня» — круг `Tint`; лента — секция: время `TextSubheadline` табличными цифрами, значок 18 белым на круге 32 цвета категории, название, подробности; текущая — `Tint` и полоса 4; `DoneToggle`
- [ ] Циферблат (`DESIGN.md` §4.10): `SegmentedControl` «Сейчас / Вечер»; `DialDrawable` — цвета из ресурсов (`SecondarySystemGroupedBackground`, `OpaqueSeparator`, `SecondaryLabel`, `Cat1`…`Cat6`, `Red`), перерисовка по `ThemeService.Changed`; карточка-секция с `FilledButtonM` «Изменить», `GrayButtonM` «Сделано»/«Снять отметку», `PlainButton` «Снять выбор»
- [ ] `DialLayout`: категория = `Id` mod 6, стабильно; тест
- [ ] «+» — в шапке вместо FAB на обоих экранах

### Task 13: Чистка и полная приёмка на эмуляторе (координатор)

- [ ] Удалить раздел «Старые ключи» из `Styles.xaml`, `legacy` из `DESIGN.json` (генератор → `ChronoColors.Legacy` пустой — убрать его и цикл в `ThemeService` вместе с кистями `CanvasBrush`/`MetalBrush`/`GoldEdgeBrush`/`FadeDownBrush`/`FadeUpBrush`), тест `Legacy_CoversEveryKeyOfOldTheme`
- [ ] Удалить Manrope (`Resources/Fonts/Manrope-*`, регистрацию в `MauiProgram`, `licenses/Manrope-OFL.txt`) и Android-маппинг `Digits`, если он больше не нужен
- [ ] Снять фиксацию темы: убрать `this.UserAppTheme = AppTheme.Light;` из `App`
- [ ] `grep` по `src/Chrono/Views`, `Controls`: нет `StaticResource` на цвета, нет `FontSize=`/`FontFamily=`, нет `#` в цветах
- [ ] Приёмка на `chrono_api36`: все экраны в светлой и тёмной теме, `font_scale` 1.3 и 2.0, `animator_duration_scale 0`, контраст высокий — скриншоты рядом с эталонами в прогресс; пункты Review Focus 1–5
- [ ] Обновить `DESIGN.md`, если реализация где-то отступила от него (раздел «Отступление от макета»)

## Самопроверка плана

- Каждый экран `DESIGN.md` §4 закрыт задачей: 4.1 → 11; 4.2 → 3, 4, 5, 7; 4.3 → 8; 4.4, 4.7 → 9; 4.5, 4.6 → 10; 4.8 → 6; 4.9, 4.10 → 12.
- Компоненты задачи 2–5 нужны волне 2–3 и стоят раньше.
- Решения спецификации (1–7) отражены: 1 → задачи 1 и 13; 2 → 2–5, 7; 3 → основа; 4 → 11; 5 → 7; 6 → 7; 7 → основа.
- Всё, что меняет поведение (длительность степпером, 3 точки недели, 6 категорий циферблата), покрыто тестами или приёмкой.
