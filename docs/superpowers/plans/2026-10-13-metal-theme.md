# Тема «Шлифованный металл · Кость» и новый экран будильника — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** перевести Chrono на светлую тему «Шлифованный металл · Кость» и сделать экран будильника по макету: плашка, группы строк, шторки времени (барабан), даты (календарь), повтора и звука.

**Architecture:** токены и стили — в `Colors.xaml`/`Styles.xaml` (старые ключи временно остаются псевдонимами, чтобы экраны собирались по одному). Шторка — `Controls/BottomSheet` (затемнение + панель + слот), содержимое каждой шторки — свой `ContentView` со своей моделью (`TimeSheetModel`, `CalendarSheetModel`, `RepeatSheetModel`, `SoundSheetModel`), которая сообщает о закрытии событием `Done(bool accepted)`. `AlarmEditViewModel` держит сохраняемые значения и четыре модели шторок. Расчёты («Ближайшее», «через …», сетка месяца) — в чистом `Services/AlarmSchedule.cs` с unit-тестами.

**Tech Stack:** .NET SDK 11.0.100-rc.1, .NET MAUI 11 rc.1 (`net11.0-android;net11.0-ios`), CommunityToolkit.Mvvm 8.4.2, xunit.v3 4.0.1 на Microsoft.Testing.Platform, Android min API 26 / target 37.

**Spec:** `docs/superpowers/specs/2026-10-13-metal-theme-design.md` — исполнитель читает спецификацию вместе с задачей.

**Прогресс:** `docs/superpowers/plans/2026-10-13-metal-theme-progress.md` — координатор отмечает задачи и записывает выводы, которые меняют следующие задачи.

## Global Constraints

- Язык: комментарии в коде и сообщения коммитов — на русском; идентификаторы — на английском.
- Приватные поля без `_`, обращение через `this.`: `private readonly ISoundPlayer soundPlayer;` → `this.soundPlayer`.
- Не создавать private-методы-помощники без необходимости (допустимо, когда у метода ≥ 2 вызывающих и он несёт логику, а не обёртку).
- Индексировать только точные пути файлов: `git add -- <файл> <файл>`; запрещены `git add -A`, `git add .`, папки. Коммит — `git commit -m "..." -- <те же пути>`.
- Не убивать чужие процессы (`taskkill /IM dotnet.exe` и т. п. запрещены).
- Новых NuGet-пакетов не добавлять.
- Сборка Android: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0 ошибок, 0 предупреждений. Полное решение: `dotnet build Chrono.slnx`. Тесты: `dotnet test --solution Chrono.slnx`.
- Палитра без красного и зелёного: предупреждения и деструктивные действия — `GoldInk` (`#8A6A32`) с обводкой `Gold` (`#D1B27C`).
- Неделя везде с понедельника (календарь, чипы дней).
- Кнопки в ряду действий прижаты вправо; primary («Окей», «Сохранить») — крайняя справа.
- Корневой layout страницы с `Entry` — `SafeAreaEdges="Container, Container, Container, All"` (режим окна `Resize` уже включён в `App.xaml`). Перед правкой формы с полем ввода — прочитать `~/.claude/references/fix-user-experience.md`.
- Окружение исполнителя: Windows 11, Git Bash; БД и Docker не нужны; эмулятор задачам **не нужен** — запуск на эмуляторе делает координатор после каждой волны.
- Каждый агент работает в **своём git worktree** (параллельные сборки MAUI в одном дереве блокируют `obj`). Пути worktree — ASCII.

## Review Focus

1. **Время раньше текущего при «Ближайшее»** — ожидается завтрашняя дата на плашке и в `alarms.json`; ровно текущая минута — тоже завтра (тест в задаче 1: `ResolveNearest_TimeNotAhead_IsTomorrow`, `ResolveNearest_SecondsAfterMinute_TreatsMinuteAsPassed`).
2. **Отсчёт за секунды до срока** — «через 1 мин», а не «через 0 мин» (тест `Until_PartialMinute_RoundsUp`).
3. **Месяц, начинающийся в воскресенье, и високосный февраль** — 6 пустых ячеек, 29 февраля 2028 есть (тесты `Month_StartingSunday_HasSixLeadingBlanks`, `Month_ContainsEveryDayOfMonth`).
4. **Барабан после быстрого «флинга»** — значение в полосе выбора совпадает с плашкой после «Окей»; нажатие на число прокручивает к нему (задача 5, приёмка на эмуляторе в задаче 12, шаги с `adb shell input swipe`).
5. **Системная «Назад»**: с открытой шторкой закрывает шторку, а не страницу; с несохранёнными правками спрашивает подтверждение; без правок — просто уходит (задача 11, приёмка в задаче 12).

---

## Волны и исполнители

| Волна | Задачи (параллельно) | Модель | Навыки |
|---|---|---|---|
| 1 | 1 `AlarmSchedule` + тесты · 2 основа темы · 3 `DESIGN.md/json` · 4 графика | 1 — haiku (код готов); 2, 3, 4 — sonnet | 1 — superpowers:test-driven-development |
| 2 | 5 `BottomSheet` + барабан · 6 календарь · 7 повтор + звук · 8 список · 9 сигнал + заставка | sonnet ×5 | — |
| 3 | 10 настройки + «Надёжность» · 11 экран будильника | sonnet ×2 | 11 — прочитать `fix-user-experience.md` |
| 4 | 12 чистка + полная приёмка на эмуляторе (координатор) | координатор | superpowers:verification-before-completion |
| финал | ревью всей ветки | opus | superpowers:requesting-code-review |

После каждой задачи — ревью задачи (sonnet) по диффу **файлом**; после волны координатор вливает ветки worktree в `feature/metal-theme`, собирает решение, гоняет тесты, ставит приложение на эмулятор `chrono_api36` (`emulator-5570`) и снимает скриншоты затронутых экранов.

---

### Task 1: `AlarmSchedule` — «Ближайшее», обратный отсчёт, сетка месяца

Код и тесты ниже прогнаны (17/17) и проверены мутациями: `Ceiling→Floor`, `>`→`>=`, неделя с воскресенья, игнор `allowPast` — каждая мутация роняет тесты.

**Files:**
- Create: `src/Chrono/Services/AlarmSchedule.cs`
- Create: `tests/Chrono.Tests/AlarmScheduleTests.cs`
- Modify: `tests/Chrono.Tests/Chrono.Tests.csproj` (ссылка на файл)

**Interfaces:**
- Produces:
  - `public readonly record struct CalendarCell(DateOnly? Date, bool IsPast, bool IsToday, bool IsWeekend, bool IsEnabled)` (namespace `Chrono.Services`)
  - `public static DateTime AlarmSchedule.ResolveNearest(TimeOnly time, DateTime now)`
  - `public static (int Days, int Hours, int Minutes)? AlarmSchedule.Until(DateTime target, DateTime now)`
  - `public static IReadOnlyList<CalendarCell> AlarmSchedule.Month(int year, int month, DateOnly today, bool allowPast)`

- [ ] **Step 1: Подключить файл в тестовый проект.** В `tests/Chrono.Tests/Chrono.Tests.csproj` в группу ссылок добавить строку после `SleepPhaseEstimator.cs`:

```xml
    <Compile Include="..\..\src\Chrono\Services\AlarmSchedule.cs" Link="Linked\AlarmSchedule.cs" />
```

- [ ] **Step 2: Написать тесты** `tests/Chrono.Tests/AlarmScheduleTests.cs`:

```csharp
using Chrono.Services;

namespace Chrono.Tests;

public class AlarmScheduleTests
{
    [Fact]
    public void ResolveNearest_TimeAhead_IsToday()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal(new DateTime(2026, 10, 16, 21, 0, 0), AlarmSchedule.ResolveNearest(new TimeOnly(21, 0), now));
    }

    [Theory]
    [InlineData(19, 46)] // ровно сейчас — уже не «сегодня»
    [InlineData(6, 30)]
    public void ResolveNearest_TimeNotAhead_IsTomorrow(int hour, int minute)
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal(new DateTime(2026, 10, 17, hour, minute, 0), AlarmSchedule.ResolveNearest(new TimeOnly(hour, minute), now));
    }

    [Fact]
    public void ResolveNearest_SecondsAfterMinute_TreatsMinuteAsPassed()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 30);

        Assert.Equal(new DateTime(2026, 10, 17, 19, 46, 0), AlarmSchedule.ResolveNearest(new TimeOnly(19, 46), now));
    }

    [Fact]
    public void ResolveNearest_CrossesYearEnd()
    {
        var now = new DateTime(2026, 12, 31, 23, 50, 0);

        Assert.Equal(new DateTime(2027, 1, 1, 7, 0, 0), AlarmSchedule.ResolveNearest(new TimeOnly(7, 0), now));
    }

    [Fact]
    public void Until_HoursAndMinutes()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal((0, 20, 10), AlarmSchedule.Until(new DateTime(2026, 10, 17, 15, 56, 0), now));
    }

    [Fact]
    public void Until_DaysHoursMinutes()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Equal((2, 3, 4), AlarmSchedule.Until(new DateTime(2026, 10, 18, 22, 50, 0), now));
    }

    [Fact]
    public void Until_PartialMinute_RoundsUp()
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 40);

        Assert.Equal((0, 0, 1), AlarmSchedule.Until(new DateTime(2026, 10, 16, 19, 47, 0), now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    public void Until_NowOrPast_IsNull(int secondsFromNow)
    {
        var now = new DateTime(2026, 10, 16, 19, 46, 0);

        Assert.Null(AlarmSchedule.Until(now.AddSeconds(secondsFromNow), now));
    }

    [Fact]
    public void Month_StartingSunday_HasSixLeadingBlanks()
    {
        // 1 ноября 2026 — воскресенье.
        var cells = AlarmSchedule.Month(2026, 11, new DateOnly(2026, 10, 16), allowPast: false);

        Assert.Equal(6, cells.TakeWhile(c => c.Date is null).Count());
        Assert.Equal(new DateOnly(2026, 11, 1), cells[6].Date);
        Assert.Equal(36, cells.Count);
    }

    [Fact]
    public void Month_StartingMonday_HasNoLeadingBlanks()
    {
        // 1 июня 2026 — понедельник.
        var cells = AlarmSchedule.Month(2026, 6, new DateOnly(2026, 1, 1), allowPast: false);

        Assert.Equal(new DateOnly(2026, 6, 1), cells[0].Date);
        Assert.Equal(30, cells.Count);
    }

    [Theory]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 2, 28)]
    [InlineData(2026, 10, 31)]
    public void Month_ContainsEveryDayOfMonth(int year, int month, int days)
    {
        var cells = AlarmSchedule.Month(year, month, new DateOnly(2026, 1, 1), allowPast: false);

        Assert.Equal(Enumerable.Range(1, days), cells.Where(c => c.Date is not null).Select(c => c.Date!.Value.Day));
    }

    [Fact]
    public void Month_FlagsPastTodayAndWeekend()
    {
        var today = new DateOnly(2026, 10, 16);

        var cells = AlarmSchedule.Month(2026, 10, today, allowPast: false).Where(c => c.Date is not null).ToDictionary(c => c.Date!.Value.Day);

        Assert.True(cells[15].IsPast);
        Assert.False(cells[15].IsEnabled);
        Assert.True(cells[16].IsToday);
        Assert.False(cells[16].IsPast);
        Assert.True(cells[16].IsEnabled);
        Assert.True(cells[17].IsWeekend); // суббота
        Assert.True(cells[18].IsWeekend); // воскресенье
        Assert.False(cells[19].IsWeekend);
    }

    [Fact]
    public void Month_AllowPast_EnablesPastDays()
    {
        var cells = AlarmSchedule.Month(2026, 10, new DateOnly(2026, 10, 16), allowPast: true);

        var first = cells.First(c => c.Date is not null);
        Assert.True(first.IsPast);
        Assert.True(first.IsEnabled);
    }
}
```

- [ ] **Step 3: Убедиться, что тесты не собираются.** Run: `dotnet test --solution Chrono.slnx`. Expected: ошибка сборки `CS0103: The name 'AlarmSchedule' does not exist` (файла ещё нет — ссылка в csproj указывает на несуществующий файл, ожидаемо `CS2001` или `CS0103`).

- [ ] **Step 4: Реализация** `src/Chrono/Services/AlarmSchedule.cs`:

```csharp
namespace Chrono.Services;

/// <summary>Ячейка календаря; Date == null — пустая ячейка перед первым числом (неделя с понедельника).</summary>
public readonly record struct CalendarCell(DateOnly? Date, bool IsPast, bool IsToday, bool IsWeekend, bool IsEnabled);

/// <summary>Расчёты экрана будильника без зависимостей от MAUI: «Ближайшее», обратный отсчёт, сетка месяца.</summary>
public static class AlarmSchedule
{
    /// <summary>Ближайший момент с этим временем суток: сегодня, если он строго позже now, иначе завтра.</summary>
    public static DateTime ResolveNearest(TimeOnly time, DateTime now)
    {
        var today = DateOnly.FromDateTime(now).ToDateTime(time);
        return today > now ? today : today.AddDays(1);
    }

    /// <summary>
    /// Сколько осталось до target, с округлением минут вверх: за 20 секунд до срока — «1 мин», а не «0 мин».
    /// null, если target уже наступил.
    /// </summary>
    public static (int Days, int Hours, int Minutes)? Until(DateTime target, DateTime now)
    {
        if (target <= now)
        {
            return null;
        }

        var total = (int)Math.Ceiling((target - now).TotalMinutes);
        return (total / 1440, total % 1440 / 60, total % 60);
    }

    /// <summary>Сетка месяца с понедельника: пустые ячейки до 1-го числа, затем все дни месяца.</summary>
    public static IReadOnlyList<CalendarCell> Month(int year, int month, DateOnly today, bool allowPast)
    {
        var first = new DateOnly(year, month, 1);
        var lead = ((int)first.DayOfWeek + 6) % 7;
        var cells = new List<CalendarCell>(lead + 31);
        for (var i = 0; i < lead; i++)
        {
            cells.Add(new CalendarCell(null, false, false, false, false));
        }

        for (var day = first; day.Month == month; day = day.AddDays(1))
        {
            var past = day < today;
            cells.Add(new CalendarCell(
                day,
                past,
                day == today,
                day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                allowPast || !past));
        }

        return cells;
    }
}
```

- [ ] **Step 5: Тесты зелёные.** Run: `dotnet test --solution Chrono.slnx`. Expected: `total: 57, failed: 0` (40 прежних + 17 новых).

- [ ] **Step 6: Мутация.** Временно заменить `Math.Ceiling` на `Math.Floor`, запустить тесты — ожидается `failed: 1` (`Until_PartialMinute_RoundsUp`). Вернуть, убедиться `git diff -- src/Chrono/Services/AlarmSchedule.cs` пуст относительно шага 4 и тесты снова `failed: 0`.

- [ ] **Step 7: Сборка приложения** (файл попадает и в MAUI-проект): `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0 ошибок, 0 предупреждений.

- [ ] **Step 8: Commit**

```bash
git add -- src/Chrono/Services/AlarmSchedule.cs tests/Chrono.Tests/AlarmScheduleTests.cs tests/Chrono.Tests/Chrono.Tests.csproj
git commit -m "AlarmSchedule: «Ближайшее», обратный отсчёт и сетка месяца с тестами" -- src/Chrono/Services/AlarmSchedule.cs tests/Chrono.Tests/AlarmScheduleTests.cs tests/Chrono.Tests/Chrono.Tests.csproj
```

---

### Task 2: Основа темы — токены, стили, шрифты, строки, системные панели

Все экраны продолжают собираться: старые ключи (`Accent`, `AccentBrush`, `OnAccent`, `SurfaceBorder`, `Danger`, `SwitchOff`, `PageBackground`, `BgDeep`, `BgMid`, `BgTop`, `AccentEnd`) остаются **псевдонимами** на новые значения до задачи 12. Все новые строки ресурсов для всех задач добавляются здесь, чтобы параллельные задачи не правили `.resx` одновременно.

**Files:**
- Modify: `src/Chrono/Resources/Styles/Colors.xaml` (полная замена)
- Modify: `src/Chrono/Resources/Styles/Styles.xaml` (полная замена)
- Create: `src/Chrono/Resources/Fonts/Manrope-Medium.ttf`, `Manrope-Bold.ttf`, `Manrope-ExtraBold.ttf`
- Modify: `src/Chrono/MauiProgram.cs` (регистрация шрифтов)
- Modify: `src/Chrono/Platforms/Android/MainActivity.cs` (светлые системные панели)
- Modify: `src/Chrono/Platforms/Android/Resources/values/colors.xml`
- Modify: `src/Chrono/Resources/Strings/AppResources.resx`, `AppResources.ru.resx`

**Interfaces:**
- Produces — ключи ресурсов (используют задачи 5–11):
  - цвета: `Surface`, `Surface2`, `Hairline`, `Divider`, `Metal900`, `Metal700`, `Metal500`, `Text`, `TextMuted`, `OnMetal`, `Gold`, `GoldInk`, `Scrim`, `CanvasFlat`;
  - кисти: `CanvasBrush`, `MetalBrush`, `GoldEdgeBrush`, `FadeDownBrush`, `FadeUpBrush`;
  - стили: `ScreenTitle`, `Eyebrow`, `Footnote`, `Muted`, `SectionHeader`, `Card`, `RowGroup`, `Row`, `RowKey`, `RowValue`, `RowDivider`, `IconWell`, `PrimaryButton`, `SecondaryButton`, `AccentButton`, `CompactAccentButton`, `Chip`, `ChipText`, `SheetTitle`, `SheetHint`;
  - шрифты: `ManropeLight`, `ManropeRegular`, `ManropeMedium`, `ManropeSemiBold`, `ManropeBold`, `ManropeExtraBold`;
  - строки `AppResources`: см. шаг 6.

- [ ] **Step 1: Шрифты.** Статических Medium/Bold/ExtraBold в google/fonts нет — только вариативный `ofl/manrope/Manrope[wght].ttf`. Рецепт проверен (`fontTools` 4.63 установлен):

```bash
cd "$TMP" && curl -sL -o manrope-var.ttf "https://github.com/google/fonts/raw/main/ofl/manrope/Manrope%5Bwght%5D.ttf"
for w in 500:Medium 700:Bold 800:ExtraBold; do python -m fontTools.varLib.instancer manrope-var.ttf wght=${w%%:*} --static --update-name-table -o Manrope-${w##*:}.ttf; done
```

Критерий пригодности: `python -c "from fontTools.ttLib import TTFont as T; [print(f, T(f)['OS/2'].usWeightClass, T(f)['name'].getDebugName(6)) for f in ['Manrope-Medium.ttf','Manrope-Bold.ttf','Manrope-ExtraBold.ttf']]"` → `500 Manrope-Medium`, `700 Manrope-Bold`, `800 Manrope-ExtraBold`. Скопировать три файла в `src/Chrono/Resources/Fonts/`. Лицензия OFL уже лежит в `licenses/Manrope-OFL.txt`.

- [ ] **Step 2: Регистрация шрифтов** в `src/Chrono/MauiProgram.cs` рядом с существующими `fonts.AddFont(...)`:

```csharp
                fonts.AddFont("Manrope-Medium.ttf", "ManropeMedium");
                fonts.AddFont("Manrope-Bold.ttf", "ManropeBold");
                fonts.AddFont("Manrope-ExtraBold.ttf", "ManropeExtraBold");
```

- [ ] **Step 3: `Colors.xaml`** — полная замена:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<!-- Тема «Шлифованный металл · Кость» (DESIGN.md). Приложение всегда светлое. -->
<ResourceDictionary>

    <Color x:Key="CanvasFlat">#F5F2EC</Color>
    <Color x:Key="Surface">#FFFFFF</Color>
    <Color x:Key="Surface2">#F5F2EC</Color>
    <Color x:Key="Hairline">#B8B0A6</Color>
    <Color x:Key="Divider">#E4DED3</Color>
    <Color x:Key="Metal900">#26292D</Color>
    <Color x:Key="Metal700">#2E3136</Color>
    <Color x:Key="Metal500">#3A3E44</Color>
    <Color x:Key="Text">#2E3136</Color>
    <Color x:Key="TextMuted">#6B7078</Color>
    <Color x:Key="OnMetal">#F5F2EC</Color>
    <Color x:Key="Gold">#D1B27C</Color>
    <Color x:Key="GoldInk">#8A6A32</Color>
    <!-- rgba(46,49,54,.42): .42 × 255 = 0x6B. -->
    <Color x:Key="Scrim">#6B2E3136</Color>

    <LinearGradientBrush x:Key="CanvasBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#FFFFFF" Offset="0" />
        <GradientStop Color="#EDE9E1" Offset="1" />
    </LinearGradientBrush>

    <LinearGradientBrush x:Key="MetalBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#3A3E44" Offset="0" />
        <GradientStop Color="#2E3136" Offset="0.55" />
        <GradientStop Color="#26292D" Offset="1" />
    </LinearGradientBrush>

    <LinearGradientBrush x:Key="GoldEdgeBrush" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#E3C88F" Offset="0" />
        <GradientStop Color="#B8975A" Offset="1" />
    </LinearGradientBrush>

    <!-- Затухание барабана: белый к прозрачному сверху и снизу. -->
    <LinearGradientBrush x:Key="FadeDownBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#FFFFFFFF" Offset="0" />
        <GradientStop Color="#00FFFFFF" Offset="1" />
    </LinearGradientBrush>

    <LinearGradientBrush x:Key="FadeUpBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#00FFFFFF" Offset="0" />
        <GradientStop Color="#FFFFFFFF" Offset="1" />
    </LinearGradientBrush>

    <!-- Псевдонимы старой темы «Ночное небо»: экраны переводятся на новые ключи по одному; удаляются в задаче 12. -->
    <Color x:Key="BgDeep">#F5F2EC</Color>
    <Color x:Key="BgMid">#F5F2EC</Color>
    <Color x:Key="BgTop">#FFFFFF</Color>
    <Color x:Key="SurfaceBorder">#B8B0A6</Color>
    <Color x:Key="Accent">#8A6A32</Color>
    <Color x:Key="AccentEnd">#B8975A</Color>
    <Color x:Key="OnAccent">#F5F2EC</Color>
    <Color x:Key="Danger">#8A6A32</Color>
    <Color x:Key="SwitchOff">#E4DED3</Color>

    <LinearGradientBrush x:Key="PageBackground" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#FFFFFF" Offset="0" />
        <GradientStop Color="#EDE9E1" Offset="1" />
    </LinearGradientBrush>

    <LinearGradientBrush x:Key="AccentBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#3A3E44" Offset="0" />
        <GradientStop Color="#2E3136" Offset="0.55" />
        <GradientStop Color="#26292D" Offset="1" />
    </LinearGradientBrush>

</ResourceDictionary>
```

- [ ] **Step 4: `Styles.xaml`** — полная замена. **Набросок, не собирался:** если сеттер не компилируется (например, `Shadow` у `Button`), убрать именно его и записать это в отчёт; ключи стилей менять нельзя — на них ссылаются другие задачи.

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary>

    <Style TargetType="ContentPage" ApplyToDerivedTypes="True">
        <Setter Property="Background" Value="{StaticResource CanvasBrush}" />
        <Setter Property="Shell.NavBarIsVisible" Value="False" />
    </Style>

    <Style TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="FontFamily" Value="ManropeMedium" />
        <Setter Property="FontSize" Value="14" />
    </Style>

    <Style x:Key="ScreenTitle" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeExtraBold" />
        <Setter Property="FontSize" Value="22" />
        <Setter Property="VerticalOptions" Value="Center" />
    </Style>

    <!-- Надпись-«бровь»: 11/600, капсом. -->
    <Style x:Key="Eyebrow" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="11" />
        <Setter Property="CharacterSpacing" Value="1" />
        <Setter Property="TextTransform" Value="Uppercase" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
    </Style>

    <Style x:Key="SectionHeader" TargetType="Label" BasedOn="{StaticResource Eyebrow}">
        <Setter Property="Margin" Value="4,14,0,6" />
    </Style>

    <Style x:Key="Muted" TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="FontSize" Value="12" />
    </Style>

    <Style x:Key="Footnote" TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="FontSize" Value="11.5" />
        <Setter Property="LineHeight" Value="1.5" />
    </Style>

    <Style x:Key="Card" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Surface}" />
        <Setter Property="Stroke" Value="{StaticResource Hairline}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="StrokeShape" Value="RoundRectangle 20" />
        <Setter Property="Padding" Value="16,12" />
    </Style>

    <!-- Группа строк: карточка без внутренних отступов; строки 54 pt разделены RowDivider. -->
    <Style x:Key="RowGroup" TargetType="Border" BasedOn="{StaticResource Card}">
        <Setter Property="Padding" Value="0" />
    </Style>

    <Style x:Key="Row" TargetType="Grid">
        <Setter Property="HeightRequest" Value="54" />
        <Setter Property="Padding" Value="16,0" />
        <Setter Property="ColumnSpacing" Value="12" />
    </Style>

    <Style x:Key="RowKey" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeMedium" />
        <Setter Property="FontSize" Value="13.5" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="VerticalOptions" Value="Center" />
    </Style>

    <Style x:Key="RowValue" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="14.5" />
        <Setter Property="VerticalOptions" Value="Center" />
    </Style>

    <Style x:Key="RowDivider" TargetType="BoxView">
        <Setter Property="HeightRequest" Value="1" />
        <Setter Property="Color" Value="{StaticResource Divider}" />
        <Setter Property="Margin" Value="16,0" />
    </Style>

    <!-- Колодец 42×42 для иконок: назад, настройки, листание месяца. -->
    <Style x:Key="IconWell" TargetType="Border">
        <Setter Property="WidthRequest" Value="42" />
        <Setter Property="HeightRequest" Value="42" />
        <Setter Property="Background" Value="{StaticResource Surface2}" />
        <Setter Property="Stroke" Value="{StaticResource Hairline}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="StrokeShape" Value="RoundRectangle 15" />
    </Style>

    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource MetalBrush}" />
        <Setter Property="TextColor" Value="{StaticResource OnMetal}" />
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="CornerRadius" Value="18" />
        <Setter Property="HeightRequest" Value="54" />
        <Setter Property="Padding" Value="22,0" />
        <Setter Property="Shadow">
            <Shadow Brush="#2E3136" Offset="0,10" Radius="22" Opacity="0.28" />
        </Setter>
    </Style>

    <Style x:Key="SecondaryButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource CanvasBrush}" />
        <Setter Property="BorderColor" Value="{StaticResource Hairline}" />
        <Setter Property="BorderWidth" Value="1" />
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="CornerRadius" Value="18" />
        <Setter Property="HeightRequest" Value="54" />
        <Setter Property="Padding" Value="22,0" />
    </Style>

    <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource SecondaryButton}">
        <Setter Property="BorderColor" Value="{StaticResource Gold}" />
        <Setter Property="TextColor" Value="{StaticResource GoldInk}" />
    </Style>

    <Style x:Key="CompactAccentButton" TargetType="Button" BasedOn="{StaticResource AccentButton}">
        <Setter Property="HeightRequest" Value="38" />
        <Setter Property="CornerRadius" Value="13" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Padding" Value="14,0" />
    </Style>

    <!-- Старая ссылка «Удалить»; убирается в задаче 12. -->
    <Style x:Key="DangerLink" TargetType="Button">
        <Setter Property="BackgroundColor" Value="Transparent" />
        <Setter Property="TextColor" Value="{StaticResource GoldInk}" />
        <Setter Property="FontFamily" Value="ManropeMedium" />
        <Setter Property="FontSize" Value="14" />
    </Style>

    <Style x:Key="Chip" TargetType="Border">
        <Setter Property="HeightRequest" Value="40" />
        <Setter Property="Padding" Value="14,0" />
        <Setter Property="Background" Value="{StaticResource Surface2}" />
        <Setter Property="Stroke" Value="{StaticResource Hairline}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="StrokeShape" Value="RoundRectangle 20" />
    </Style>

    <Style x:Key="ChipText" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="12.5" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="HorizontalOptions" Value="Center" />
        <Setter Property="VerticalOptions" Value="Center" />
    </Style>

    <Style x:Key="SheetTitle" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeExtraBold" />
        <Setter Property="FontSize" Value="21" />
        <Setter Property="VerticalOptions" Value="Center" />
    </Style>

    <Style x:Key="SheetHint" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="VerticalOptions" Value="Center" />
        <Setter Property="HorizontalOptions" Value="End" />
    </Style>

    <Style TargetType="Switch">
        <Setter Property="OnColor" Value="{StaticResource Metal700}" />
        <Setter Property="ThumbColor" Value="White" />
    </Style>

    <Style TargetType="CheckBox">
        <Setter Property="Color" Value="{StaticResource Text}" />
    </Style>

    <Style TargetType="Entry">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="PlaceholderColor" Value="{StaticResource Hairline}" />
        <Setter Property="FontFamily" Value="ManropeMedium" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="BackgroundColor" Value="Transparent" />
    </Style>

    <!-- Цифровые поля старого редактора; удаляются в задаче 12. -->
    <Style x:Key="DigitBox" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Surface}" />
        <Setter Property="Stroke" Value="{StaticResource Hairline}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="StrokeShape" Value="RoundRectangle 12" />
        <Setter Property="Padding" Value="4,0" />
    </Style>

    <Style x:Key="DigitEntry" TargetType="Entry">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="PlaceholderColor" Value="{StaticResource Hairline}" />
        <Setter Property="FontFamily" Value="ManropeMedium" />
        <Setter Property="BackgroundColor" Value="Transparent" />
        <Setter Property="Keyboard" Value="Numeric" />
        <Setter Property="HorizontalTextAlignment" Value="Center" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 5: Светлые системные панели.** В `src/Chrono/Platforms/Android/MainActivity.cs` в `OnCreate` **сразу после** `base.OnCreate(savedInstanceState);` (строка 33) добавить (API проверено в `Xamarin.AndroidX.Core 1.19.0.1`: `WindowCompat.GetInsetsController`, свойства `AppearanceLightStatusBars`/`AppearanceLightNavigationBars`):

```csharp
        // Светлая тема «Кость»: тёмные значки статус-бара и панели навигации (иначе белые значки пропадают на светлом фоне).
        var insets = AndroidX.Core.View.WindowCompat.GetInsetsController(this.Window!, this.Window!.DecorView);
        insets.AppearanceLightStatusBars = true;
        insets.AppearanceLightNavigationBars = true;
```

В `src/Chrono/Platforms/Android/Resources/values/colors.xml` все три цвета (`colorPrimary`, `colorPrimaryDark`, `colorAccent`) → `#F5F2EC`.

**Как выглядит молчаливый отказ:** сборка проходит, но MAUI при навигации сам переустанавливает вид статус-бара, и значки снова белые. Это ловит только скриншот координатора; в отчёте укажите, что проверка вида — на стороне координатора.

- [ ] **Step 6: Строки ресурсов.** В конец `AppResources.resx` (en) и `AppResources.ru.resx` (ru) перед `</root>` добавить (формат `  <data name="X" xml:space="preserve"><value>…</value></data>`):

| Ключ | en | ru |
|---|---|---|
| `PlateDate` | `Rings · {0}` | `Сработает · {0}` |
| `PlateToday` | `Rings today · {0}` | `Сработает сегодня · {0}` |
| `PlateTomorrow` | `Rings tomorrow · {0}` | `Сработает завтра · {0}` |
| `RelDays` | `in {0} d {1} h` | `через {0} д {1} ч` |
| `RelHours` | `in {0} h {1} min` | `через {0} ч {1} мин` |
| `RelMinutes` | `in {0} min` | `через {0} мин` |
| `TimePassed` | `time has passed` | `время уже прошло` |
| `RowTime` | `Time` | `Время` |
| `RowDate` | `Date` | `Дата` |
| `RowName` | `Name` | `Название` |
| `Nearest` | `Nearest` | `Ближайшее` |
| `NearestHint` | `Nearest suitable` | `Ближайшее подходящее` |
| `NoSound` | `No sound` | `Без звука` |
| `SaveFootnote` | `Changes apply when you tap Save.` | `Изменения применяются по кнопке «Сохранить».` |
| `DiscardTitle` | `Discard changes?` | `Не сохранять изменения?` |
| `DiscardYes` | `Discard` | `Не сохранять` |
| `SheetOk` | `OK` | `Окей` |
| `Clear` | `Clear` | `Очистить` |
| `Today` | `Today` | `Сегодня` |
| `Now` | `Now` | `Сейчас` |
| `Back` | `Back` | `Назад` |

Существующие ключи (`Sound`, `Vibration`, `RepeatHeader`, `Repeat_*`, `Sound_*`, `DefaultLabel`, `Cancel`, `Save`, `Delete`, `NewAlarm`, `EditAlarm`, `TimeInPast`, `PickNewDate`) не трогать.

- [ ] **Step 7: Сборка и тесты.** `dotnet build Chrono.slnx` — 0 ошибок, 0 предупреждений; `dotnet test --solution Chrono.slnx` — `failed: 0`. Затем контрольная проверка ключей: `grep -c '<data name=' src/Chrono/Resources/Strings/AppResources.resx` и то же для `.ru.resx` — числа равны.

- [ ] **Step 8: Commit**

```bash
git add -- src/Chrono/Resources/Styles/Colors.xaml src/Chrono/Resources/Styles/Styles.xaml src/Chrono/Resources/Fonts/Manrope-Medium.ttf src/Chrono/Resources/Fonts/Manrope-Bold.ttf src/Chrono/Resources/Fonts/Manrope-ExtraBold.ttf src/Chrono/MauiProgram.cs src/Chrono/Platforms/Android/MainActivity.cs src/Chrono/Platforms/Android/Resources/values/colors.xml src/Chrono/Resources/Strings/AppResources.resx src/Chrono/Resources/Strings/AppResources.ru.resx
git commit -m "Тема «Шлифованный металл · Кость»: токены, стили, шрифты Manrope 500/700/800, строки, светлые системные панели" -- <те же пути>
```

---

### Task 3: `DESIGN.md` и `DESIGN.json` Chrono

**Files:**
- Create: `DESIGN.md`, `DESIGN.json` (корень репозитория)

**Interfaces:** только документы; код на них не ссылается.

- [ ] **Step 1: `DESIGN.md`.** Структура как у `DESIGN.md` Trianon (заголовок с `Screen set ID: chrono.mobile.metal.v1`, `Version: 1.0.0`, `Platform: Android 8+ / iOS 15+`, `Viewport 390 × 844, safe area 54/34`, `Feel`), разделы:
  1. Цели опыта (поставить будильник за 3 нажатия; видно, когда сработает и через сколько; сигнал читается спросонья; ничего не мешает выключить/включить будильник).
  2. Основы: сетка (4 pt, поля 20, промежуток между блоками 22, между карточками 12), цвет — таблица токенов **ровно из спецификации, раздел 1.1** + градиенты + блики, типографика — таблица из раздела 1.2, радиусы (15/16/18/20/28/999), тени (primary `0 10 22 rgba(46,49,54,.28)`, шторка `0 −10 40 rgba(0,0,0,.25)`), иконки (контурные 24×24, 2–2.4), движение (качание колокола, волны; шторки без анимаций).
  3. Компоненты — из спецификации, раздел 1.3 и 3.1–3.5.
  4. Экраны — разделы 2 и 5 спецификации: список, редактор, четыре шторки, сигнал, настройки, «Надёжность», заставка, иконка.
  5. Правила: палитра без красного/зелёного; один металлический fill на зону; золото только мелкое; кнопки действий вправо, primary крайняя справа.
  Источник: проект claude.ai/design «Trianon OCPP mobile project», тема v1.3.0.

- [ ] **Step 2: `DESIGN.json`** — те же данные машиночитаемо:

```json
{
  "id": "chrono.mobile.metal.v1",
  "version": "1.0.0",
  "source": "Trianon OCPP mobile project · DESIGN.json 1.3.0 · тема «Шлифованный металл · Кость»",
  "viewport": { "width": 390, "height": 844, "safeTop": 54, "safeBottom": 34 },
  "color": {
    "canvas": { "gradient": ["#FFFFFF", "#EDE9E1"], "angle": 180, "flat": "#F5F2EC" },
    "surface": "#FFFFFF", "surface2": "#F5F2EC", "hairline": "#B8B0A6", "divider": "#E4DED3",
    "metal": { "900": "#26292D", "700": "#2E3136", "500": "#3A3E44", "gradient": ["#3A3E44", "#2E3136 55%", "#26292D"] },
    "text": "#2E3136", "textMuted": "#6B7078", "onMetal": "#F5F2EC",
    "gold": "#D1B27C", "goldInk": "#8A6A32", "goldEdge": { "gradient": ["#E3C88F", "#B8975A"], "angle": 135 },
    "scrim": "rgba(46,49,54,.42)"
  },
  "font": { "family": "Manrope", "weights": [400, 500, 600, 700, 800] },
  "type": { "plateTime": [64, 800], "listTime": [38, 800], "ringTime": [72, 800], "appTitle": [28, 800], "screenTitle": [22, 800], "sheetTitle": [21, 800], "rowValue": [14.5, 700], "rowKey": [13.5, 500], "button": [15, 600], "chip": [12.5, 600], "eyebrow": [11, 600], "footnote": [11.5, 500] },
  "radius": { "chip": 999, "iconWell": 15, "wheelBand": 16, "button": 18, "card": 20, "sheet": 28 },
  "size": { "row": 54, "button": 54, "buttonCompact": 38, "iconWell": 42, "fab": 60, "wheelRow": 56, "wheelHeight": 224, "calendarCell": 44 },
  "shadow": { "primary": "0 10 22 rgba(46,49,54,.28)", "sheet": "0 -10 40 rgba(0,0,0,.25)" }
}
```

Проверка: `python -m json.tool DESIGN.json > /dev/null` — без ошибок.

- [ ] **Step 3: Commit**

```bash
git add -- DESIGN.md DESIGN.json
git commit -m "DESIGN.md и DESIGN.json Chrono: тема «Шлифованный металл · Кость»" -- DESIGN.md DESIGN.json
```

---

### Task 4: Графика — иконки, колокол, иконка приложения, нативный splash

**Files:**
- Create: `src/Chrono/Resources/Images/icon_back.svg`, `icon_next.svg`, `icon_chevron.svg`, `icon_gear.svg`, `icon_trash.svg`, `icon_check.svg`, `icon_plus.svg`
- Modify: `src/Chrono/Resources/Images/bell.svg`, `src/Chrono/Resources/AppIcon/appicon.svg`, `appiconfg.svg`, `src/Chrono/Resources/Splash/splash.svg`
- Modify: `src/Chrono/Chrono.csproj` (только атрибут `Color` у `MauiSplashScreen`)

**Interfaces:**
- Produces — изображения (в XAML как `icon_back.png` и т. д.; resizetizer делает PNG из SVG):

| Файл | Рисунок | Цвет обводки |
|---|---|---|
| `icon_back` | шеврон влево `M15 6l-6 6 6 6` | `#2E3136` |
| `icon_next` | шеврон вправо `M9 6l6 6-6 6` | `#2E3136` |
| `icon_chevron` | шеврон вправо `M9 6l6 6-6 6` | `#B8B0A6` |
| `icon_gear` | шестерёнка (окружность r=3 + зубчатый контур) | `#2E3136` |
| `icon_trash` | корзина `M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3` | `#8A6A32` |
| `icon_check` | галочка `M5 12.5l4.5 4.5L19 7.5` | `#2E3136` |
| `icon_plus` | плюс `M12 5v14M5 12h14` | `#F5F2EC` |

Все — `viewBox="0 0 24 24"`, `width="24" height="24"`, `fill="none"`, `stroke-width="2.2"`, `stroke-linecap="round"`, `stroke-linejoin="round"`, цвет записан явно (resizetizer не поддерживает `currentColor`).

- `bell.svg` (70×80, ось качания сверху — прежняя): тело `#F5F2EC`, язычок `#D1B27C`; путь тела и язычка прежние (`M35 6c-12 0-20 9-20 22v14l-7 10h54l-7-10V28C55 15 47 6 35 6z`, `circle cx=35 cy=62 r=6`).
- `appicon.svg` (108×108): сплошной фон `#F5F2EC`.
- `appiconfg.svg` (108×108, безопасная зона — круг 66): металлический круг `cx=54 cy=54 r=30`, заливка линейным градиентом сверху вниз `#3A3E44 → #2E3136 (55%) → #26292D`, обводка 1.5 линейным градиентом 135° `#E3C88F → #B8975A`; внутри колокол `#F5F2EC` (тот же путь, масштаб ≈ 0.5, по центру круга) и язычок `#D1B27C`.
- `splash.svg` — копия `appiconfg.svg` (нативный splash показывает только этот слой).
- `Chrono.csproj`: `<MauiSplashScreen Include="Resources\Splash\splash.svg" Color="#F5F2EC" BaseSize="128,128" />`.

- [ ] **Step 1:** Написать семь `icon_*.svg` по таблице.
- [ ] **Step 2:** Переписать `bell.svg`, `appicon.svg`, `appiconfg.svg`, `splash.svg`; изменить `Color` в `Chrono.csproj`.
- [ ] **Step 3: Проверка рендера SVG** (молчаливый отказ resizetizer — пустая или чёрная PNG): `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0 ошибок/предупреждений; затем найти сгенерированные PNG (`find <каталог obj> -name "icon_trash*.png" -o -name "appiconfg*.png"`; каталог obj вынесен во временную папку, если путь содержит не-ASCII, иначе `src/Chrono/obj/Debug/net11.0-android/resizetizer`) и открыть 2–3 файла инструментом чтения изображений: рисунок виден, цвет верный.
- [ ] **Step 4: Commit**

```bash
git add -- src/Chrono/Resources/Images/icon_back.svg src/Chrono/Resources/Images/icon_next.svg src/Chrono/Resources/Images/icon_chevron.svg src/Chrono/Resources/Images/icon_gear.svg src/Chrono/Resources/Images/icon_trash.svg src/Chrono/Resources/Images/icon_check.svg src/Chrono/Resources/Images/icon_plus.svg src/Chrono/Resources/Images/bell.svg src/Chrono/Resources/AppIcon/appicon.svg src/Chrono/Resources/AppIcon/appiconfg.svg src/Chrono/Resources/Splash/splash.svg src/Chrono/Chrono.csproj
git commit -m "Графика темы «Металл · Кость»: контурные иконки, колокол, иконка приложения, splash" -- <те же пути>
```

---

### Task 5: `BottomSheet` и шторка времени (барабан)

**Files:**
- Create: `src/Chrono/Controls/BottomSheet.xaml`, `src/Chrono/Controls/BottomSheet.xaml.cs`
- Create: `src/Chrono/ViewModels/TimeSheetModel.cs`
- Create: `src/Chrono/Views/Sheets/TimeWheelSheet.xaml`, `src/Chrono/Views/Sheets/TimeWheelSheet.xaml.cs`

**Interfaces:**
- Consumes: стили и кисти задачи 2 (`Surface`, `Surface2`, `Hairline`, `Scrim`, `SheetTitle`, `SheetHint`, `Chip`, `ChipText`, `PrimaryButton`, `SecondaryButton`, `FadeDownBrush`, `FadeUpBrush`), строки `Cancel`, `SheetOk`, `Now`.
- Produces:
  - `Chrono.Controls.BottomSheet : ContentView`, `[ContentProperty(nameof(SheetContent))]`, bindable: `bool IsOpen`, `string Title`, `string Hint`, `View? SheetContent`, `ICommand? CancelCommand` (нажатие на затемнение).
  - `Chrono.ViewModels.WheelItem : ObservableObject` — `int Value`, `string Label` (`"00"`), `bool IsSelected`.
  - `Chrono.ViewModels.TimeSheetModel : ObservableObject` — `IReadOnlyList<WheelItem> Hours` (0–23), `Minutes` (0–59), `int Hour`, `int Minute`, `TimeOnly Value`, `void Load(TimeOnly time)`, `void RequestScroll()`, `event EventHandler? ScrollRequested`, `event EventHandler<bool>? Done`, команды `QuickCommand(string "HH:mm")`, `NowCommand`, `PickHourCommand(WheelItem)`, `PickMinuteCommand(WheelItem)`, `OkCommand`, `CancelCommand`.
  - `Chrono.Views.Sheets.TimeWheelSheet : ContentView` (`x:DataType="vm:TimeSheetModel"`).

- [ ] **Step 1: `BottomSheet`.** Разметка (**набросок, не собирался**):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Chrono.Controls.BottomSheet"
             x:Name="Root" IsVisible="False">
    <!-- Слой поверх страницы: затемнение (нажатие = «Отмена») и панель снизу. Без анимаций — состояние меняется сразу. -->
    <!-- Явный ContentView.Content обязателен: [ContentProperty(SheetContent)] класса иначе заберёт этот Grid в SheetContent,
         он переедет в Body внутри самого себя — цикл дерева и зависание главного потока (найдено на эмуляторе). -->
    <ContentView.Content>
    <Grid>
        <BoxView Color="{StaticResource Scrim}">
            <BoxView.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding CancelCommand, Source={x:Reference Root}}" />
            </BoxView.GestureRecognizers>
        </BoxView>
        <Border VerticalOptions="End" Background="{StaticResource Surface}" StrokeThickness="0"
                StrokeShape="RoundRectangle 28,28,0,0" Padding="22,10,22,34">
            <Border.Shadow>
                <Shadow Brush="Black" Offset="0,-10" Radius="40" Opacity="0.25" />
            </Border.Shadow>
            <VerticalStackLayout Spacing="16">
                <BoxView WidthRequest="44" HeightRequest="5" CornerRadius="3" Color="{StaticResource Hairline}" HorizontalOptions="Center" />
                <Grid ColumnDefinitions="*,Auto">
                    <Label Text="{Binding Title, Source={x:Reference Root}}" Style="{StaticResource SheetTitle}" />
                    <Label Grid.Column="1" Text="{Binding Hint, Source={x:Reference Root}}" Style="{StaticResource SheetHint}" />
                </Grid>
                <ContentView x:Name="Body" />
            </VerticalStackLayout>
        </Border>
    </Grid>
    </ContentView.Content>
</ContentView>
```

Code-behind: bindable-свойства `IsOpen` (propertyChanged → `IsVisible = (bool)newValue`), `Title`, `Hint`, `SheetContent` (propertyChanged → `this.Body.Content = (View?)newValue`), `CancelCommand`. Тап по панели не должен закрывать шторку — затемнение и панель соседние элементы, панель перекрывает затемнение.

- [ ] **Step 2: `TimeSheetModel`** (**набросок**):

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Число на барабане: выбранное рисуется крупно и тёмным.</summary>
public sealed partial class WheelItem : ObservableObject
{
    public WheelItem(int value)
    {
        this.Value = value;
        this.Label = value.ToString("00", CultureInfo.InvariantCulture);
    }

    public int Value { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Черновик шторки времени: барабаны часов и минут, быстрые чипы. Применяется только по «Окей».</summary>
public sealed partial class TimeSheetModel : ObservableObject
{
    public TimeSheetModel()
    {
        // Начальные 00:00: присваивание значения по умолчанию не вызывает OnHourChanged — отмечаем явно.
        this.Hours[0].IsSelected = true;
        this.Minutes[0].IsSelected = true;
    }

    public IReadOnlyList<WheelItem> Hours { get; } = Enumerable.Range(0, 24).Select(i => new WheelItem(i)).ToArray();

    public IReadOnlyList<WheelItem> Minutes { get; } = Enumerable.Range(0, 60).Select(i => new WheelItem(i)).ToArray();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    public partial int Hour { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    public partial int Minute { get; set; }

    public TimeOnly Value => new(this.Hour, this.Minute);

    /// <summary>Просит представление прокрутить барабаны к Hour/Minute.</summary>
    public event EventHandler? ScrollRequested;

    /// <summary>true — «Окей», false — «Отмена».</summary>
    public event EventHandler<bool>? Done;

    public void Load(TimeOnly time)
    {
        this.Hour = time.Hour;
        this.Minute = time.Minute;
        this.RequestScroll();
    }

    public void RequestScroll() => this.ScrollRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Quick(string hhmm) => this.Load(TimeOnly.ParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture));

    [RelayCommand]
    private void Now() => this.Load(TimeOnly.FromDateTime(DateTime.Now));

    [RelayCommand]
    private void PickHour(WheelItem item)
    {
        this.Hour = item.Value;
        this.RequestScroll();
    }

    [RelayCommand]
    private void PickMinute(WheelItem item)
    {
        this.Minute = item.Value;
        this.RequestScroll();
    }

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnHourChanged(int value)
    {
        foreach (var item in this.Hours)
        {
            item.IsSelected = item.Value == value;
        }
    }

    partial void OnMinuteChanged(int value)
    {
        foreach (var item in this.Minutes)
        {
            item.IsSelected = item.Value == value;
        }
    }
}
```

`TimeOnly.FromDateTime(DateTime.Now)` содержит секунды — `Load` берёт только `Hour`/`Minute`, это корректно.

- [ ] **Step 3: `TimeWheelSheet.xaml`** (**набросок**). Сетка высотой 224 с колонками `*,110,Auto,110,*`:
  - полоса выбора: `Border` во всю ширину (`Grid.ColumnSpan="5"`), `HeightRequest="56"`, `VerticalOptions="Center"`, `Background="{StaticResource Surface2}"`, `Stroke="{StaticResource Hairline}"`, `StrokeShape="RoundRectangle 16"`, `InputTransparent="True"`;
  - колонки 1 и 3 — `CollectionView` `x:Name="HourWheel"` / `MinuteWheel`, `ItemsSource="{Binding Hours}"`/`Minutes`, `SelectionMode="None"`, `VerticalScrollBarVisibility="Never"`, `Scrolled="OnHourScrolled"`/`OnMinuteScrolled`; `ItemsLayout` — `<LinearItemsLayout Orientation="Vertical" SnapPointsType="MandatorySingle" SnapPointsAlignment="Center" />`; `Header` и `Footer` — `BoxView HeightRequest="84" Color="Transparent"` (чтобы крайние числа вставали в центр); шаблон — `Grid HeightRequest="56"` с `Label Text="{Binding Label}"` по центру: обычный `ManropeBold` 26 `Hairline`, `DataTrigger IsSelected=True` → `ManropeExtraBold` 40 `Text`; `TapGestureRecognizer` → `PickHourCommand`/`PickMinuteCommand` модели (`Source={RelativeSource AncestorType={x:Type vm:TimeSheetModel}}`), параметр — элемент;
  - колонка 2 — `Label Text=":"` `ManropeExtraBold` 32, по центру;
  - затухание: два `BoxView` во всю ширину высотой 84 (`VerticalOptions="Start"` с `FadeDownBrush`, `VerticalOptions="End"` с `FadeUpBrush`), `InputTransparent="True"`, поверх списков.
  Ниже — ряд из 4 чипов (`Grid` `*,*,*,*`, `ColumnSpacing="8"`): «06:30», «07:00», «08:00» → `QuickCommand` с параметром-строкой, «Сейчас» (`{x:Static res:AppResources.Now}`) → `NowCommand`. Ниже — `Grid` `*,1.5*`, `ColumnSpacing="10"`: `SecondaryButton` «Отмена» → `CancelCommand`, `PrimaryButton` «Окей» (`SheetOk`) → `OkCommand`.

- [ ] **Step 4: `TimeWheelSheet.xaml.cs`** (**набросок**):

```csharp
using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

public partial class TimeWheelSheet : ContentView
{
    // Высота строки барабана; заголовок 84 = (224 − 56) / 2, поэтому строка i в центре при смещении i × 56.
    private const double RowHeight = 56;

    private TimeSheetModel? model;

    public TimeWheelSheet()
    {
        this.InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (this.model is not null)
        {
            this.model.ScrollRequested -= this.OnScrollRequested;
        }

        this.model = this.BindingContext as TimeSheetModel;
        if (this.model is not null)
        {
            this.model.ScrollRequested += this.OnScrollRequested;
        }
    }

    private void OnScrollRequested(object? sender, EventArgs e)
    {
        // Шторка только что стала видимой: список ещё не разложен, прокрутка до раскладки игнорируется.
        this.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
        {
            this.HourWheel.ScrollTo(this.model!.Hour, position: ScrollToPosition.Center, animate: false);
            this.MinuteWheel.ScrollTo(this.model.Minute, position: ScrollToPosition.Center, animate: false);
        });
    }

    private void OnHourScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var index = Math.Clamp((int)Math.Round(e.VerticalOffset / RowHeight), 0, 23);
        if (this.model is not null && this.model.Hour != index)
        {
            this.model.Hour = index;
        }
    }

    private void OnMinuteScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var index = Math.Clamp((int)Math.Round(e.VerticalOffset / RowHeight), 0, 59);
        if (this.model is not null && this.model.Minute != index)
        {
            this.model.Minute = index;
        }
    }
}
```

**Как выглядит молчаливый отказ барабана** (проверит координатор на эмуляторе, исполнитель должен знать): (а) после «флинга» в полосе стоит одно число, а `Hour` — соседнее, потому что последнее событие `Scrolled` пришло до окончания прилипания; (б) `ScrollTo` до раскладки молча ничего не делает — барабан открывается на 00. Если `VerticalOffset` на Android включает высоту `Header` иначе, чем предполагается, — индекс смещён на 1–2. Исполнитель не может это проверить без эмулятора: в отчёте перечислить эти три риска как непроверенные.

- [ ] **Step 5: Сборка.** `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0 ошибок, 0 предупреждений (компилированные привязки: у каждого `DataTemplate` и `RelativeSource` указан `x:DataType`, иначе предупреждения XC0022/XC0025).

- [ ] **Step 6: Commit**

```bash
git add -- src/Chrono/Controls/BottomSheet.xaml src/Chrono/Controls/BottomSheet.xaml.cs src/Chrono/ViewModels/TimeSheetModel.cs src/Chrono/Views/Sheets/TimeWheelSheet.xaml src/Chrono/Views/Sheets/TimeWheelSheet.xaml.cs
git commit -m "Шторка и барабан времени: BottomSheet, TimeSheetModel, TimeWheelSheet" -- <те же пути>
```

---

### Task 6: Шторка даты (календарь)

**Files:**
- Create: `src/Chrono/ViewModels/CalendarSheetModel.cs`
- Create: `src/Chrono/Views/Sheets/CalendarSheet.xaml`, `src/Chrono/Views/Sheets/CalendarSheet.xaml.cs`

**Interfaces:**
- Consumes: `AlarmSchedule.Month`, `CalendarCell` (задача 1); стили задачи 2 (`IconWell`, `AccentButton`, `SecondaryButton`, `PrimaryButton`, `Eyebrow`, `MetalBrush`, `Gold`, `GoldInk`, `Hairline`, `OnMetal`, `Text`); изображения `icon_back`, `icon_next` (задача 4); строки `Clear`, `Today`, `SheetOk`, `NearestHint`.
- Produces:
  - `Chrono.ViewModels.CalendarDayItem : ObservableObject` — `CalendarCell Cell`, `string Label` (`""` для пустой), `bool IsSelected`, `bool IsToday`, `bool IsWeekend`, `bool IsDisabled` (= `Cell.Date is not null && !Cell.IsEnabled`).
  - `Chrono.ViewModels.CalendarSheetModel : ObservableObject` — `ObservableCollection<CalendarDayItem> Cells`, `IReadOnlyList<string> WeekDayNames` (7, с понедельника), `DateOnly? Selected`, `string MonthTitle`, `string Hint`, `void Load(DateOnly? selected, DateOnly today, bool allowPast)`, `event EventHandler<bool>? Done`, команды `PrevMonthCommand`, `NextMonthCommand`, `PickCommand(CalendarDayItem)`, `TodayCommand`, `ClearCommand`, `OkCommand`, `CancelCommand`.
  - `Chrono.Views.Sheets.CalendarSheet : ContentView`.

- [ ] **Step 1: Модель** (**набросок**):

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class CalendarDayItem : ObservableObject
{
    public CalendarDayItem(CalendarCell cell)
    {
        this.Cell = cell;
        this.Label = cell.Date?.Day.ToString(CultureInfo.CurrentUICulture) ?? "";
    }

    public CalendarCell Cell { get; }

    public string Label { get; }

    public bool IsToday => this.Cell.IsToday;

    public bool IsWeekend => this.Cell.IsWeekend;

    public bool IsDisabled => this.Cell.Date is not null && !this.Cell.IsEnabled;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Черновик шторки даты. Selected == null — «Ближайшее» (сегодня или завтра по времени).</summary>
public sealed partial class CalendarSheetModel : ObservableObject
{
    private DateOnly today;
    private bool allowPast;
    private int year;
    private int month;

    public CalendarSheetModel()
    {
        // Подписи дней с понедельника: AbbreviatedDayNames начинается с воскресенья.
        var names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
        this.WeekDayNames = Enumerable.Range(1, 7).Select(i => names[i % 7]).ToArray();
    }

    public ObservableCollection<CalendarDayItem> Cells { get; } = [];

    public IReadOnlyList<string> WeekDayNames { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial DateOnly? Selected { get; set; }

    [ObservableProperty]
    public partial string MonthTitle { get; set; } = "";

    public string Hint => this.Selected is { } date
        ? date.ToString("ddd, d MMMM", CultureInfo.CurrentUICulture)
        : AppResources.NearestHint;

    public event EventHandler<bool>? Done;

    public void Load(DateOnly? selected, DateOnly today, bool allowPast)
    {
        this.today = today;
        this.allowPast = allowPast;
        this.Selected = selected;
        var view = selected ?? today;
        this.year = view.Year;
        this.month = view.Month;
        this.Rebuild();
    }

    [RelayCommand]
    private void PrevMonth()
    {
        (this.year, this.month) = this.month == 1 ? (this.year - 1, 12) : (this.year, this.month - 1);
        this.Rebuild();
    }

    [RelayCommand]
    private void NextMonth()
    {
        (this.year, this.month) = this.month == 12 ? (this.year + 1, 1) : (this.year, this.month + 1);
        this.Rebuild();
    }

    [RelayCommand]
    private void Pick(CalendarDayItem item)
    {
        if (item.Cell.Date is null || !item.Cell.IsEnabled)
        {
            return;
        }

        this.Selected = item.Cell.Date;
        foreach (var cell in this.Cells)
        {
            cell.IsSelected = cell.Cell.Date == this.Selected;
        }
    }

    [RelayCommand]
    private void Today()
    {
        this.Selected = this.today;
        this.year = this.today.Year;
        this.month = this.today.Month;
        this.Rebuild();
    }

    [RelayCommand]
    private void Clear()
    {
        this.Selected = null;
        foreach (var cell in this.Cells)
        {
            cell.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    // Четыре вызывающих (Load, листание назад/вперёд, «Сегодня») — общий пересчёт сетки и заголовка.
    private void Rebuild()
    {
        this.Cells.Clear();
        foreach (var cell in AlarmSchedule.Month(this.year, this.month, this.today, this.allowPast))
        {
            this.Cells.Add(new CalendarDayItem(cell) { IsSelected = cell.Date is not null && cell.Date == this.Selected });
        }

        // «MMMM yyyy» без числа — именительный падеж («октябрь 2026»); первая буква заглавная.
        var title = new DateTime(this.year, this.month, 1).ToString("MMMM yyyy", CultureInfo.CurrentUICulture);
        this.MonthTitle = char.ToUpper(title[0], CultureInfo.CurrentUICulture) + title[1..];
    }
}
```

- [ ] **Step 2: Разметка** `CalendarSheet.xaml` (**набросок**), `x:DataType="vm:CalendarSheetModel"`, `VerticalStackLayout Spacing="16"`:
  - ряд навигации `Grid` `Auto,*,Auto`: `Border Style=IconWell` с `Image Source="icon_back.png"` 18×18 и `TapGestureRecognizer` → `PrevMonthCommand`; по центру `Label MonthTitle` `ManropeBold` 16; справа то же с `icon_next.png` → `NextMonthCommand`;
  - заголовки дней: `Grid` 7 колонок одинаковой ширины, высота 28; в колонках `Label Text="{Binding WeekDayNames[0]}"` … `[6]`, `Style=Eyebrow`, по центру; колонки 5 и 6 (Сб, Вс) — `TextColor=GoldInk`;
  - сетка дней: `CollectionView ItemsSource="{Binding Cells}" SelectionMode="None" HeightRequest="264"` (6 строк × 44), `ItemsLayout="VerticalGrid, 7"`; шаблон (`x:DataType="vm:CalendarDayItem"`): `Grid HeightRequest="44"` → `Border` 40×40 `StrokeShape="RoundRectangle 20"` `StrokeThickness="0"` `Background="Transparent"` по центру → `Label Text="{Binding Label}"` `ManropeSemiBold` 14 `Text`. Триггеры **в этом порядке** (последний применимый побеждает): `IsWeekend` → цвет `GoldInk`; `IsDisabled` → цвет `Hairline`; `IsToday` → у `Border` `Stroke=Gold`, `StrokeThickness=1.5`, у `Label` `ManropeExtraBold`; `IsSelected` → у `Border` `Background=MetalBrush`, `StrokeThickness=0`, у `Label` цвет `OnMetal` и `ManropeExtraBold`. `TapGestureRecognizer` → `PickCommand` модели (`RelativeSource AncestorType`), параметр — элемент;
  - кнопки `Grid` `*,*,1.3*` `ColumnSpacing="10"`: `AccentButton` «Очистить» → `ClearCommand`, `SecondaryButton` «Сегодня» → `TodayCommand`, `PrimaryButton` «Окей» → `OkCommand`. «Отмена» — затемнение шторки и «Назад».
- [ ] **Step 3: Code-behind** — только `InitializeComponent()`.
- [ ] **Step 4: Сборка** `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` — 0/0.
- [ ] **Step 5: Commit**

```bash
git add -- src/Chrono/ViewModels/CalendarSheetModel.cs src/Chrono/Views/Sheets/CalendarSheet.xaml src/Chrono/Views/Sheets/CalendarSheet.xaml.cs
git commit -m "Шторка даты: календарь с понедельника, «Очистить / Сегодня / Окей»" -- <те же пути>
```

---

### Task 7: Шторки повтора и звука

**Files:**
- Create: `src/Chrono/ViewModels/ChoiceOption.cs` (класс переносится из `AlarmEditViewModel.cs` без изменений)
- Modify: `src/Chrono/ViewModels/AlarmEditViewModel.cs` (только удалить класс `ChoiceOption`)
- Create: `src/Chrono/ViewModels/RepeatSheetModel.cs`, `src/Chrono/ViewModels/SoundSheetModel.cs`
- Create: `src/Chrono/Views/Sheets/RepeatSheet.xaml(.cs)`, `src/Chrono/Views/Sheets/SoundSheet.xaml(.cs)`

**Interfaces:**
- Consumes: `ChoiceOption(string id, string name)` с `Id`, `Name`, `IsSelected`; `RepeatKind`, `WeekDays`, `Sounds.All`, `ISoundPlayer.Play(string id, bool loop)`, `ISoundPlayer.Stop()`; стили `RowGroup`, `Row`, `RowValue`, `RowDivider`, `Chip`, `ChipText`, `MetalBrush`, `OnMetal`, `SecondaryButton`, `PrimaryButton`; `icon_check`; строки `Repeat_*`, `Sound_*`, `NoSound`, `Cancel`, `SheetOk`.
- Produces:
  - `RepeatSheetModel` — `ObservableCollection<ChoiceOption> Options` (Id = `RepeatKind.ToString()`), `ObservableCollection<ChoiceOption> Days` (7, с понедельника; Id = имя флага `WeekDays`, Name — сокращённое имя дня культуры), `RepeatKind Repeat`, `bool IsDaysVisible`, `WeekDays SelectedDays`, `void Load(RepeatKind repeat, WeekDays days)`, `event EventHandler<bool>? Done`, команды `SelectCommand(ChoiceOption)`, `ToggleDayCommand(ChoiceOption)`, `OkCommand`, `CancelCommand`. **«Окей» с `Weekly` без выбранных дней ставит `Repeat = Daily`** до события `Done`.
  - `SoundSheetModel(ISoundPlayer player)` — `ObservableCollection<ChoiceOption> Options` (первая — Id `""` «Без звука», затем `Sounds.All`), `string SelectedId` (`""` = без звука), `void Load(bool soundEnabled, string soundId)`, `event EventHandler<bool>? Done`, команды `SelectCommand(ChoiceOption)` (выбрать; мелодию — проиграть один раз `Play(id, loop: false)`; «Без звука» — `Stop()`), `OkCommand`, `CancelCommand` (обе вызывают `Stop()` перед `Done`).

- [ ] **Step 1:** Перенести `ChoiceOption` в `ViewModels/ChoiceOption.cs` (namespace `Chrono.ViewModels`, текст класса без изменений), удалить его из `AlarmEditViewModel.cs`. Сборка 0/0 — поведение не изменилось.
- [ ] **Step 2: `RepeatSheetModel`** (**набросок**; выбранность вариантов обновляется в `OnRepeatChanged`, начальная — в конструкторе явным вызовом, как в нынешнем `AlarmEditViewModel`):

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик шторки повтора; без выбранных дней «По дням недели» сохраняется как «Каждый день».</summary>
public sealed partial class RepeatSheetModel : ObservableObject
{
    public RepeatSheetModel()
    {
        this.Options = new(Enum.GetValues<RepeatKind>().Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Repeat_{kind}", AppResources.Culture) ?? kind.ToString())));
        // Неделя с понедельника; AbbreviatedDayNames начинается с воскресенья.
        var names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
        this.Days = new(Enumerable.Range(1, 7).Select(i => (DayOfWeek)(i % 7))
            .Select(day => new ChoiceOption(((WeekDays)(1 << (int)day)).ToString(), names[(int)day])));
        this.OnRepeatChanged(this.Repeat);
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    public ObservableCollection<ChoiceOption> Days { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDaysVisible))]
    public partial RepeatKind Repeat { get; set; }

    public bool IsDaysVisible => this.Repeat == RepeatKind.Weekly;

    public WeekDays SelectedDays =>
        this.Days.Where(d => d.IsSelected).Aggregate(WeekDays.None, (days, d) => days | Enum.Parse<WeekDays>(d.Id));

    public event EventHandler<bool>? Done;

    public void Load(RepeatKind repeat, WeekDays days)
    {
        this.Repeat = repeat;
        foreach (var day in this.Days)
        {
            day.IsSelected = days.HasFlag(Enum.Parse<WeekDays>(day.Id));
        }
    }

    [RelayCommand]
    private void Select(ChoiceOption option) => this.Repeat = Enum.Parse<RepeatKind>(option.Id);

    [RelayCommand]
    private void ToggleDay(ChoiceOption option) => option.IsSelected = !option.IsSelected;

    [RelayCommand]
    private void Ok()
    {
        if (this.Repeat == RepeatKind.Weekly && this.SelectedDays == WeekDays.None)
        {
            this.Repeat = RepeatKind.Daily;
        }

        this.Done?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnRepeatChanged(RepeatKind value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value.ToString();
        }
    }
}
```

Замечание: `WeekDays.None.HasFlag(...)` для `days == None` даёт `false` для всех дней — верно.

- [ ] **Step 3: `SoundSheetModel`** (**набросок**):

```csharp
using System.Collections.ObjectModel;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик шторки звука: Id "" — «Без звука». Нажатие на мелодию проигрывает её один раз.</summary>
public sealed partial class SoundSheetModel : ObservableObject
{
    private readonly ISoundPlayer player;

    public SoundSheetModel(ISoundPlayer player)
    {
        this.player = player;
        this.Options = new([
            new ChoiceOption("", AppResources.NoSound),
            .. Sounds.All.Select(id => new ChoiceOption(id, AppResources.ResourceManager.GetString($"Sound_{id}", AppResources.Culture) ?? id)),
        ]);
        this.OnSelectedIdChanged(this.SelectedId);
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    [ObservableProperty]
    public partial string SelectedId { get; set; } = Sounds.Default;

    public event EventHandler<bool>? Done;

    public void Load(bool soundEnabled, string soundId) => this.SelectedId = soundEnabled ? soundId : "";

    [RelayCommand]
    private void Select(ChoiceOption option)
    {
        this.SelectedId = option.Id;
        if (option.Id.Length == 0)
        {
            this.player.Stop();
        }
        else
        {
            this.player.Play(option.Id, loop: false);
        }
    }

    [RelayCommand]
    private void Ok()
    {
        this.player.Stop();
        this.Done?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        this.player.Stop();
        this.Done?.Invoke(this, false);
    }

    partial void OnSelectedIdChanged(string value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
```

Проверить по `src/Chrono/Services/PlatformContracts.cs` точную сигнатуру `ISoundPlayer.Play` (сейчас вызывается как `Play(option.Id, loop: false)` в `AlarmEditViewModel`).

- [ ] **Step 4: Разметка** (**набросок**). `RepeatSheet`: `VerticalStackLayout Spacing="16"`; `Border Style=RowGroup` с `VerticalStackLayout BindableLayout.ItemsSource="{Binding Options}"`: шаблон — `Grid Style=Row` `*,Auto`: `Label Name Style=RowValue`, `Image icon_check.png` 20×20 `IsVisible="{Binding IsSelected}"`, `TapGestureRecognizer` → `SelectCommand`; под каждой строкой `BoxView Style=RowDivider` (у последней допустим). Ниже `FlexLayout JustifyContent="SpaceBetween" IsVisible="{Binding IsDaysVisible}" BindableLayout.ItemsSource="{Binding Days}"`: шаблон — `Border Style=Chip` `WidthRequest="44"` `Padding="0"` с `Label Style=ChipText`; `DataTrigger IsSelected` → `Background=MetalBrush`, у текста `OnMetal`; тап → `ToggleDayCommand`. Ниже кнопки `Grid` `*,1.5*`: «Отмена» / «Окей». `SoundSheet`: та же группа строк по `Options` с галочкой → `SelectCommand`; кнопки «Отмена» / «Окей».
- [ ] **Step 5: Сборка** — 0/0.
- [ ] **Step 6: Commit**

```bash
git add -- src/Chrono/ViewModels/ChoiceOption.cs src/Chrono/ViewModels/AlarmEditViewModel.cs src/Chrono/ViewModels/RepeatSheetModel.cs src/Chrono/ViewModels/SoundSheetModel.cs src/Chrono/Views/Sheets/RepeatSheet.xaml src/Chrono/Views/Sheets/RepeatSheet.xaml.cs src/Chrono/Views/Sheets/SoundSheet.xaml src/Chrono/Views/Sheets/SoundSheet.xaml.cs
git commit -m "Шторки повтора и звука; ChoiceOption в отдельном файле" -- <те же пути>
```

---

### Task 8: Список будильников

**Files:**
- Modify: `src/Chrono/Views/AlarmListPage.xaml`

**Interfaces:** Consumes: стили и ресурсы задачи 2, `icon_gear`, `icon_plus` (задача 4); `AlarmListViewModel` и `AlarmItem` не меняются (`TimeText`, `Details`, `IsEnabled`, `NextAlarmText`, `IsWarningVisible`, команды `OpenSettingsCommand`, `OpenReliabilityCommand`, `AddCommand`, `EditCommand`, `DeleteCommand`).

- [ ] **Step 1: Разметка** (**набросок**), отличия от текущей:
  - шапка: `Label AppName` `ManropeExtraBold` 28; справа `Border Style=IconWell` с `Image icon_gear.png` 20×20, `TapGestureRecognizer` → `OpenSettingsCommand`, `SemanticProperties.Description="{x:Static res:AppResources.SettingsTitle}"`;
  - `NextAlarmText` — `TextColor=TextMuted`, 13;
  - баннер: `Background=Surface`, `Stroke=Gold`, `StrokeShape="RoundRectangle 16"`, текст `GoldInk` `ManropeSemiBold`;
  - карточка: `Style=Card` без триггера прозрачности; время `ManropeExtraBold` 38, `DataTrigger IsEnabled=False` → `TextColor=Hairline`; `Details` — `Style=Muted` 12.5; тумблер справа;
  - `SwipeItem` «Удалить» — `BackgroundColor=Metal700`;
  - «+»: `Border` 60×60 `StrokeShape="RoundRectangle 30"` `Background=MetalBrush` `Stroke=Gold` `StrokeThickness="1"` с тенью `Shadow Brush=#2E3136 Offset=0,10 Radius=22 Opacity=0.28`, внутри `Image icon_plus.png` 24×24; `TapGestureRecognizer` → `AddCommand`; `SemanticProperties.Description` — `NewAlarm`; `HorizontalOptions=End VerticalOptions=End Margin="0,0,4,24"`.
- [ ] **Step 2: Сборка** — 0/0. **Step 3: Commit** `-- src/Chrono/Views/AlarmListPage.xaml`, сообщение «Список будильников в теме «Металл · Кость»».

---

### Task 9: Экран сигнала и заставка

**Files:**
- Modify: `src/Chrono/Views/RingPage.xaml`
- Modify: `src/Chrono/Views/SplashPage.cs`

**Interfaces:** Consumes: стили задачи 2, новый `bell.svg` (светлое тело, золотой язычок; задача 4). Имена `Bell`, `Wave1..3` в `RingPage.xaml` сохранить — их анимирует `RingPage.xaml.cs`. `RingViewModel` не меняется.

- [ ] **Step 1: `RingPage.xaml`** (**набросок**): блок колокола 180×180 — три `Ellipse` волн со `Stroke=Gold`, по центру `Border` 128×128 `StrokeShape="RoundRectangle 64"` `Background=MetalBrush` `Stroke=GoldEdgeBrush` `StrokeThickness="1"`, внутри `Image x:Name="Bell"` 62×71 (пропорция 70:80) `AnchorY="0.08"`; время `ManropeExtraBold` 72; название `ManropeBold` 20; дата `Style=Muted` 14; «Стоп» — `PrimaryButton` во всю ширину, `HeightRequest="58"` `FontSize="18"`; блок примера: подпись `Style=Muted`, пример `ManropeExtraBold` 40, «Неверно…» `TextColor=GoldInk`, четыре ответа — `SecondaryButton` `FontSize="22"`.
- [ ] **Step 2: `SplashPage.cs`**: фон `Color.FromArgb("#F5F2EC")` (совпадает с `MauiSplashScreen Color` задачи 4 — переход без вспышки); волны `Stroke` `#D1B27C`; под колоколом металлический круг `Ellipse` 72×72 с `Fill`/`Stroke` — кисти `MetalBrush`/`GoldEdgeBrush` через `Application.Current!.Resources.TryGetValue(key, out var value)` (индексатор `Resources[key]` может не искать в `MergedDictionaries`), `StrokeThickness = 1.5`; колокол 34×39 по центру круга; заголовок «Chrono» `ManropeExtraBold` 28 `#2E3136`. Размеры подобрать так, чтобы круг совпадал с нативным splash (круг r=30 из 108 при `BaseSize 128` ≈ 71 dp) — проверяет координатор двумя скриншотами подряд.
- [ ] **Step 3: Сборка** — 0/0. **Step 4: Commit** `-- src/Chrono/Views/RingPage.xaml src/Chrono/Views/SplashPage.cs`, сообщение «Экран сигнала и заставка в теме «Металл · Кость»».

---

### Task 10: Настройки и «Надёжность»

**Files:**
- Modify: `src/Chrono/Views/SettingsPage.xaml`, `src/Chrono/Views/ReliabilityPage.xaml`

**Interfaces:** Consumes: стили задачи 2, `icon_back`, `icon_chevron`; `SettingsViewModel` (`SmartWakeEnabled`, `IsSmartWakeSupported`, `AccessText`, `CanGrantAccess`, `MathChallengeEnabled`, команды `GrantAccessCommand`, `OpenReliabilityCommand`, `CloseCommand`), `ReliabilityViewModel` (`Rows` с `Title`, `NeedsFix`; `IsAndroid`, `IsIos`, `FixCommand`, `CloseCommand`) — модели не меняются.

- [ ] **Step 1: Шапка** обеих страниц: `Grid` `Auto,*` `ColumnSpacing="12"` `Padding="0,6,0,14"`: `Border Style=IconWell` с `Image icon_back.png` 18×18, тап → `CloseCommand`, `SemanticProperties.Description` = `Back`; заголовок `Style=ScreenTitle`. Кнопка «OK» внизу удаляется, строка `Grid` для неё убирается.
- [ ] **Step 2: Настройки** (**набросок**): заголовок группы `SectionHeader` «Умное пробуждение» (`SmartWake`); `Card` со строкой «тумблер», описанием `Muted` 13, `AccessText` `TextColor=GoldInk` 13, `CompactAccentButton` «Дать доступ» (`HorizontalOptions=Start`), приватность `Footnote`. `SectionHeader` `DismissGroup`; `Card` с `CheckBox` + «Решить пример» `ManropeBold` 15 + описание `Muted` 13. Строка «Надёжность» — `RowGroup` с `Grid Style=Row`: `Label Style=RowValue`, `Image icon_chevron.png` 16×16, тап → `OpenReliabilityCommand`.
- [ ] **Step 3: «Надёжность»** (**набросок**): строки в одном `RowGroup` (`BindableLayout` по `Rows`): `Grid Style=Row` `Auto,*,Auto` — метка 28×28: при `NeedsFix=False` `Border` круг `Background=MetalBrush` с `Label "✓"` `OnMetal` `ManropeBold` 14; при `NeedsFix=True` `Border` круг `Background=Surface2` `Stroke=Gold` с `Label "!"` `GoldInk` `ManropeExtraBold` 14 (два `Border` с `IsVisible` по `NeedsFix`, свойство `Mark` больше не используется в разметке); `Title` — `Style=RowValue` с `LineBreakMode=WordWrap`; «Исправить» — `CompactAccentButton`, `IsVisible=NeedsFix`; под строкой `RowDivider`. Подсказки `OemHint`/`SilentHint` — `Footnote`.
- [ ] **Step 4: Сборка** — 0/0. **Step 5: Commit** `-- src/Chrono/Views/SettingsPage.xaml src/Chrono/Views/ReliabilityPage.xaml`, сообщение «Настройки и «Надёжность» в теме «Металл · Кость»».

---

### Task 11: Экран будильника — плашка, строки, шторки, сохранение

Перед правкой — прочитать `~/.claude/references/fix-user-experience.md` (поле «Название»).

**Files:**
- Modify (полная переработка): `src/Chrono/ViewModels/AlarmEditViewModel.cs`, `src/Chrono/Views/AlarmEditPage.xaml`, `src/Chrono/Views/AlarmEditPage.xaml.cs`

**Interfaces:**
- Consumes: всё из задач 1, 2, 4–7: `AlarmSchedule.ResolveNearest`, `AlarmSchedule.Until`; `BottomSheet`; `TimeSheetModel` (`Load`, `RequestScroll`, `Value`, `Done`), `CalendarSheetModel` (`Load`, `Selected`, `Hint`, `Done`), `RepeatSheetModel` (`Load`, `Repeat`, `SelectedDays`, `Done`), `SoundSheetModel` (`Load`, `SelectedId`, `Done`); `TimeWheelSheet`, `CalendarSheet`, `RepeatSheet`, `SoundSheet`; `Alarm.NextAfter`, `AlarmService.Save/Delete/GetAll`; строки задачи 2.
- Produces: `AlarmEditViewModel` с публичным API ниже (используется только страницей).

- [ ] **Step 1: Модель** — состояние и вычисления (**набросок**; поведение сохранения совпадает с нынешним: повтор → `At = NextAfter(now)`, `Save` отклоняет прошлое, `IsEnabled = true`):

```csharp
// Сохраняемые значения (поля CommunityToolkit, начальные — без вызова OnXChanged):
[ObservableProperty] TimeOnly Time            // по умолчанию: now + 1 ч, без секунд
[ObservableProperty] DateOnly? Date           // null — «Ближайшее»
[ObservableProperty] RepeatKind Repeat
[ObservableProperty] WeekDays Days
[ObservableProperty] string Label
[ObservableProperty] bool SoundEnabled        // по умолчанию true
[ObservableProperty] string SoundId           // по умолчанию Sounds.Default
[ObservableProperty] bool VibrationEnabled    // по умолчанию true
[ObservableProperty] string ErrorText, string Title, bool IsExisting
// Модели шторок и флаги открытия:
TimeSheetModel TimeSheet; CalendarSheetModel CalendarSheet; RepeatSheetModel RepeatSheet; SoundSheetModel SoundSheet (new SoundSheetModel(soundPlayer))
[ObservableProperty] bool IsTimeSheetOpen, IsDateSheetOpen, IsRepeatSheetOpen, IsSoundSheetOpen
bool IsAnySheetOpen
// Тексты для разметки:
string TimeText        // Time.ToString("HH:mm")
string DateText        // Date null → Nearest; Repeat == Yearly → "d MMMM"; иначе "dd.MM.yyyy"
bool IsDateRowVisible  // Repeat is None or Yearly
string RepeatText      // None/Daily/Yearly → Repeat_*; Weekly → дни с понедельника через ", " (сокращённые имена культуры); Weekly без дней → Repeat_Daily
string SoundText       // SoundEnabled ? Sound_{SoundId} : NoSound
string PlateTitle      // см. ниже
string RelText         // Until(Target(Time), now) → RelDays/RelHours/RelMinutes; null → TimePassed
string TimeHint        // то же для Target(TimeSheet.Value) — подсказка шторки времени
bool IsDirty           // правки после загрузки
```

Вычисление момента срабатывания (один метод — у него четыре вызывающих: `PlateTitle`, `RelText`, `TimeHint`, сохранение):

```csharp
    // Повтор — ближайшее по правилу; разовый с датой — ровно эта дата (может быть в прошлом: сохранение откажет);
    // разовый без даты — «Ближайшее»: сегодня или завтра.
    private DateTime Target(TimeOnly time) => this.Repeat != RepeatKind.None
        ? new Alarm { At = (this.Date ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(time), Repeat = this.Repeat, Days = this.Days }.NextAfter(DateTime.Now)
        : this.Date is { } date
            ? date.ToDateTime(time)
            : AlarmSchedule.ResolveNearest(time, DateTime.Now);
```

`PlateTitle`: `var t = Target(Time)`; если `Repeat == None && Date is null` — `string.Format(t.Date == DateTime.Today ? PlateToday : PlateTomorrow, t.ToString("ddd, d MMMM", CurrentUICulture))`; иначе `string.Format(PlateDate, t.ToString("dddd, d MMMM", CurrentUICulture))`.

Все вычисляемые тексты пересчитываются при изменении любого сохраняемого значения (`[NotifyPropertyChangedFor(...)]` на свойствах `Time`, `Date`, `Repeat`, `Days`, `SoundEnabled`, `SoundId`). `RefreshClock()` — публичный метод: `OnPropertyChanged(nameof(PlateTitle)); OnPropertyChanged(nameof(RelText));` (таймер страницы).

`TimeSheet.PropertyChanged` → при `Hour`/`Minute` — `OnPropertyChanged(nameof(TimeHint))`.

- [ ] **Step 2: Модель** — шторки и команды:
  - `OpenTimeCommand`: `TimeSheet.Load(Time); IsTimeSheetOpen = true; MainThread.BeginInvokeOnMainThread(TimeSheet.RequestScroll);`
  - `OpenDateCommand`: `CalendarSheet.Load(Date, DateOnly.FromDateTime(DateTime.Now), allowPast: Repeat == RepeatKind.Yearly); IsDateSheetOpen = true;`
  - `OpenRepeatCommand`: `RepeatSheet.Load(Repeat, Days); IsRepeatSheetOpen = true;`
  - `OpenSoundCommand`: `SoundSheet.Load(SoundEnabled, SoundId); IsSoundSheetOpen = true;`
  - обработчики `Done` (подписка в конструкторе): при `accepted` переносят значения (`Time = TimeSheet.Value`; `Date = CalendarSheet.Selected`; `Repeat = RepeatSheet.Repeat; Days = RepeatSheet.Repeat == Weekly ? RepeatSheet.SelectedDays : WeekDays.None`; `SoundEnabled = SoundSheet.SelectedId.Length > 0; if (SoundEnabled) SoundId = SoundSheet.SelectedId;`) и закрывают свою шторку; при `false` только закрывают;
  - `CloseSheetCommand` (затемнение, «Назад»): вызывает `CancelCommand` открытой модели (звук останавливается в `SoundSheetModel.Cancel`);
  - `SaveCommand`: `var alarm = (existing ?? new Alarm()) with { At = Target(Time), Label = Label.Trim(), SoundEnabled, SoundId, VibrationEnabled, IsEnabled = true, Repeat, Days = Repeat == Weekly ? Days : WeekDays.None };` → `if (!alarmService.Save(alarm)) { ErrorText = AppResources.TimeInPast; return; }` → `soundPlayer.Stop(); await Shell.Current.GoToAsync("..");`
  - `DeleteCommand` — как сейчас;
  - `BackCommand`: если `IsDirty` — `await Shell.Current.DisplayAlertAsync(AppResources.DiscardTitle, "", AppResources.DiscardYes, AppResources.Cancel)` (перегрузка `title, message, accept, cancel` → `Task<bool>`; `null` в `message` дал бы предупреждение nullable); при `false` — остаться; иначе `soundPlayer.Stop(); await Shell.Current.GoToAsync("..");`
  - `ApplyQueryAttributes`: для существующего — `Time = TimeOnly.FromDateTime(At)` (без секунд), `Date = Repeat is None or Yearly ? DateOnly.FromDateTime(At) : null`, `Repeat`, `Days`, `Label`, `SoundEnabled`, `SoundId`, `VibrationEnabled`, `Title = EditAlarm`, `IsExisting = true`; `expired` → `ErrorText = PickNewDate`; в конце `IsDirty = false`.
  - `IsDirty = true` в `OnTimeChanged`, `OnDateChanged`, `OnRepeatChanged`, `OnDaysChanged`, `OnLabelChanged`, `OnSoundEnabledChanged`, `OnSoundIdChanged`, `OnVibrationEnabledChanged` (каждый также `ErrorText = ""`); загрузка (`ApplyQueryAttributes`) сбрасывает `IsDirty` последней.
  - Удалить: `Hour/Minute/Day/Month/Year`, `At`, `WhenText`, `SoundOptions`, `RepeatOptions`, `DayOptions`, `SelectedDays`, команды `SelectSound`, `SelectRepeat`, `ToggleDay`, `Cancel`.

- [ ] **Step 3: Разметка `AlarmEditPage.xaml`** (**набросок**): корень `Grid SafeAreaEdges="Container, Container, Container, All"` без отступов; первый ребёнок — `Grid RowDefinitions="Auto,*,Auto" Padding="20,0"`; поверх — четыре `controls:BottomSheet` (во всю площадь корня):

```xml
<controls:BottomSheet IsOpen="{Binding IsTimeSheetOpen}" Title="{x:Static res:AppResources.RowTime}"
                      Hint="{Binding TimeHint}" CancelCommand="{Binding CloseSheetCommand}">
    <sheets:TimeWheelSheet BindingContext="{Binding TimeSheet}" />
</controls:BottomSheet>
<!-- Дата: Title RowDate, Hint="{Binding CalendarSheet.Hint}", <sheets:CalendarSheet BindingContext="{Binding CalendarSheet}" />
     Повтор: Title RepeatHeader, Hint пустой, RepeatSheet.  Звук: Title Sound, Hint пустой, SoundSheet. -->
```

Строка 0 — шапка (`IconWell` + `icon_back`, тап → `BackCommand`; `Label Title Style=ScreenTitle`). Строка 1 — `ScrollView` с `VerticalStackLayout Spacing="22"`:
  1. плашка: `Border StrokeShape="RoundRectangle 20" Background="{StaticResource MetalBrush}" Stroke="{StaticResource GoldEdgeBrush}" StrokeThickness="1" Padding="20,20,20,18"` → `Label PlateTitle Style=Eyebrow TextColor=Gold` + `Grid *,Auto`: `Label TimeText` `ManropeExtraBold` 64 `OnMetal`, `Label RelText` `ManropeSemiBold` 13 `OnMetal` `Opacity="0.7"` `VerticalOptions=End`;
  2. `RowGroup`: строки «Время» (`TimeText`), «Дата» (`DateText`, строка и разделитель перед ней `IsVisible="{Binding IsDateRowVisible}"`), «Повтор» (`RepeatText`); каждая — `Grid Style=Row` `*,Auto,Auto`: `RowKey`, `RowValue`, `icon_chevron.png` 16×16; тап → `Open*Command`;
  3. `RowGroup`: «Название» — `Grid Style=Row` `Auto,*`: `RowKey`, `Entry Text="{Binding Label}" Placeholder="{x:Static res:AppResources.DefaultLabel}" MaxLength="60" HorizontalTextAlignment="End" FontFamily="ManropeBold" FontSize="14.5" ReturnType="Done"`; «Звук» (`SoundText`, шеврон, → `OpenSoundCommand`); «Вибрация» — `Switch IsToggled="{Binding VibrationEnabled}"`;
  4. `Label SaveFootnote Style=Footnote`.
  Строка 2 — `VerticalStackLayout Spacing="8" Padding="0,14,0,0"`: `Label ErrorText TextColor=GoldInk IsVisible=HasError`; `Grid ColumnDefinitions="Auto,*" ColumnSpacing="10"`: «Удалить» — `Border` 54×54 `StrokeShape="RoundRectangle 18"` `Background=CanvasBrush` `Stroke=Gold` с `icon_trash.png` 20×20, `IsVisible=IsExisting`, тап → `DeleteCommand`, `SemanticProperties.Description=DeleteAlarm`; «Сохранить» — `PrimaryButton` в колонке 1 → `SaveCommand`. Нижний отступ страницы — 16 (системная панель уже учтена `SafeAreaEdges`).

- [ ] **Step 4: `AlarmEditPage.xaml.cs`**: удалить обработчики цифровых полей; добавить:
  - `OnAppearing`: `this.isShown = true; this.Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () => { this.viewModel.RefreshClock(); return this.isShown; });`
  - `OnDisappearing`: `this.isShown = false; this.viewModel.StopPreview();` (как сейчас)
  - `OnBackButtonPressed`: `if (this.viewModel.IsAnySheetOpen) { this.viewModel.CloseSheetCommand.Execute(null); return true; } if (this.viewModel.IsDirty) { this.viewModel.BackCommand.Execute(null); return true; } return base.OnBackButtonPressed();`

**Молчаливые отказы этой задачи** (назвать в отчёте, какие проверены сборкой, какие оставлены координатору): Shell на Android может не вызвать `OnBackButtonPressed` страницы при системном жесте «Назад» — тогда шторка не закрывается, а страница уходит; `BindingContext="{Binding TimeSheet}"` внутри `BottomSheet` теряется, если `SheetContent` назначается до установки контекста страницы — шторка пустая без ошибок; `Entry` внутри строки 54 pt обрезает текст по высоте.

- [ ] **Step 5: Сборка решения** `dotnet build Chrono.slnx` — 0/0; `dotnet test --solution Chrono.slnx` — `failed: 0`.
- [ ] **Step 6: Commit** `-- src/Chrono/ViewModels/AlarmEditViewModel.cs src/Chrono/Views/AlarmEditPage.xaml src/Chrono/Views/AlarmEditPage.xaml.cs`, сообщение «Экран будильника по макету: плашка, группы строк, шторки времени, даты, повтора и звука».

---

### Task 12: Чистка и полная приёмка на эмуляторе (координатор)

**Files:**
- Modify: `src/Chrono/Resources/Styles/Colors.xaml` (удалить псевдонимы), `src/Chrono/Resources/Styles/Styles.xaml` (удалить `DangerLink`, `DigitBox`, `DigitEntry`), `AppResources.resx`/`.ru.resx` (удалить неиспользуемые ключи), `src/Chrono/MauiProgram.cs` и `Resources/Fonts/Manrope-Light.ttf` (если `ManropeLight` больше нигде не используется), `docs/superpowers/plans/2026-10-13-metal-theme-progress.md`.

- [ ] **Step 1: Найти неиспользуемое.** `grep -rn "Accent\b\|AccentBrush\|OnAccent\|SurfaceBorder\|Danger\|SwitchOff\|PageBackground\|BgDeep\|BgMid\|BgTop\|AccentEnd\|DangerLink\|DigitBox\|DigitEntry\|ManropeLight" src/Chrono --include=*.xaml --include=*.cs` (без `obj`). Для строк ресурсов — каждый ключ из `.resx` искать как `AppResources.<Key>` и `"<Key>"`/`$"…{…}"`-шаблоны (`Sound_`, `Repeat_`, `Rel_` берутся по префиксу — их не удалять). Кандидаты: `DayPlaceholder`, `MonthPlaceholder`, `YearPlaceholder`, `InvalidDateTime`, `LabelHeader`, `LabelPlaceholder`, `SignalHeader`, `SoundPickerTitle`, `NoDaysSelected`, `NextOccurrence`, `Ok` (если больше нигде).
- [ ] **Step 2: Удалить найденное**, собрать решение 0/0, тесты `failed: 0`.
- [ ] **Step 3: Эмулятор.** `chrono_api36` на `emulator-5570`; установка `dotnet build src/Chrono/Chrono.csproj -t:Install -f net11.0-android -p:EmbedAssembliesIntoApk=true "-p:AdbTarget=-s emulator-5570"`. Скриншоты (в `.superpowers/sdd/metal/`): заставка (два кадра), список пустой и с будильниками (вкл/выкл), редактор нового и существующего, каждая шторка, сигнал обычный и с примером (открыть тестовым запуском `RingLauncher`/intent с id существующего будильника), настройки, «Надёжность», иконка в лаунчере, статус-бар с тёмными значками.
- [ ] **Step 4: Барабан и календарь.** `adb shell input swipe` по колонке часов (медленно и «флингом»), чтение плашки после «Окей» (`uiautomator dump`); тап по числу; чипы; календарь: листание вперёд/назад, тап по прошедшему дню — без эффекта, «Сегодня», «Очистить» → «Ближайшее».
- [ ] **Step 5: «Название»** по чек-листу `fix-user-experience.md`: ввод по одному символу с чтением поля после каждого, Backspace ×3 и повторный ввод, скриншот с открытой клавиатурой — «Сохранить» видна.
- [ ] **Step 6: «Назад».** С открытой шторкой — закрывается шторка; с правками — диалог «Не сохранять изменения?»; без правок — уход.
- [ ] **Step 7: Сохранение.** Будильник на дальнюю дату: `run-as com.chrono.alarm cat files/alarms.json` и `dumpsys alarm` совпадают с плашкой; «Ближайшее» с временем раньше текущего → завтра; после проверки будильники удалить.
- [ ] **Step 8: Commit** чистки точными путями; записать итоги в файл прогресса.

---

## Самопроверка плана

- Покрытие спецификации: 1.1–1.4 → задача 2; 1.5 → задача 3; иконки/колокол/иконка/splash → задачи 4, 9; раздел 2 → задача 11; 3.1–3.2 → задача 5; 3.3 → задача 6; 3.4–3.5 → задача 7; раздел 4 → задачи 1, 11; раздел 5 → задачи 8–10; раздел 6 → задача 12; раздел 7 → задача 1.
- Имена сквозь задачи: `TimeSheetModel.Load/RequestScroll/Value/Done`, `CalendarSheetModel.Load/Selected/Hint/Done`, `RepeatSheetModel.Load/Repeat/SelectedDays/Done`, `SoundSheetModel.Load/SelectedId/Done`, `BottomSheet.IsOpen/Title/Hint/SheetContent/CancelCommand` — совпадают в задачах 5–7 и 11.
- Наброски помечены; проверенный код — только задача 1 (прогон 17/17 и четыре мутации).
