# Chrono — план реализации будильника (MAUI .NET 11)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Будильник для Android и iPhone на .NET MAUI 11: несколько разовых будильников, текст + выбираемые звук и вибрация, сигнал при выгруженном приложении и после перезагрузки, тема «Ночное небо», иконка-колокол, анимированный splash.

**Architecture:** Одно MAUI-приложение `src/Chrono` (`net11.0-android;net11.0-ios`). Расписание отдаётся ОС: Android — `AlarmManager.SetAlarmClock` → `AlarmReceiver` → foreground-сервис `systemExempted` с полноэкранным сигналом; iOS — цепочка time-sensitive уведомлений. Граница с ОС — `IAlarmScheduler`/`ISoundPlayer`/`IReliabilityChecks` в `Services/PlatformContracts.cs`; чистая логика (`AlarmStore`, `AlarmService`, `IosNotificationPlan`) покрыта unit-тестами.

**Tech Stack:** .NET SDK 11.0.100-rc.1, MAUI 11 rc.1, CommunityToolkit.Mvvm 8.4.2, xunit.v3 4.0.1 (Microsoft.Testing.Platform), System.Text.Json, Python 3 stdlib (синтез мелодий), шрифт Manrope (OFL).

**Spec:** `docs/superpowers/specs/2026-10-06-chrono-alarm-design.md` — читать вместе с планом. Раздел «Уточнения к спецификации» ниже имеет приоритет над спецификацией там, где они расходятся.

**Происхождение кода.** Весь код в задачах взят из спайка, проверенного 2026-10-06:
- Android и iOS компилируются без ошибок и предупреждений; каждое промежуточное состояние волн собрано отдельно в кириллическом пути.
- 21 unit-тест зелёный. Каждый ключевой тест доказан мутацией: сломанный код роняет тест.
- На эмуляторе API 36 цепочка проверена живым прогоном: процесс убит (`kill -9`), экран выключен; будильник сработал вовремя; FGS `types=0x400`; экран сигнала показан поверх блокировки; звук `USAGE_ALARM` играет; «Стоп» снимает сервис и уведомление. Отрендерены splash, список, редактор (с открытой клавиатурой и посимвольным вводом), «Надёжность», русская локаль и иконка в лаунчере.

**Переносить код дословно.** Если код из плана не собирается или ведёт себя не так, как описано, не исправляйте его втихую. Остановитесь и сообщите координатору: дефект нужно исправить и в плане.

---

## Окружение (обязательно для каждого субагента)

| Что | Значение |
|---|---|
| ОС / оболочки | Windows 11; Git Bash (инструмент Bash) и PowerShell 7 |
| .NET | SDK `11.0.100-rc.1.26425.128`; workloads `android` (37.0 rc.1), `ios` (26.5) |
| Репозиторий | `E:\Works_Sata\Chrono-Будильник` — **путь с кириллицей**. `obj`/`bin` вынесены в `%TEMP%\chrono-build\<хеш>\` через `Directory.Build.props` (задача 1), иначе Android падает с `APT2265` |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk` (adb: `platform-tools\adb.exe`, emulator: `emulator\emulator.exe`) |
| Эмулятор | любой запущенный AVD API 36 (`adb devices`; в день проверки — `emulator-5562`, AVD `mobileasr_test`). AVD `pixel_7_-_api_36_0` занят зависшим процессом — его не трогать. Чужие эмуляторы не перезапускать и не закрывать |
| Mac | нет. iOS компилируется на Windows (`-f net11.0-ios`), но не запускается |
| БД / Docker / порты | не используются |
| Python | 3.13 в PATH (`python`) |

**Грабли окружения (проверены):**
- **Git Bash переписывает пути вида `/sdcard/...` в аргументах adb.** Перед adb-командами с такими путями выполняйте `export MSYS_NO_PATHCONV=1`.
- **Хук запрещает `rm -rf` в Bash.** Удаляйте файлы через PowerShell: `Remove-Item -LiteralPath <путь> -Recurse -Force`, путь указывайте точно.
- **Установка на эмулятор — только APK со встроенными сборками:**
  `dotnet build src/Chrono/Chrono.csproj -t:Install -f net11.0-android -p:EmbedAssembliesIntoApk=true "-p:AdbTarget=-s <serial>"`.
  Без `EmbedAssembliesIntoApk` команда `pm clear` стирает сборки fast deployment, и приложение падает на старте с `Failed to initialize CoreCLR 80070002`.
- **Строка «FATAL» в logcat ещё не значит падение.** `W HWUI: Failed to initialize 101010-2 format` — шум эмулятора. Падение приложения видно по `E AndroidRuntime: FATAL EXCEPTION` или `F monodroid`.
- **Эмулятор считает, что подключена аппаратная клавиатура.** Поэтому Gboard показывает плавающую панель вместо полной клавиатуры, а долгий Backspace открывает обучалку стилуса. Перед проверкой клавиатуры:
  `adb shell settings put secure show_ime_with_hard_keyboard 1` и `adb shell settings put secure stylus_handwriting_enabled 0`.
  После проверки верните: `adb shell settings put secure show_ime_with_hard_keyboard 0` и `adb shell settings delete secure stylus_handwriting_enabled`.
- **Локаль меняется только для приложения**, AVD не трогается: `adb shell cmd locale set-app-locales com.chrono.alarm --locales ru-RU`. Сброс: `... --locales en-US`.
- **`TimePicker` и `DatePicker` в MAUI — тоже `EditText`.** Поле «Текст» в дампе `uiautomator` ищите по `hint="Alarm text"` (en) или `hint="Текст будильника"` (ru).
- **Тест-раннер.** В .NET 11 SDK `dotnet test` работает только через Microsoft.Testing.Platform: в `global.json` задано `"test": {"runner": "Microsoft.Testing.Platform"}`. Пакеты `Microsoft.NET.Test.Sdk` и `xunit.runner.visualstudio` **не добавлять**.

## Global Constraints

- Целевые платформы: `net11.0-android;net11.0-ios`. Android: min API 26, target по умолчанию (37). iOS: min 15.0.
- `ApplicationId` = `com.chrono.alarm`. Перед публикацией владелец может его заменить.
- Зависимости: только `Microsoft.Maui.Controls`, `Microsoft.Extensions.Logging.Debug`, `CommunityToolkit.Mvvm 8.4.2`; в тестах только `xunit.v3 4.0.1`. Новые пакеты не добавлять.
- Стиль C#:
  - приватные поля без `_`, обращение через `this.`;
  - без private-методов-помощников без необходимости (исключение — обработчик события, который нужно отписать);
  - комментарии на русском; идентификаторы на английском.
- Все строки UI — в `AppResources.resx` (en) и `AppResources.ru.resx`. Литералы в UI допустимы только для «Chrono» на `SplashPage`.
- Цвета — только токены из `Resources/Styles/Colors.xaml`. Приложение всегда тёмное (`UserAppTheme = AppTheme.Dark`).
- В модальной форме кнопки прижаты вправо, primary («Сохранить»/«ОК») — крайняя справа.
- iOS: `UNNotificationInterruptionLevel.TimeSensitive2`, **никогда** `TimeSensitive` (значение 3 = Critical у iOS).
- Время будильника хранится как «настенное» (`DateTimeKind.Unspecified`, в JSON без смещения).
- Сборка каждой задачи: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` и `-f net11.0-ios` дают **0 ошибок и 0 предупреждений**. Там, где есть тесты, `dotnet test tests/Chrono.Tests/Chrono.Tests.csproj` зелёный.
- Коммиты: сообщение на русском, последней строкой `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Индексировать **только точные пути файлов** (не папки, не `-A`, не `.`), коммитить с pathspec: `git commit -m "..." -- <файлы>`.

## Уточнения к спецификации (выявлены спайком; приоритетнее спецификации)

1. **Граница с ОС** — `Services/PlatformContracts.cs`:
   - `IAlarmScheduler { Sync(IReadOnlyList<Alarm>); Cancel(Guid); StopRinging(Guid); }`. `Sync` приводит расписание ОС к списку целиком (iOS соблюдает лимит 16, Android снимает выключенные).
   - `ISoundPlayer { Play(string soundId, bool loop); Stop(); }` — прослушивание мелодии в редакторе и сигнал на iOS на переднем плане.
   - `IReliabilityChecks` — проверки «Надёжности».
2. **`AlarmService.Save`:**
   - отклоняет `At <= now` (возвращает `false`);
   - приводит `At` к `Kind=Unspecified`;
   - редактор всегда сохраняет с `IsEnabled = true`.
3. **Мелодии** лежат в корне `Resources/Raw/` (`bell|chime|digital|rising|soft.wav`), без подпапки `sounds/`. В csproj задан `AndroidStoreUncompressedFileExtensions=.wav`.
4. **Вибрация на Android** включается с `VibrationAttributes.CreateForUsage((int)VibrationAttributesUsageType.Alarm)` на API 33+. Без атрибутов система записывает её как `UNKNOWN` и может подавить в «Не беспокоить».
5. **`StartForeground` вызывается раньше любых ранних выходов** из `OnStartCommand`, иначе система роняет процесс. Тип `systemExempted` передаётся только на API 34+.
6. **`Page.Window` у страницы, открытой через Shell, в `OnAppearing` ещё `null`** (NRE на эмуляторе). Окно берётся через `Application.Current?.Windows.FirstOrDefault()`.
7. **Первый запуск:** запрос разрешений, затем сразу экран «Надёжность». Флаг `first_run_done` хранится в `Preferences`.
8. **Нативное подчёркивание** у `Entry`/`DatePicker`/`TimePicker` на Android убрано через `AppendToMapping` (поля стоят в карточках с рамкой).
9. **Android 16 «AudioHardening»** пишет `background playback would be muted`. Пока это только предупреждение: `mutedState:none`. Приёмка проверяет это по `dumpsys audio`.

## Review Focus

Пять входов, которые не покрыты тестами отдельных задач, но первыми ударят по пользователю. Каждый закреплён проверкой в задаче-владельце.

1. **Процесс убит, экран заблокирован.** Будильник обязан прозвучать полноэкранно и вовремя. Проверка — задача 8, сценарий A1.
2. **Смена часового пояса.** 06:30 остаётся 06:30 по новому поясу: время хранится без смещения, `BootReceiver` сбрасывает `TimeZoneInfo` и переустанавливает расписание. Проверка — тест `Save_LocalKind_IsStoredAsWallClockWithoutOffset` (задача 2) и сценарий A9 (задача 8).
3. **Срабатывание удалённого будильника.** Receiver пришёл, а записи в файле уже нет. Сервис должен корректно стартовать и закрыться без падения и без `ForegroundServiceDidNotStartInTimeException`. Проверка — задача 8, сценарий A10.
4. **Два будильника в одну минуту.** Виден один экран сигнала, «Стоп» его закрывает, второй экран не остаётся в стеке. Проверка — задача 8, сценарий A11.
5. **Включение прошедшего будильника переключателем.** Должен открыться редактор с подсказкой «выберите новую дату», а не включиться мёртвый будильник. Проверка — тест `Toggle_EnablePastAlarm_ReturnsFalseAndKeepsItDisabled` (задача 2) и сценарий A12 (задача 8).

## Волны, модели, skills

| Волна | Задачи (параллельно) | Модель | Изоляция | Skills / инструменты |
|---|---|---|---|---|
| 1 | 1. Каркас и ресурсы | sonnet | основное дерево, ветка `feature/chrono-v1` | `dotnet-maui:dotnet-maui-doctor`, `dotnet-maui:maui-theming` |
| 2 | 2. Ядро + тесты ‖ 3. Мелодии | haiku ‖ haiku | git worktree на задачу | `superpowers:test-driven-development` (2) |
| 3 | 4. Android ‖ 5. iOS ‖ 6. ViewModels и страницы | haiku ‖ haiku ‖ haiku | git worktree на задачу | `dotnet-maui:maui-app-lifecycle` (4); `dotnet-maui:maui-data-binding`, `maui-collectionview`, `maui-shell-navigation`, `maui-safe-area` (6) |
| 4 | 7. Интеграция + первый запуск | sonnet | основное дерево | `dotnet-maui:maui-dependency-injection`, `maui-shell-navigation`, `maui-app-lifecycle`, `maui-safe-area`; памятка `~/.claude/references/fix-user-experience.md` |
| 5 | 8. Приёмка на эмуляторе | sonnet | основное дерево | adb / MCP `maui-devflow`, `impeccable:impeccable` (дизайн-ревью по скриншотам) |
| 6 | 9. Финальное ревью ветки | opus | только чтение | `superpowers:requesting-code-review` |

Ревьюер каждой задачи — sonnet. Задаче 4 (Android) он получает конкретные риски из Review Focus 1, 3, 4 и раздела «Уточнения», п. 4–5.

Файлы задач одной волны не пересекаются. После волны координатор вливает ветки worktree в `feature/chrono-v1` и прогоняет полную сборку и тесты. Прогресс и выводы, меняющие следующие задачи, координатор записывает в `docs/superpowers/plans/2026-10-06-chrono-progress.md`.

---

## Task 1: Каркас решения и ресурсы

**Модель:** sonnet. **Файлы:**
- Create:
  - корень: `Directory.Build.props`, `global.json`, `.gitignore` (заменить), `Chrono.slnx`;
  - проект: `src/Chrono/Chrono.csproj` (заменить шаблонный), `src/Chrono/App.xaml`, `src/Chrono/App.xaml.cs`, `src/Chrono/AppShell.xaml`, `src/Chrono/AppShell.xaml.cs`, `src/Chrono/MauiProgram.cs`, `src/Chrono/MainPage.xaml`, `src/Chrono/MainPage.xaml.cs` (последние шесть — промежуточные версии);
  - ресурсы: `src/Chrono/Resources/Styles/{Colors,Styles}.xaml`, `src/Chrono/Resources/Strings/AppResources{,.ru}.resx`, `src/Chrono/Resources/AppIcon/{appicon,appiconfg}.svg`, `src/Chrono/Resources/Splash/splash.svg`, `src/Chrono/Resources/Images/bell.svg`, `src/Chrono/Resources/Fonts/Manrope-{Light,Regular,SemiBold}.ttf`, `licenses/Manrope-OFL.txt`;
  - платформы: `src/Chrono/Platforms/Android/AndroidManifest.xml`, `src/Chrono/Platforms/Android/Resources/values/colors.xml`, `src/Chrono/Platforms/Android/Resources/drawable/ic_stat_alarm.xml`, `src/Chrono/Platforms/iOS/Entitlements.plist`.
- Delete (из шаблона): `src/Chrono/Platforms/MacCatalyst/`, `src/Chrono/Platforms/Windows/`, `src/Chrono/Resources/Images/dotnet_bot.png`, `src/Chrono/Resources/Raw/AboutAssets.txt`, `src/Chrono/Resources/Fonts/OpenSans-*.ttf`, `src/Chrono/.gitignore`.

**Interfaces:**
- Consumes: ничего.
- Produces:
  - класс `Chrono.Resources.Strings.AppResources`, генерируется из resx: 42 ключа, список ниже;
  - алиасы шрифтов `ManropeLight`, `ManropeRegular`, `ManropeSemiBold`;
  - ресурсы цвета `BgDeep`, `BgMid`, `BgTop`, `Surface`, `SurfaceBorder`, `Accent`, `AccentEnd`, `OnAccent`, `Text`, `TextMuted`, `Danger`, `SwitchOff` и кисти `PageBackground`, `AccentBrush`;
  - стили `Muted`, `SectionHeader`, `Card`, `PrimaryButton`, `SecondaryButton`, `DangerLink`;
  - изображение `bell.png` (из `bell.svg`) и drawable `Resource.Drawable.ic_stat_alarm`.

- [ ] **Шаг 1: Проверить окружение.**
  Run: `dotnet --version` и `dotnet workload list`.
  Expected: `11.0.100-rc.1.26425.128`; в списке есть `android` и `ios`. Иначе остановиться и сообщить координатору. При сомнениях — skill `dotnet-maui:dotnet-maui-doctor`.

- [ ] **Шаг 2: Ветка.**
  Run: `git switch -c feature/chrono-v1`.
  Expected: `Switched to a new branch 'feature/chrono-v1'`.

- [ ] **Шаг 3: Шаблон MAUI.** В корне репозитория:
  Run: `dotnet new maui -n Chrono -o src/Chrono`.
  Expected: в `src/Chrono` появились `Chrono.csproj`, `App.xaml`, `Platforms/{Android,iOS,MacCatalyst,Windows}`.

- [ ] **Шаг 4: Удалить лишнее из шаблона** (PowerShell, точные пути):

```powershell
Remove-Item -LiteralPath "src/Chrono/Platforms/MacCatalyst" -Recurse -Force
Remove-Item -LiteralPath "src/Chrono/Platforms/Windows" -Recurse -Force
Remove-Item -LiteralPath "src/Chrono/Resources/Images/dotnet_bot.png" -Force
Remove-Item -LiteralPath "src/Chrono/Resources/Raw/AboutAssets.txt" -Force
Remove-Item -LiteralPath "src/Chrono/Resources/Fonts/OpenSans-Regular.ttf" -Force
Remove-Item -LiteralPath "src/Chrono/Resources/Fonts/OpenSans-Semibold.ttf" -Force
Remove-Item -LiteralPath "src/Chrono/.gitignore" -Force
```

- [ ] **Шаг 5: Файлы корня.**

Файл `Directory.Build.props`:

```xml
<Project>
  <!--
    obj/bin вынесены в ASCII-путь: aapt2 (Android) не открывает файлы, если в пути есть кириллица
    (ошибка APT2265), а репозиторий лежит в «Chrono-Будильник». Хеш пути проекта разводит сборки
    разных worktree и клонов по своим каталогам.
  -->
  <PropertyGroup>
    <ChronoBuildRoot>$(TEMP)\chrono-build\$([MSBuild]::StableStringHash($(MSBuildProjectDirectory)))\$(MSBuildProjectName)\</ChronoBuildRoot>
    <BaseIntermediateOutputPath>$(ChronoBuildRoot)obj\</BaseIntermediateOutputPath>
    <BaseOutputPath>$(ChronoBuildRoot)bin\</BaseOutputPath>
  </PropertyGroup>
</Project>
```

Файл `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Файл `.gitignore`:

```text
.superpowers/
bin/
obj/
*.user
.vs/
```

Файл `Chrono.slnx`:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Chrono/Chrono.csproj" />
  </Folder>
</Solution>
```


- [ ] **Шаг 6: Проект** (полностью заменяет шаблонный `Chrono.csproj`).

Файл `src/Chrono/Chrono.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<!-- Только Android и iOS: Windows и Mac Catalyst приложению не нужны. -->
		<TargetFrameworks>net11.0-android;net11.0-ios</TargetFrameworks>
		<OutputType>Exe</OutputType>
		<RootNamespace>Chrono</RootNamespace>
		<UseMaui>true</UseMaui>
		<SingleProject>true</SingleProject>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>

		<ApplicationTitle>Chrono</ApplicationTitle>
		<ApplicationId>com.chrono.alarm</ApplicationId>
		<ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
		<ApplicationVersion>1</ApplicationVersion>

		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">15.0</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">26.0</SupportedOSPlatformVersion>

		<!-- Мелодии из assets открываются через OpenFd — файлы не должны сжиматься в APK. -->
		<AndroidStoreUncompressedFileExtensions>.wav</AndroidStoreUncompressedFileExtensions>
	</PropertyGroup>

	<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
		<!-- Time-sensitive уведомления; capability включается и в App ID на developer.apple.com. -->
		<CodesignEntitlements>Platforms\iOS\Entitlements.plist</CodesignEntitlements>
	</PropertyGroup>

	<ItemGroup>
		<MauiIcon Include="Resources\AppIcon\appicon.svg" ForegroundFile="Resources\AppIcon\appiconfg.svg" />
		<MauiSplashScreen Include="Resources\Splash\splash.svg" Color="#0A0B1A" BaseSize="128,128" />
		<MauiImage Include="Resources\Images\*" />
		<MauiFont Include="Resources\Fonts\*" />
		<!-- Без подпапок: на iOS звук уведомления ищется в корне бандла, на Android — в корне assets. -->
		<MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
	</ItemGroup>

	<ItemGroup>
		<!-- Строго типизированный AppResources генерируется при сборке, без Visual Studio. -->
		<EmbeddedResource Update="Resources\Strings\AppResources.resx"
		                  StronglyTypedLanguage="CSharp"
		                  StronglyTypedClassName="AppResources"
		                  StronglyTypedNamespace="Chrono.Resources.Strings"
		                  StronglyTypedFileName="$(IntermediateOutputPath)AppResources.Designer.cs"
		                  PublicClass="true" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
		<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="11.0.0-*" />
		<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
	</ItemGroup>

</Project>
```


- [ ] **Шаг 7: Тема и строки.**

Файл `src/Chrono/Resources/Styles/Colors.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<!-- Токены темы «Ночное небо». Приложение всегда тёмное. -->
<ResourceDictionary>

    <Color x:Key="BgDeep">#0A0B1A</Color>
    <Color x:Key="BgMid">#12142E</Color>
    <Color x:Key="BgTop">#2A2F6B</Color>
    <!-- Поверхность карточек — белый 6 %, рамка — белый 8 %. -->
    <Color x:Key="Surface">#0FFFFFFF</Color>
    <Color x:Key="SurfaceBorder">#14FFFFFF</Color>
    <Color x:Key="Accent">#FFB35C</Color>
    <Color x:Key="AccentEnd">#FF7A59</Color>
    <Color x:Key="OnAccent">#1A0F08</Color>
    <Color x:Key="Text">#E9EBFF</Color>
    <Color x:Key="TextMuted">#9AA0D6</Color>
    <Color x:Key="Danger">#FF8F7A</Color>
    <Color x:Key="SwitchOff">#3A3E66</Color>

    <RadialGradientBrush x:Key="PageBackground" Center="0.8,0" Radius="1.2">
        <GradientStop Color="{StaticResource BgTop}" Offset="0" />
        <GradientStop Color="{StaticResource BgMid}" Offset="0.55" />
        <GradientStop Color="{StaticResource BgDeep}" Offset="1" />
    </RadialGradientBrush>

    <LinearGradientBrush x:Key="AccentBrush" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="{StaticResource Accent}" Offset="0" />
        <GradientStop Color="{StaticResource AccentEnd}" Offset="1" />
    </LinearGradientBrush>

</ResourceDictionary>
```

Файл `src/Chrono/Resources/Styles/Styles.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary>

    <Style TargetType="ContentPage" ApplyToDerivedTypes="True">
        <Setter Property="Background" Value="{StaticResource PageBackground}" />
        <Setter Property="Shell.NavBarIsVisible" Value="False" />
    </Style>

    <Style TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="FontFamily" Value="ManropeRegular" />
        <Setter Property="FontSize" Value="14" />
    </Style>

    <Style x:Key="Muted" TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="FontSize" Value="12" />
    </Style>

    <Style x:Key="SectionHeader" TargetType="Label">
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="11" />
        <Setter Property="CharacterSpacing" Value="1" />
        <Setter Property="TextTransform" Value="Uppercase" />
        <Setter Property="Margin" Value="4,14,0,6" />
    </Style>

    <Style x:Key="Card" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Surface}" />
        <Setter Property="Stroke" Value="{StaticResource SurfaceBorder}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="StrokeShape" Value="RoundRectangle 18" />
        <Setter Property="Padding" Value="16,12" />
    </Style>

    <Style TargetType="Switch">
        <Setter Property="OnColor" Value="{StaticResource Accent}" />
        <Setter Property="ThumbColor" Value="White" />
        <Setter Property="VisualStateManager.VisualStateGroups">
            <VisualStateGroupList>
                <VisualStateGroup x:Name="CommonStates">
                    <VisualState x:Name="Normal" />
                    <VisualState x:Name="Off">
                        <VisualState.Setters>
                            <Setter Property="ThumbColor" Value="White" />
                        </VisualState.Setters>
                    </VisualState>
                </VisualStateGroup>
            </VisualStateGroupList>
        </Setter>
    </Style>

    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource AccentBrush}" />
        <Setter Property="TextColor" Value="{StaticResource OnAccent}" />
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="CornerRadius" Value="16" />
        <Setter Property="Padding" Value="22,12" />
    </Style>

    <Style x:Key="SecondaryButton" TargetType="Button">
        <Setter Property="BackgroundColor" Value="{StaticResource Surface}" />
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="FontFamily" Value="ManropeSemiBold" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="CornerRadius" Value="16" />
        <Setter Property="Padding" Value="22,12" />
    </Style>

    <Style x:Key="DangerLink" TargetType="Button">
        <Setter Property="BackgroundColor" Value="Transparent" />
        <Setter Property="TextColor" Value="{StaticResource Danger}" />
        <Setter Property="FontFamily" Value="ManropeRegular" />
        <Setter Property="FontSize" Value="14" />
    </Style>

    <Style TargetType="Entry">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="PlaceholderColor" Value="{StaticResource TextMuted}" />
        <Setter Property="FontFamily" Value="ManropeRegular" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="BackgroundColor" Value="Transparent" />
    </Style>

    <Style TargetType="DatePicker">
        <Setter Property="TextColor" Value="{StaticResource Accent}" />
        <Setter Property="FontFamily" Value="ManropeRegular" />
        <Setter Property="BackgroundColor" Value="Transparent" />
    </Style>

    <Style TargetType="TimePicker">
        <Setter Property="TextColor" Value="{StaticResource Text}" />
        <Setter Property="FontFamily" Value="ManropeLight" />
        <Setter Property="BackgroundColor" Value="Transparent" />
    </Style>

</ResourceDictionary>
```

Файл `src/Chrono/Resources/Strings/AppResources.resx`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="AppName" xml:space="preserve"><value>Chrono</value></data>
  <data name="NextInHours" xml:space="preserve"><value>Next alarm in {0} h {1} min</value></data>
  <data name="NextInMinutes" xml:space="preserve"><value>Next alarm in {0} min</value></data>
  <data name="NoActiveAlarms" xml:space="preserve"><value>No active alarms</value></data>
  <data name="EmptyList" xml:space="preserve"><value>No alarms yet. Tap + to add one.</value></data>
  <data name="WarningBanner" xml:space="preserve"><value>Alarm may not ring — fix</value></data>
  <data name="NewAlarm" xml:space="preserve"><value>New alarm</value></data>
  <data name="EditAlarm" xml:space="preserve"><value>Edit alarm</value></data>
  <data name="TapToChange" xml:space="preserve"><value>tap the time or date to change</value></data>
  <data name="LabelHeader" xml:space="preserve"><value>Text</value></data>
  <data name="LabelPlaceholder" xml:space="preserve"><value>Alarm text</value></data>
  <data name="SignalHeader" xml:space="preserve"><value>Signal</value></data>
  <data name="Sound" xml:space="preserve"><value>Sound</value></data>
  <data name="Vibration" xml:space="preserve"><value>Vibration</value></data>
  <data name="DeleteAlarm" xml:space="preserve"><value>Delete alarm</value></data>
  <data name="Delete" xml:space="preserve"><value>Delete</value></data>
  <data name="Cancel" xml:space="preserve"><value>Cancel</value></data>
  <data name="Save" xml:space="preserve"><value>Save</value></data>
  <data name="Ok" xml:space="preserve"><value>OK</value></data>
  <data name="TimeInPast" xml:space="preserve"><value>This time has already passed</value></data>
  <data name="PickNewDate" xml:space="preserve"><value>This alarm's time has passed. Choose a new date.</value></data>
  <data name="DefaultLabel" xml:space="preserve"><value>Alarm</value></data>
  <data name="Stop" xml:space="preserve"><value>Stop</value></data>
  <data name="ChannelAlarms" xml:space="preserve"><value>Alarms</value></data>
  <data name="ChannelMissed" xml:space="preserve"><value>Missed alarms</value></data>
  <data name="MissedAlarm" xml:space="preserve"><value>Missed alarm {0}</value></data>
  <data name="CorruptFile" xml:space="preserve"><value>Couldn't read the alarm list. The damaged file was set aside.</value></data>
  <data name="SoundPickerTitle" xml:space="preserve"><value>Melody</value></data>
  <data name="Sound_bell" xml:space="preserve"><value>Bell</value></data>
  <data name="Sound_chime" xml:space="preserve"><value>Chime</value></data>
  <data name="Sound_digital" xml:space="preserve"><value>Digital</value></data>
  <data name="Sound_rising" xml:space="preserve"><value>Rising</value></data>
  <data name="Sound_soft" xml:space="preserve"><value>Soft</value></data>
  <data name="ReliabilityTitle" xml:space="preserve"><value>Reliability</value></data>
  <data name="Rel_Notifications" xml:space="preserve"><value>Notifications allowed</value></data>
  <data name="Rel_ExactAlarms" xml:space="preserve"><value>Exact alarms allowed</value></data>
  <data name="Rel_FullScreen" xml:space="preserve"><value>Full-screen alerts allowed</value></data>
  <data name="Rel_Battery" xml:space="preserve"><value>Battery optimization off</value></data>
  <data name="Rel_TimeSensitive" xml:space="preserve"><value>Time-sensitive notifications allowed</value></data>
  <data name="Fix" xml:space="preserve"><value>Fix</value></data>
  <data name="OemHint" xml:space="preserve"><value>On Xiaomi, Huawei and Samsung phones also allow Autostart for Chrono: Settings → Apps → Chrono → Autostart / Battery.</value></data>
  <data name="SilentHint" xml:space="preserve"><value>In silent mode only vibration will play.</value></data>
</root>
```

Файл `src/Chrono/Resources/Strings/AppResources.ru.resx`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="AppName" xml:space="preserve"><value>Chrono</value></data>
  <data name="NextInHours" xml:space="preserve"><value>Следующий через {0} ч {1} мин</value></data>
  <data name="NextInMinutes" xml:space="preserve"><value>Следующий через {0} мин</value></data>
  <data name="NoActiveAlarms" xml:space="preserve"><value>Нет активных будильников</value></data>
  <data name="EmptyList" xml:space="preserve"><value>Будильников пока нет. Нажмите +, чтобы добавить.</value></data>
  <data name="WarningBanner" xml:space="preserve"><value>Будильник может не сработать — исправить</value></data>
  <data name="NewAlarm" xml:space="preserve"><value>Новый будильник</value></data>
  <data name="EditAlarm" xml:space="preserve"><value>Изменить будильник</value></data>
  <data name="TapToChange" xml:space="preserve"><value>нажмите на время или дату, чтобы изменить</value></data>
  <data name="LabelHeader" xml:space="preserve"><value>Текст</value></data>
  <data name="LabelPlaceholder" xml:space="preserve"><value>Текст будильника</value></data>
  <data name="SignalHeader" xml:space="preserve"><value>Сигнал</value></data>
  <data name="Sound" xml:space="preserve"><value>Звук</value></data>
  <data name="Vibration" xml:space="preserve"><value>Вибрация</value></data>
  <data name="DeleteAlarm" xml:space="preserve"><value>Удалить будильник</value></data>
  <data name="Delete" xml:space="preserve"><value>Удалить</value></data>
  <data name="Cancel" xml:space="preserve"><value>Отмена</value></data>
  <data name="Save" xml:space="preserve"><value>Сохранить</value></data>
  <data name="Ok" xml:space="preserve"><value>ОК</value></data>
  <data name="TimeInPast" xml:space="preserve"><value>Это время уже прошло</value></data>
  <data name="PickNewDate" xml:space="preserve"><value>Время этого будильника прошло. Выберите новую дату.</value></data>
  <data name="DefaultLabel" xml:space="preserve"><value>Будильник</value></data>
  <data name="Stop" xml:space="preserve"><value>Стоп</value></data>
  <data name="ChannelAlarms" xml:space="preserve"><value>Будильники</value></data>
  <data name="ChannelMissed" xml:space="preserve"><value>Пропущенные будильники</value></data>
  <data name="MissedAlarm" xml:space="preserve"><value>Пропущенный будильник {0}</value></data>
  <data name="CorruptFile" xml:space="preserve"><value>Не удалось прочитать список будильников. Повреждённый файл сохранён отдельно.</value></data>
  <data name="SoundPickerTitle" xml:space="preserve"><value>Мелодия</value></data>
  <data name="Sound_bell" xml:space="preserve"><value>Колокол</value></data>
  <data name="Sound_chime" xml:space="preserve"><value>Перезвон</value></data>
  <data name="Sound_digital" xml:space="preserve"><value>Цифровой</value></data>
  <data name="Sound_rising" xml:space="preserve"><value>Нарастающий</value></data>
  <data name="Sound_soft" xml:space="preserve"><value>Мягкий</value></data>
  <data name="ReliabilityTitle" xml:space="preserve"><value>Надёжность</value></data>
  <data name="Rel_Notifications" xml:space="preserve"><value>Уведомления разрешены</value></data>
  <data name="Rel_ExactAlarms" xml:space="preserve"><value>Точные будильники разрешены</value></data>
  <data name="Rel_FullScreen" xml:space="preserve"><value>Полноэкранные уведомления разрешены</value></data>
  <data name="Rel_Battery" xml:space="preserve"><value>Оптимизация батареи отключена</value></data>
  <data name="Rel_TimeSensitive" xml:space="preserve"><value>Срочные уведомления разрешены</value></data>
  <data name="Fix" xml:space="preserve"><value>Исправить</value></data>
  <data name="OemHint" xml:space="preserve"><value>На телефонах Xiaomi, Huawei и Samsung также разрешите Chrono автозапуск: Настройки → Приложения → Chrono → Автозапуск / Батарея.</value></data>
  <data name="SilentHint" xml:space="preserve"><value>В беззвучном режиме будет только вибрация.</value></data>
</root>
```


- [ ] **Шаг 8: Иконка, splash, изображение колокола.**

Файл `src/Chrono/Resources/AppIcon/appicon.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="108" height="108" viewBox="0 0 108 108">
  <!-- Фон иконки: ночной радиальный градиент. -->
  <defs>
    <radialGradient id="bg" cx="0.75" cy="0.1" r="1.1">
      <stop offset="0" stop-color="#2F3577"/>
      <stop offset="0.55" stop-color="#14163A"/>
      <stop offset="1" stop-color="#0A0B1A"/>
    </radialGradient>
  </defs>
  <rect width="108" height="108" fill="url(#bg)"/>
</svg>
```

Файл `src/Chrono/Resources/AppIcon/appiconfg.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="108" height="108" viewBox="0 0 108 108">
  <!-- Передний слой: янтарный колокол в безопасной зоне adaptive icon (круг 66 из 108). -->
  <defs>
    <linearGradient id="amber" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FFC372"/>
      <stop offset="1" stop-color="#FF7A59"/>
    </linearGradient>
    <radialGradient id="glow" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="#FFB35C" stop-opacity="0.35"/>
      <stop offset="1" stop-color="#FFB35C" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <circle cx="54" cy="52" r="32" fill="url(#glow)"/>
  <path d="M54 30c-9 0-15.5 6.5-15.5 16.5v10l-5 6.5h41l-5-6.5v-10C69.5 36.5 63 30 54 30z" fill="url(#amber)"/>
  <circle cx="54" cy="69" r="4.2" fill="url(#amber)"/>
  <path d="M33 38q-4.5 7 0 14M75 38q4.5 7 0 14" stroke="#FFC372" stroke-width="2.6" fill="none" stroke-linecap="round"/>
</svg>
```

Файл `src/Chrono/Resources/Splash/splash.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="108" height="108" viewBox="0 0 108 108">
  <!-- Передний слой: янтарный колокол в безопасной зоне adaptive icon (круг 66 из 108). -->
  <defs>
    <linearGradient id="amber" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FFC372"/>
      <stop offset="1" stop-color="#FF7A59"/>
    </linearGradient>
    <radialGradient id="glow" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="#FFB35C" stop-opacity="0.35"/>
      <stop offset="1" stop-color="#FFB35C" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <circle cx="54" cy="52" r="32" fill="url(#glow)"/>
  <path d="M54 30c-9 0-15.5 6.5-15.5 16.5v10l-5 6.5h41l-5-6.5v-10C69.5 36.5 63 30 54 30z" fill="url(#amber)"/>
  <circle cx="54" cy="69" r="4.2" fill="url(#amber)"/>
  <path d="M33 38q-4.5 7 0 14M75 38q4.5 7 0 14" stroke="#FFC372" stroke-width="2.6" fill="none" stroke-linecap="round"/>
</svg>
```

Файл `src/Chrono/Resources/Images/bell.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="70" height="80" viewBox="0 0 70 80">
  <!-- Колокол для SplashPage и RingPage; ось качания — верх (AnchorY ≈ 0.08). -->
  <defs>
    <linearGradient id="amber" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FFC372"/>
      <stop offset="1" stop-color="#FF7A59"/>
    </linearGradient>
  </defs>
  <path d="M35 6c-12 0-20 9-20 22v14l-7 10h54l-7-10V28C55 15 47 6 35 6z" fill="url(#amber)"/>
  <circle cx="35" cy="62" r="6" fill="url(#amber)"/>
</svg>
```


- [ ] **Шаг 9: Шрифт Manrope.**
  Нужны статические TTF (не вариативные) начертаний Light, Regular и SemiBold с кириллицей. Источник — Google Fonts CSS API: со «старым» User-Agent он отдаёт по одному статическому TTF на начертание. Способ проверен 2026-10-06; ссылок на конкретные файлы в плане нет, потому что они меняются. Запустить из корня репозитория:

```python
# Скачивает Manrope Light/Regular/SemiBold (статические TTF с кириллицей) в src/Chrono/Resources/Fonts.
import re, urllib.request
for weight, name in ((300, "Manrope-Light"), (400, "Manrope-Regular"), (600, "Manrope-SemiBold")):
    req = urllib.request.Request(f"https://fonts.googleapis.com/css2?family=Manrope:wght@{weight}",
                                 headers={"User-Agent": "Mozilla/4.0"})
    url = re.findall(r"url\((https://[^)]+)\)", urllib.request.urlopen(req).read().decode())[0]
    data = urllib.request.urlopen(url).read()
    assert data[:4] in (b"\x00\x01\x00\x00", b"true"), f"{name}: не TrueType"
    open(f"src/Chrono/Resources/Fonts/{name}.ttf", "wb").write(data)
    print(name, len(data), "байт")
```

  Expected: три файла, каждый ≈ 95 000 байт.
  Критерий пригодности: TrueType, есть глифы «Ж» и «ё», нет таблицы `fvar`. Если есть `fontTools`, можно проверить им; если нет — достаточно рендера на шаге 13.
  Лицензия: положить текст SIL Open Font License 1.1 для Manrope (copyright «The Manrope Project Authors») в `licenses/Manrope-OFL.txt`. Источник — каталог `ofl/manrope` репозитория `google/fonts`. Файл обязательно лежит **вне** `Resources/Fonts`, иначе `MauiFont` попытается собрать его как шрифт.

- [ ] **Шаг 10: Платформенные ресурсы.**

Файл `src/Chrono/Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
	<application android:allowBackup="true" android:icon="@mipmap/appicon" android:roundIcon="@mipmap/appicon_round" android:supportsRtl="true"></application>
		<uses-permission android:name="android.permission.USE_EXACT_ALARM" />
	<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
	<uses-permission android:name="android.permission.USE_FULL_SCREEN_INTENT" />
	<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
	<uses-permission android:name="android.permission.FOREGROUND_SERVICE_SYSTEM_EXEMPTED" />
	<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />
	<uses-permission android:name="android.permission.VIBRATE" />
	<uses-permission android:name="android.permission.WAKE_LOCK" />
	<uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" android:maxSdkVersion="32" />
</manifest>
```

Файл `src/Chrono/Platforms/Android/Resources/values/colors.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="colorPrimary">#0A0B1A</color>
    <color name="colorPrimaryDark">#0A0B1A</color>
    <color name="colorAccent">#0A0B1A</color>
</resources>
```

Файл `src/Chrono/Platforms/Android/Resources/drawable/ic_stat_alarm.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Монохромный колокол для статус-бара (система перекрашивает в белый). -->
<vector xmlns:android="http://schemas.android.com/apk/res/android"
    android:width="24dp"
    android:height="24dp"
    android:viewportWidth="24"
    android:viewportHeight="24">
    <path
        android:fillColor="#FFFFFFFF"
        android:pathData="M12,2C8.7,2 6.5,4.6 6.5,8v4.2L4.6,15h14.8l-1.9,-2.8V8C17.5,4.6 15.3,2 12,2zM12,22c1.4,0 2.5,-1.1 2.5,-2.5h-5C9.5,20.9 10.6,22 12,22z" />
</vector>
```

Файл `src/Chrono/Platforms/iOS/Entitlements.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<!-- Уведомления будильника проходят сквозь режимы фокусировки. Одобрения Apple не требует. -->
	<key>com.apple.developer.usernotifications.time-sensitive</key>
	<true/>
</dict>
</plist>
```


- [ ] **Шаг 11: App и промежуточные файлы каркаса.**
  `App.xaml` — финальный. Остальные пять файлов — промежуточные: в задаче 7 их заменят, а `MainPage` удалят.

Файл `src/Chrono/App.xaml`:

```xml
<Application xmlns:local="clr-namespace:Chrono"
             x:Class="Chrono.App"
             xmlns:android="clr-namespace:Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;assembly=Microsoft.Maui.Controls"
             android:Application.WindowSoftInputModeAdjust="Resize">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
                <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Файл `src/Chrono/App.xaml.cs`:

```csharp
namespace Chrono;

// Промежуточная версия каркаса (задача 1). Финальная версия — в задаче 7.
public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
```

Файл `src/Chrono/AppShell.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<!-- Промежуточная версия каркаса (задача 1). Финальная версия — в задаче 7. -->
<Shell xmlns:local="clr-namespace:Chrono"
       x:Class="Chrono.AppShell"
       Shell.NavBarIsVisible="False">

    <ShellContent ContentTemplate="{DataTemplate local:MainPage}" Route="main" />

</Shell>
```

Файл `src/Chrono/AppShell.xaml.cs`:

```csharp
namespace Chrono;

// Промежуточная версия каркаса (задача 1). Финальная версия — в задаче 7.
public partial class AppShell : Shell
{
    public AppShell()
    {
        this.InitializeComponent();
    }
}
```

Файл `src/Chrono/MauiProgram.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace Chrono;

// Промежуточная версия каркаса (задача 1): только шрифты. Финальная версия — в задаче 7.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Light.ttf", "ManropeLight");
                fonts.AddFont("Manrope-Regular.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

Файл `src/Chrono/MainPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- Временная заглушка каркаса (задача 1). Удаляется в задаче 7. -->
<ContentPage xmlns:res="clr-namespace:Chrono.Resources.Strings"
             x:Class="Chrono.MainPage">
    <Label Text="{x:Static res:AppResources.AppName}" FontFamily="ManropeLight" FontSize="48"
           HorizontalOptions="Center" VerticalOptions="Center" />
</ContentPage>
```

Файл `src/Chrono/MainPage.xaml.cs`:

```csharp
namespace Chrono;

// Временная заглушка каркаса (задача 1). Удаляется в задаче 7.
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        this.InitializeComponent();
    }
}
```


- [ ] **Шаг 12: Сборка обеих платформ.**
  Run: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` и `dotnet build src/Chrono/Chrono.csproj -f net11.0-ios`.
  Expected: обе дают `0 Warning(s)`, `0 Error(s)`. Проверка смысла: `git status --short` не показывает `src/Chrono/obj` и `src/Chrono/bin`, потому что они вынесены в `%TEMP%\chrono-build`.

- [ ] **Шаг 13: Проверка рендером.**
  Run: установка по правилу из «Окружения», затем `adb shell monkey -p com.chrono.alarm -c android.intent.category.LAUNCHER 1`, через 3 с `adb exec-out screencap -p > s1.png`.
  Expected на скриншоте: надпись «Chrono» тонким шрифтом Manrope Light по центру, фон — тёмно-синий радиальный градиент. Если шрифт системный (толстый Roboto) — шрифты не подхватились, исправить.
  Затем свайп вверх на рабочем столе (`adb shell input swipe 540 1800 540 600 300`) и скриншот: иконка Chrono — янтарный колокол с «волнами» на тёмно-синем круге.

- [ ] **Шаг 14: Коммит.**

```bash
FILES="Directory.Build.props global.json .gitignore Chrono.slnx licenses/Manrope-OFL.txt src/Chrono/Chrono.csproj src/Chrono/App.xaml src/Chrono/App.xaml.cs src/Chrono/AppShell.xaml src/Chrono/AppShell.xaml.cs src/Chrono/MauiProgram.cs src/Chrono/MainPage.xaml src/Chrono/MainPage.xaml.cs src/Chrono/Properties/launchSettings.json src/Chrono/Resources/Styles/Colors.xaml src/Chrono/Resources/Styles/Styles.xaml src/Chrono/Resources/Strings/AppResources.resx src/Chrono/Resources/Strings/AppResources.ru.resx src/Chrono/Resources/AppIcon/appicon.svg src/Chrono/Resources/AppIcon/appiconfg.svg src/Chrono/Resources/Splash/splash.svg src/Chrono/Resources/Images/bell.svg src/Chrono/Resources/Fonts/Manrope-Light.ttf src/Chrono/Resources/Fonts/Manrope-Regular.ttf src/Chrono/Resources/Fonts/Manrope-SemiBold.ttf src/Chrono/Platforms/Android/AndroidManifest.xml src/Chrono/Platforms/Android/MainActivity.cs src/Chrono/Platforms/Android/MainApplication.cs src/Chrono/Platforms/Android/Resources/values/colors.xml src/Chrono/Platforms/Android/Resources/drawable/ic_stat_alarm.xml src/Chrono/Platforms/iOS/AppDelegate.cs src/Chrono/Platforms/iOS/Program.cs src/Chrono/Platforms/iOS/Info.plist src/Chrono/Platforms/iOS/Entitlements.plist src/Chrono/Platforms/iOS/Resources/PrivacyInfo.xcprivacy"
git add $FILES
git status --short
```

  Каждый блок команд выполняется отдельным вызовом оболочки, и переменные между вызовами не сохраняются, поэтому `FILES` задаётся в обоих блоках.
  Expected: в `git status --short` не осталось неотслеживаемых файлов под `src/Chrono`, кроме `obj/` и `bin/`, которых тоже быть не должно. Если что-то осталось, добавьте это точным путём.

```bash
FILES="Directory.Build.props global.json .gitignore Chrono.slnx licenses/Manrope-OFL.txt src/Chrono/Chrono.csproj src/Chrono/App.xaml src/Chrono/App.xaml.cs src/Chrono/AppShell.xaml src/Chrono/AppShell.xaml.cs src/Chrono/MauiProgram.cs src/Chrono/MainPage.xaml src/Chrono/MainPage.xaml.cs src/Chrono/Properties/launchSettings.json src/Chrono/Resources/Styles/Colors.xaml src/Chrono/Resources/Styles/Styles.xaml src/Chrono/Resources/Strings/AppResources.resx src/Chrono/Resources/Strings/AppResources.ru.resx src/Chrono/Resources/AppIcon/appicon.svg src/Chrono/Resources/AppIcon/appiconfg.svg src/Chrono/Resources/Splash/splash.svg src/Chrono/Resources/Images/bell.svg src/Chrono/Resources/Fonts/Manrope-Light.ttf src/Chrono/Resources/Fonts/Manrope-Regular.ttf src/Chrono/Resources/Fonts/Manrope-SemiBold.ttf src/Chrono/Platforms/Android/AndroidManifest.xml src/Chrono/Platforms/Android/MainActivity.cs src/Chrono/Platforms/Android/MainApplication.cs src/Chrono/Platforms/Android/Resources/values/colors.xml src/Chrono/Platforms/Android/Resources/drawable/ic_stat_alarm.xml src/Chrono/Platforms/iOS/AppDelegate.cs src/Chrono/Platforms/iOS/Program.cs src/Chrono/Platforms/iOS/Info.plist src/Chrono/Platforms/iOS/Entitlements.plist src/Chrono/Platforms/iOS/Resources/PrivacyInfo.xcprivacy"
git commit -m "Каркас Chrono: решение, проект MAUI, тема, строки, иконка, splash" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 2: Ядро логики и unit-тесты (TDD)

**Модель:** haiku. **Изоляция:** git worktree от `feature/chrono-v1` после задачи 1. **Skill:** `superpowers:test-driven-development`.

**Файлы:**
- Create:
  - тесты: `tests/Chrono.Tests/Chrono.Tests.csproj`, `tests/Chrono.Tests/TestDoubles.cs`, `tests/Chrono.Tests/AlarmStoreTests.cs`, `tests/Chrono.Tests/AlarmServiceTests.cs`, `tests/Chrono.Tests/IosNotificationPlanTests.cs`;
  - модели: `src/Chrono/Models/Alarm.cs`, `src/Chrono/Models/Sounds.cs`;
  - сервисы: `src/Chrono/Services/PlatformContracts.cs`, `src/Chrono/Services/AlarmStore.cs`, `src/Chrono/Services/AlarmService.cs`, `src/Chrono/Services/IosNotificationPlan.cs`.
- Modify: `Chrono.slnx` — добавить тестовый проект командой `dotnet sln`.

**Interfaces:**
- Consumes: каркас задачи 1.
- Produces (точные сигнатуры, на них опираются задачи 4–7):
  - `sealed record Alarm { Guid Id; DateTime At; string Label; bool SoundEnabled; string SoundId; bool VibrationEnabled; bool IsEnabled; }` — все свойства `init`; значения по умолчанию: `Id = Guid.NewGuid()`, `SoundEnabled = true`, `SoundId = Sounds.Default`, `VibrationEnabled = true`, `IsEnabled = true`.
  - `static class Sounds { const string Default = "bell"; IReadOnlyList<string> All; }`.
  - `interface IAlarmScheduler { void Sync(IReadOnlyList<Alarm>); void Cancel(Guid); void StopRinging(Guid); }`.
  - `interface ISoundPlayer { void Play(string soundId, bool loop); void Stop(); }`.
  - `enum ReliabilityItem { Notifications, ExactAlarms, FullScreen, Battery, TimeSensitive }`, `sealed record ReliabilityStatus(ReliabilityItem Item, bool Ok)`.
  - `interface IReliabilityChecks { Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync(); Task RequestPermissionsAsync(); void Fix(ReliabilityItem); }`.
  - `sealed class AlarmStore(string directory)`: `(List<Alarm> Alarms, bool WasCorrupt) Load()`, `void Save(IReadOnlyList<Alarm>)`.
  - `sealed class AlarmService(AlarmStore, IAlarmScheduler, TimeProvider)`:
    - методы `IReadOnlyList<Alarm> GetAll()`, `bool Save(Alarm)`, `bool Toggle(Guid id, bool enabled)`, `void Delete(Guid)`, `void RescheduleAll()`;
    - свойства `bool RecoveredFromCorruptFile`, `Exception? SchedulingError`.
  - `readonly record struct PlannedNotification(string Id, Alarm Alarm, DateTime FireAt)`.
  - `static class IosNotificationPlan`: `const int MaxAlarms = 16`, `IReadOnlyList<TimeSpan> Offsets`, `Build(IEnumerable<Alarm>, DateTime now)`, `ChainIds(Guid)`.

- [ ] **Шаг 1: Тестовый проект и тесты (сначала тесты).**

Файл `tests/Chrono.Tests/Chrono.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.1" />
    <Using Include="Xunit" />
  </ItemGroup>

  <!-- Логика без зависимостей от MAUI подключается ссылками на файлы приложения. -->
  <ItemGroup>
    <Compile Include="..\..\src\Chrono\Models\Alarm.cs" Link="Linked\Alarm.cs" />
    <Compile Include="..\..\src\Chrono\Models\Sounds.cs" Link="Linked\Sounds.cs" />
    <Compile Include="..\..\src\Chrono\Services\PlatformContracts.cs" Link="Linked\PlatformContracts.cs" />
    <Compile Include="..\..\src\Chrono\Services\AlarmStore.cs" Link="Linked\AlarmStore.cs" />
    <Compile Include="..\..\src\Chrono\Services\AlarmService.cs" Link="Linked\AlarmService.cs" />
    <Compile Include="..\..\src\Chrono\Services\IosNotificationPlan.cs" Link="Linked\IosNotificationPlan.cs" />
  </ItemGroup>

</Project>
```

Файл `tests/Chrono.Tests/TestDoubles.cs`:

```csharp
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

/// <summary>Часы с фиксированным моментом; локальный пояс — UTC, чтобы «настенное» время совпадало с моментом.</summary>
public sealed class FixedTime(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(this.Now, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Запоминает вызовы планировщика; при ThrowOnSync = true бросает из Sync.</summary>
public sealed class FakeScheduler : IAlarmScheduler
{
    public List<IReadOnlyList<Alarm>> Synced { get; } = [];

    public List<Guid> Cancelled { get; } = [];

    public bool ThrowOnSync { get; set; }

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        if (this.ThrowOnSync)
        {
            throw new InvalidOperationException("планировщик недоступен");
        }

        this.Synced.Add(alarms.ToList());
    }

    public void Cancel(Guid id) => this.Cancelled.Add(id);

    public void StopRinging(Guid id)
    {
    }
}
```

Файл `tests/Chrono.Tests/AlarmStoreTests.cs`:

```csharp
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class AlarmStoreTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-store-");

    public void Dispose() => this.directory.Delete(recursive: true);

    [Fact]
    public void Load_MissingFile_ReturnsEmptyAndNotCorrupt()
    {
        var store = new AlarmStore(this.directory.FullName);

        var (alarms, wasCorrupt) = store.Load();

        Assert.Empty(alarms);
        Assert.False(wasCorrupt);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFieldsAndWallClockTime()
    {
        var store = new AlarmStore(this.directory.FullName);
        var alarm = new Alarm
        {
            At = new DateTime(2026, 10, 7, 6, 30, 0),
            Label = "Подъём",
            SoundEnabled = false,
            SoundId = "chime",
            VibrationEnabled = true,
            IsEnabled = true,
        };

        store.Save([alarm]);
        var (alarms, wasCorrupt) = store.Load();

        Assert.False(wasCorrupt);
        var loaded = Assert.Single(alarms);
        Assert.Equal(alarm, loaded);
        // «Настенное» время не сдвигается при сериализации.
        Assert.Equal(new DateTime(2026, 10, 7, 6, 30, 0), loaded.At);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new AlarmStore(this.directory.FullName);

        store.Save([new Alarm { At = new DateTime(2026, 10, 7, 6, 30, 0) }]);

        Assert.Equal(["alarms.json"], this.directory.GetFiles().Select(f => f.Name));
    }

    [Fact]
    public void Load_CorruptFile_MovesItAsideAndReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(this.directory.FullName, "alarms.json"), "{ это не json");
        var store = new AlarmStore(this.directory.FullName);

        var (alarms, wasCorrupt) = store.Load();

        Assert.Empty(alarms);
        Assert.True(wasCorrupt);
        Assert.False(File.Exists(Path.Combine(this.directory.FullName, "alarms.json")));
        var corrupt = Assert.Single(this.directory.GetFiles("alarms.corrupt-*.json"));
        Assert.Equal("{ это не json", File.ReadAllText(corrupt.FullName));
    }
}
```

Файл `tests/Chrono.Tests/AlarmServiceTests.cs`:

```csharp
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class AlarmServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-service-");
    private readonly FakeScheduler scheduler = new();
    private readonly FixedTime time = new(Now);
    private readonly AlarmStore store;
    private readonly AlarmService service;

    public AlarmServiceTests()
    {
        this.store = new AlarmStore(this.directory.FullName);
        this.service = new AlarmService(this.store, this.scheduler, this.time);
    }

    public void Dispose() => this.directory.Delete(recursive: true);

    [Fact]
    public void Save_PastTime_ReturnsFalseAndStoresNothing()
    {
        var saved = this.service.Save(new Alarm { At = Now.AddMinutes(-1) });

        Assert.False(saved);
        Assert.Empty(this.store.Load().Alarms);
        Assert.Empty(this.scheduler.Synced);
    }

    [Fact]
    public void Save_ExactlyNow_IsRejectedAsPast()
    {
        Assert.False(this.service.Save(new Alarm { At = Now }));
    }

    [Fact]
    public void Save_Future_StoresAndSyncsSchedulerWithIt()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Label = "Встреча" };

        var saved = this.service.Save(alarm);

        Assert.True(saved);
        Assert.Equal([alarm], this.store.Load().Alarms);
        Assert.Equal([alarm], Assert.Single(this.scheduler.Synced));
    }

    [Fact]
    public void Save_LocalKind_IsStoredAsWallClockWithoutOffset()
    {
        // DateTime.Today + TimeSpan даёт Kind=Local — так формирует время редактор.
        var at = DateTime.SpecifyKind(Now.AddHours(2), DateTimeKind.Local);

        this.service.Save(new Alarm { At = at });

        var json = File.ReadAllText(Path.Combine(this.directory.FullName, "alarms.json"));
        Assert.Contains("\"At\": \"2026-10-06T14:00:00\"", json);
        Assert.Equal(DateTimeKind.Unspecified, this.store.Load().Alarms.Single().At.Kind);
    }

    [Fact]
    public void Save_ExistingId_ReplacesInsteadOfDuplicating()
    {
        var alarm = new Alarm { At = Now.AddHours(1), Label = "Старое" };
        this.service.Save(alarm);

        this.service.Save(alarm with { Label = "Новое", At = Now.AddHours(2) });

        var stored = Assert.Single(this.store.Load().Alarms);
        Assert.Equal("Новое", stored.Label);
        Assert.Equal(Now.AddHours(2), stored.At);
    }

    [Fact]
    public void GetAll_DisablesFiredAlarmsAndPersistsIt()
    {
        var fired = new Alarm { At = Now.AddMinutes(5) };
        var future = new Alarm { At = Now.AddHours(5) };
        this.service.Save(fired);
        this.service.Save(future);
        this.time.Now = Now.AddMinutes(5);

        var all = this.service.GetAll();

        Assert.False(all.Single(a => a.Id == fired.Id).IsEnabled);
        Assert.True(all.Single(a => a.Id == future.Id).IsEnabled);
        Assert.False(this.store.Load().Alarms.Single(a => a.Id == fired.Id).IsEnabled);
    }

    [Fact]
    public void GetAll_ReturnsAlarmsOrderedByTime()
    {
        var late = new Alarm { At = Now.AddHours(3) };
        var early = new Alarm { At = Now.AddHours(1) };
        this.service.Save(late);
        this.service.Save(early);

        Assert.Equal([early.Id, late.Id], this.service.GetAll().Select(a => a.Id));
    }

    [Fact]
    public void Toggle_EnablePastAlarm_ReturnsFalseAndKeepsItDisabled()
    {
        var alarm = new Alarm { At = Now.AddMinutes(5) };
        this.service.Save(alarm);
        this.time.Now = Now.AddMinutes(10);

        var toggled = this.service.Toggle(alarm.Id, enabled: true);

        Assert.False(toggled);
        Assert.False(this.store.Load().Alarms.Single().IsEnabled);
    }

    [Fact]
    public void Toggle_Disable_StoresAndSyncsDisabledAlarm()
    {
        var alarm = new Alarm { At = Now.AddHours(1) };
        this.service.Save(alarm);

        var toggled = this.service.Toggle(alarm.Id, enabled: false);

        Assert.True(toggled);
        Assert.False(this.store.Load().Alarms.Single().IsEnabled);
        Assert.False(this.scheduler.Synced[^1].Single().IsEnabled);
    }

    [Fact]
    public void Toggle_UnknownId_ReturnsFalse()
    {
        Assert.False(this.service.Toggle(Guid.NewGuid(), enabled: true));
    }

    [Fact]
    public void Delete_CancelsAndRemoves()
    {
        var keep = new Alarm { At = Now.AddHours(1) };
        var remove = new Alarm { At = Now.AddHours(2) };
        this.service.Save(keep);
        this.service.Save(remove);

        this.service.Delete(remove.Id);

        Assert.Equal([remove.Id], this.scheduler.Cancelled);
        Assert.Equal([keep], this.store.Load().Alarms);
        Assert.Equal([keep], this.scheduler.Synced[^1]);
    }

    [Fact]
    public void Save_SchedulerThrows_AlarmIsStillSavedAndErrorExposed()
    {
        this.scheduler.ThrowOnSync = true;
        var alarm = new Alarm { At = Now.AddHours(1) };

        var saved = this.service.Save(alarm);

        Assert.True(saved);
        Assert.Equal([alarm], this.store.Load().Alarms);
        Assert.IsType<InvalidOperationException>(this.service.SchedulingError);
    }

    [Fact]
    public void SchedulingError_ClearsAfterSuccessfulSync()
    {
        this.scheduler.ThrowOnSync = true;
        this.service.Save(new Alarm { At = Now.AddHours(1) });
        this.scheduler.ThrowOnSync = false;

        this.service.RescheduleAll();

        Assert.Null(this.service.SchedulingError);
    }

    [Fact]
    public void GetAll_CorruptFile_ReportsRecovery()
    {
        File.WriteAllText(Path.Combine(this.directory.FullName, "alarms.json"), "[[[");

        var all = this.service.GetAll();

        Assert.Empty(all);
        Assert.True(this.service.RecoveredFromCorruptFile);
    }
}
```

Файл `tests/Chrono.Tests/IosNotificationPlanTests.cs`:

```csharp
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class IosNotificationPlanTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0);

    [Fact]
    public void Build_OneAlarm_GivesFourNotificationsAt0_30_60_90Seconds()
    {
        var alarm = new Alarm { At = Now.AddHours(1) };

        var plan = IosNotificationPlan.Build([alarm], Now);

        Assert.Equal(
            [alarm.At, alarm.At.AddSeconds(30), alarm.At.AddSeconds(60), alarm.At.AddSeconds(90)],
            plan.Select(p => p.FireAt));
        Assert.Equal(IosNotificationPlan.ChainIds(alarm.Id), plan.Select(p => p.Id));
    }

    [Fact]
    public void Build_SkipsDisabledAndPastAlarms()
    {
        var disabled = new Alarm { At = Now.AddHours(1), IsEnabled = false };
        var past = new Alarm { At = Now.AddMinutes(-1) };
        var atNow = new Alarm { At = Now };
        var future = new Alarm { At = Now.AddHours(2) };

        var plan = IosNotificationPlan.Build([disabled, past, atNow, future], Now);

        Assert.All(plan, p => Assert.Equal(future.Id, p.Alarm.Id));
    }

    [Fact]
    public void Build_KeepsOnlySixteenNearestAlarms_SixtyFourNotifications()
    {
        // Перемешанный порядок: план обязан выбрать ближайшие, а не первые по списку.
        var alarms = Enumerable.Range(1, 20).Reverse().Select(h => new Alarm { At = Now.AddHours(h) }).ToList();

        var plan = IosNotificationPlan.Build(alarms, Now);

        Assert.Equal(64, plan.Count);
        Assert.Equal(Now.AddHours(16).AddSeconds(90), plan.Max(p => p.FireAt));
    }
}
```


- [ ] **Шаг 2: Убедиться, что тесты не собираются без реализации.**
  Run: `dotnet test tests/Chrono.Tests/Chrono.Tests.csproj`.
  Expected: ошибки сборки — связанных файлов ещё нет (`CS2001: Source file ... Alarm.cs could not be found`) или `CS0246`.

- [ ] **Шаг 3: Реализация.**

Файл `src/Chrono/Models/Alarm.cs`:

```csharp
namespace Chrono.Models;

/// <summary>Разовый будильник.</summary>
public sealed record Alarm
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Локальное «настенное» время срабатывания (Kind = Unspecified или Local).</summary>
    public DateTime At { get; init; }

    public string Label { get; init; } = "";

    public bool SoundEnabled { get; init; } = true;

    public string SoundId { get; init; } = Sounds.Default;

    public bool VibrationEnabled { get; init; } = true;

    public bool IsEnabled { get; init; } = true;
}
```

Файл `src/Chrono/Models/Sounds.cs`:

```csharp
namespace Chrono.Models;

/// <summary>
/// Каталог встроенных мелодий. Id совпадает с именем файла Resources/Raw/{id}.wav
/// и с суффиксом ключа строки Sound_{id} в AppResources.
/// </summary>
public static class Sounds
{
    public const string Default = "bell";

    public static readonly IReadOnlyList<string> All = ["bell", "chime", "digital", "rising", "soft"];
}
```

Файл `src/Chrono/Services/PlatformContracts.cs`:

```csharp
using Chrono.Models;

namespace Chrono.Services;

/// <summary>Единственная граница с планировщиком ОС; реализация — в Platforms/*.</summary>
public interface IAlarmScheduler
{
    /// <summary>Привести расписание ОС к списку: включённые будущие — запланировать, остальные — снять.</summary>
    void Sync(IReadOnlyList<Alarm> alarms);

    /// <summary>Снять расписание удалённого будильника.</summary>
    void Cancel(Guid id);

    /// <summary>Остановить звучащий сигнал (звук, вибрацию, хвост уведомлений).</summary>
    void StopRinging(Guid id);
}

/// <summary>Воспроизведение встроенной мелодии (прослушивание в редакторе, сигнал на iOS на переднем плане).</summary>
public interface ISoundPlayer
{
    void Play(string soundId, bool loop);

    void Stop();
}

public enum ReliabilityItem
{
    Notifications,
    ExactAlarms,
    FullScreen,
    Battery,
    TimeSensitive,
}

public sealed record ReliabilityStatus(ReliabilityItem Item, bool Ok);

/// <summary>Проверки «Надёжность»: разрешения и настройки, без которых будильник может не сработать.</summary>
public interface IReliabilityChecks
{
    Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync();

    Task RequestPermissionsAsync();

    void Fix(ReliabilityItem item);
}
```

Файл `src/Chrono/Services/AlarmStore.cs`:

```csharp
using System.Text.Json;
using Chrono.Models;

namespace Chrono.Services;

/// <summary>
/// Хранение будильников в alarms.json. Запись атомарная: временный файл + File.Move.
/// Все вызовы — с главного потока (UI, receiver'ы и сервис Android работают на нём же).
/// </summary>
public sealed class AlarmStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string directory;
    private readonly string path;

    public AlarmStore(string directory)
    {
        this.directory = directory;
        this.path = Path.Combine(directory, "alarms.json");
    }

    /// <summary>Читает список. Повреждённый файл переименовывается, возвращается пустой список и WasCorrupt = true.</summary>
    public (List<Alarm> Alarms, bool WasCorrupt) Load()
    {
        if (!File.Exists(this.path))
        {
            return ([], false);
        }

        try
        {
            var alarms = JsonSerializer.Deserialize<List<Alarm>>(File.ReadAllText(this.path), JsonOptions);
            return (alarms ?? [], false);
        }
        catch (JsonException)
        {
            // Данные не теряем: откладываем файл в сторону для ручного разбора.
            var corruptPath = Path.Combine(this.directory, $"alarms.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(this.path, corruptPath, overwrite: true);
            return ([], true);
        }
    }

    public void Save(IReadOnlyList<Alarm> alarms)
    {
        var tempPath = this.path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(alarms, JsonOptions));
        File.Move(tempPath, this.path, overwrite: true);
    }
}
```

Файл `src/Chrono/Services/AlarmService.cs`:

```csharp
using Chrono.Models;

namespace Chrono.Services;

/// <summary>API будильников для UI и receiver'ов: store + синхронизация с планировщиком ОС.</summary>
public sealed class AlarmService
{
    private readonly AlarmStore store;
    private readonly IAlarmScheduler scheduler;
    private readonly TimeProvider time;

    public AlarmService(AlarmStore store, IAlarmScheduler scheduler, TimeProvider time)
    {
        this.store = store;
        this.scheduler = scheduler;
        this.time = time;
    }

    /// <summary>true, если при последнем чтении файл оказался повреждён и был отложен в сторону.</summary>
    public bool RecoveredFromCorruptFile { get; private set; }

    /// <summary>Последняя ошибка планировщика ОС (null — ошибок не было). Будильник при этом сохранён.</summary>
    public Exception? SchedulingError { get; private set; }

    /// <summary>Все будильники по времени. Сработавшие (включённые с At ≤ now) переводятся в выключенные и сохраняются.</summary>
    public IReadOnlyList<Alarm> GetAll()
    {
        var (alarms, wasCorrupt) = this.store.Load();
        this.RecoveredFromCorruptFile = wasCorrupt;

        var now = this.time.GetLocalNow().DateTime;
        var changed = false;
        for (var i = 0; i < alarms.Count; i++)
        {
            if (alarms[i].IsEnabled && alarms[i].At <= now)
            {
                alarms[i] = alarms[i] with { IsEnabled = false };
                changed = true;
            }
        }

        if (changed)
        {
            this.store.Save(alarms);
        }

        return alarms.OrderBy(a => a.At).ToList();
    }

    /// <summary>Создать или заменить будильник. false — время уже прошло, ничего не сохранено.</summary>
    public bool Save(Alarm alarm)
    {
        // «Настенное» время храним без пояса: Kind=Local сериализуется со смещением,
        // и после смены часового пояса 06:30 превратилось бы в другой час.
        alarm = alarm with { At = DateTime.SpecifyKind(alarm.At, DateTimeKind.Unspecified) };
        if (alarm.At <= this.time.GetLocalNow().DateTime)
        {
            return false;
        }

        var alarms = this.GetAll().ToList();
        var index = alarms.FindIndex(a => a.Id == alarm.Id);
        if (index >= 0)
        {
            alarms[index] = alarm;
        }
        else
        {
            alarms.Add(alarm);
        }

        this.store.Save(alarms);

        try
        {
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            // Будильник уже сохранён; UI покажет плашку «Будильник может не сработать».
            this.SchedulingError = ex;
        }

        return true;
    }

    /// <summary>Включить или выключить. false — включение отклонено: время уже прошло.</summary>
    public bool Toggle(Guid id, bool enabled)
    {
        var alarms = this.GetAll().ToList();
        var index = alarms.FindIndex(a => a.Id == id);
        if (index < 0)
        {
            return false;
        }

        if (enabled && alarms[index].At <= this.time.GetLocalNow().DateTime)
        {
            return false;
        }

        alarms[index] = alarms[index] with { IsEnabled = enabled };
        this.store.Save(alarms);

        try
        {
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }

        return true;
    }

    public void Delete(Guid id)
    {
        var alarms = this.GetAll().Where(a => a.Id != id).ToList();
        this.store.Save(alarms);

        try
        {
            this.scheduler.Cancel(id);
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }
    }

    /// <summary>Переустановить всё расписание (старт приложения, перезагрузка, смена времени/пояса).</summary>
    public void RescheduleAll()
    {
        try
        {
            this.scheduler.Sync(this.GetAll());
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }
    }
}
```

Файл `src/Chrono/Services/IosNotificationPlan.cs`:

```csharp
using Chrono.Models;

namespace Chrono.Services;

public readonly record struct PlannedNotification(string Id, Alarm Alarm, DateTime FireAt);

/// <summary>
/// Цепочка уведомлений iOS: 4 уведомления на будильник (0/30/60/90 с), не более 16 ближайших будильников —
/// iOS хранит не более 64 запланированных уведомлений.
/// </summary>
public static class IosNotificationPlan
{
    public const int MaxAlarms = 16;

    public static readonly IReadOnlyList<TimeSpan> Offsets =
        [TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90)];

    public static IReadOnlyList<PlannedNotification> Build(IEnumerable<Alarm> alarms, DateTime now) =>
        alarms
            .Where(a => a.IsEnabled && a.At > now)
            .OrderBy(a => a.At)
            .Take(MaxAlarms)
            .SelectMany(a => Offsets.Select((offset, i) => new PlannedNotification($"{a.Id}-{i}", a, a.At + offset)))
            .ToList();

    /// <summary>Идентификаторы всех уведомлений цепочки будильника.</summary>
    public static IReadOnlyList<string> ChainIds(Guid alarmId) =>
        Enumerable.Range(0, Offsets.Count).Select(i => $"{alarmId}-{i}").ToList();
}
```


- [ ] **Шаг 4: Тесты зелёные.**
  Run: `dotnet test tests/Chrono.Tests/Chrono.Tests.csproj`.
  Expected: `total: 21`, `failed: 0`, `succeeded: 21`.

- [ ] **Шаг 5: Доказательство мутацией (обязательно).**
  Каждую мутацию внести, прогнать тесты, вернуть файл. **После возврата пересобирать с `--no-incremental`**: файл, возвращённый копированием, сохраняет старое время изменения, и MSBuild оставляет DLL с мутацией. Так тест «зеленеет» на сломанном коде — проверено.

| # | Файл | Заменить | На | Ожидается |
|---|---|---|---|---|
| M1 | `AlarmService.cs` | `if (alarm.At <= this.time` | `if (alarm.At < this.time` | падает `Save_ExactlyNow_IsRejectedAsPast` |
| M2 | `AlarmService.cs` | `if (changed)` | `if (false && changed)` | падают 2 теста (`GetAll_DisablesFiredAlarmsAndPersistsIt` и др.) |
| M3 | `AlarmService.cs` | `this.scheduler.Cancel(id);` | `` (пусто) | падает `Delete_CancelsAndRemoves` |
| M4 | `AlarmService.cs` | `if (enabled && alarms[index].At` | `if (false && alarms[index].At` | падает `Toggle_EnablePastAlarm_ReturnsFalseAndKeepsItDisabled` |
| M5 | `IosNotificationPlan.cs` | `.OrderBy(a => a.At)` | `` (пусто) | падает `Build_KeepsOnlySixteenNearestAlarms_SixtyFourNotifications` |
| M6 | `AlarmStore.cs` | `File.Move(this.path, corruptPath, overwrite: true);` | `` (пусто) | падает `Load_CorruptFile_MovesItAsideAndReturnsEmpty` |
| M7 | `AlarmService.cs` | `alarm = alarm with { At = DateTime.SpecifyKind(alarm.At, DateTimeKind.Unspecified) };` | `` (пусто) | падает `Save_LocalKind_IsStoredAsWallClockWithoutOffset` |

  Готовый прогон всех мутаций (Git Bash, из корня worktree):

```bash
mut() { f=$1; cp "$f" "$f.bak"; python -c "import sys;p,a,b=sys.argv[1:];s=open(p,encoding='utf-8').read();assert a in s,'нет фрагмента';open(p,'w',encoding='utf-8').write(s.replace(a,b,1))" "$f" "$2" "$3" || { mv "$f.bak" "$f"; return; }
  r=$(dotnet build tests/Chrono.Tests/Chrono.Tests.csproj --no-incremental -v:q >/dev/null 2>&1; dotnet test tests/Chrono.Tests/Chrono.Tests.csproj --no-build 2>&1 | grep -E "failed:" | head -1); mv "$f.bak" "$f"; echo "[$4] $r"; }
S=src/Chrono/Services
mut $S/AlarmService.cs "        if (alarm.At <= this.time" "        if (alarm.At < this.time" M1
mut $S/AlarmService.cs "        if (changed)" "        if (false && changed)" M2
mut $S/AlarmService.cs "            this.scheduler.Cancel(id);" "" M3
mut $S/AlarmService.cs "        if (enabled && alarms[index].At" "        if (false && alarms[index].At" M4
mut $S/IosNotificationPlan.cs "            .OrderBy(a => a.At)" "" M5
mut $S/AlarmStore.cs "            File.Move(this.path, corruptPath, overwrite: true);" "" M6
mut $S/AlarmService.cs "        alarm = alarm with { At = DateTime.SpecifyKind(alarm.At, DateTimeKind.Unspecified) };" "" M7
dotnet build tests/Chrono.Tests/Chrono.Tests.csproj --no-incremental -v:q >/dev/null; dotnet test tests/Chrono.Tests/Chrono.Tests.csproj --no-build 2>&1 | grep -E "succeeded:|failed:"
ls src/Chrono/Services/*.bak 2>/dev/null
```

  Expected:
  - каждая строка `[Mx]` показывает `failed: ≥1`;
  - финальный прогон — `failed: 0`, `succeeded: 21`;
  - `ls src/Chrono/Services/*.bak` ничего не находит, содержимое файлов совпадает с планом. Файлы ещё не закоммичены, поэтому `git diff` здесь не помогает.
  Если какая-то мутация не уронила тест, тест ничего не доказывает. Сообщите координатору.

- [ ] **Шаг 6: Тестовый проект в решение и сборка приложения.**
  Run: `dotnet sln Chrono.slnx add tests/Chrono.Tests/Chrono.Tests.csproj`, затем сборка обеих платформ приложения.
  Expected: `0 Warning(s)`, `0 Error(s)` для `net11.0-android` и `net11.0-ios`.

- [ ] **Шаг 7: Коммит** (в ветку своего worktree).

```bash
FILES="Chrono.slnx tests/Chrono.Tests/Chrono.Tests.csproj tests/Chrono.Tests/TestDoubles.cs tests/Chrono.Tests/AlarmStoreTests.cs tests/Chrono.Tests/AlarmServiceTests.cs tests/Chrono.Tests/IosNotificationPlanTests.cs src/Chrono/Models/Alarm.cs src/Chrono/Models/Sounds.cs src/Chrono/Services/PlatformContracts.cs src/Chrono/Services/AlarmStore.cs src/Chrono/Services/AlarmService.cs src/Chrono/Services/IosNotificationPlan.cs"
git add $FILES
git commit -m "Ядро будильников: модель, хранилище, сервис, план уведомлений iOS и unit-тесты" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 3: Встроенные мелодии

**Модель:** haiku. **Изоляция:** git worktree. **Файлы:**
- Create: `tools/gen_sounds.py`, `src/Chrono/Resources/Raw/{bell,chime,digital,rising,soft}.wav`.

**Interfaces:**
- Produces: файлы `<id>.wav` для id из `Sounds.All` (`bell`, `chime`, `digital`, `rising`, `soft`). На Android они попадают в корень assets, на iOS — в корень бандла.

- [ ] **Шаг 1: Скрипт.**

Файл `tools/gen_sounds.py`:

```python
"""Синтез встроенных мелодий Chrono: 5 файлов WAV (PCM 16 бит, моно, 22 050 Гц, 20 с).

Только стандартная библиотека Python. Лицензионно чисто: звуки создаются математически.
Запуск из корня репозитория:  python tools/gen_sounds.py
Результат: src/Chrono/Resources/Raw/{bell,chime,digital,rising,soft}.wav
"""
import math
import os
import struct
import sys
import wave

RATE = 22050
DURATION = 20.0  # iOS проигрывает звук уведомления не дольше 30 с
OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join("src", "Chrono", "Resources", "Raw")


def strike(t, freq, partials, decay):
    """Удар с затуханием: сумма обертонов (множитель частоты, амплитуда)."""
    if t < 0:
        return 0.0
    env = math.exp(-decay * t) * min(1.0, t * 400)  # 2.5 мс атака без щелчка
    return env * sum(a * math.sin(2 * math.pi * freq * m * t) for m, a in partials)


BELL = [(1.0, 1.0), (2.0, 0.6), (2.76, 0.4), (5.4, 0.25), (8.93, 0.1)]
SINE = [(1.0, 1.0), (2.0, 0.15)]
MARIMBA = [(1.0, 1.0), (4.0, 0.2)]


def bell(t):
    return strike(t % 2.0, 660, BELL, 2.2)


def chime(t):
    notes = [1046.5, 1318.5, 1568.0, 2093.0]  # до-ми-соль-до
    cycle = t % 3.0
    return sum(strike(cycle - i * 0.25, f, SINE, 3.0) for i, f in enumerate(notes))


def digital(t):
    cycle = t % 1.6
    beep = 0 <= cycle % 0.25 < 0.12 and cycle < 1.0  # четыре коротких сигнала и пауза
    if not beep:
        return 0.0
    # Мягкий «квадрат»: нечётные гармоники с убыванием.
    return sum(math.sin(2 * math.pi * 2000 * k * t) / k for k in (1, 3, 5)) * 0.8


def rising(t):
    gain = 0.15 + 0.85 * min(1.0, t / DURATION)  # громкость растёт до конца файла
    cycle = t % 1.0
    return gain * (strike(cycle, 880, SINE, 4.0) + strike(cycle - 0.5, 1175, SINE, 4.0))


def soft(t):
    notes = [440.0, 523.25, 659.25, 783.99, 659.25, 523.25]  # пентатоника вверх-вниз
    cycle = t % 3.6
    return sum(strike(cycle - i * 0.6, f, MARIMBA, 2.5) for i, f in enumerate(notes))


def render(name, fn):
    frames = [fn(i / RATE) for i in range(int(RATE * DURATION))]
    peak = max(abs(x) for x in frames) or 1.0
    scale = 0.8 * 32767 / peak  # нормализация до -2 dBFS
    path = os.path.join(OUT_DIR, f"{name}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(x * scale)) for x in frames))
    print(f"{path}: {os.path.getsize(path)} байт")


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    for sound_name, sound_fn in [("bell", bell), ("chime", chime), ("digital", digital), ("rising", rising), ("soft", soft)]:
        render(sound_name, sound_fn)
```


- [ ] **Шаг 2: Сгенерировать.**
  Run (из корня): `python tools/gen_sounds.py`.
  Expected: пять строк `src\Chrono\Resources\Raw\<id>.wav: 882044 байт`.

- [ ] **Шаг 3: Проверить параметры.**
  Run:

```bash
python -c "import wave,glob;[print(p,w.getnchannels(),w.getsampwidth()*8,w.getframerate(),round(w.getnframes()/w.getframerate(),1)) for p in sorted(glob.glob('src/Chrono/Resources/Raw/*.wav')) for w in [wave.open(p)]]"
```

  Expected: у каждого из 5 файлов `1 16 22050 20.0` — моно, 16 бит, 22 050 Гц, 20 с. Это не больше 30 с, предела iOS для звука уведомления.

- [ ] **Шаг 4: Сборка приложения.**
  Run: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android`.
  Expected: 0 ошибок и 0 предупреждений.

- [ ] **Шаг 5: Коммит.**

```bash
FILES="tools/gen_sounds.py src/Chrono/Resources/Raw/bell.wav src/Chrono/Resources/Raw/chime.wav src/Chrono/Resources/Raw/digital.wav src/Chrono/Resources/Raw/rising.wav src/Chrono/Resources/Raw/soft.wav"
git add $FILES
git commit -m "Встроенные мелодии: генератор и пять WAV" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 4: Android — планирование, сигнал, автозапуск, «Надёжность»

**Модель:** haiku. **Изоляция:** git worktree от `feature/chrono-v1` после волны 2. **Skill:** `dotnet-maui:maui-app-lifecycle`.

**Файлы:**
- Create: `src/Chrono/Platforms/Android/{AlarmReceiver,AlarmScheduler,BootReceiver,SoundPlayer,AlarmRingService,ReliabilityChecks}.cs`.
- Не трогать: `MainActivity.cs` (задача 7).

**Interfaces:**
- Consumes: `Alarm`, `AlarmStore`, `AlarmService`, `IAlarmScheduler`, `ISoundPlayer`, `IReliabilityChecks` (задача 2); `AppResources`, `Resource.Drawable.ic_stat_alarm` (задача 1); `Chrono.MainActivity` (шаблон, namespace `Chrono`).
- Produces (namespace `Chrono.Platform`):
  - `AlarmReceiver.ExtraAlarmId = "alarm_id"` и `AlarmReceiver.CreatePendingIntent(Context, Guid)`;
  - классы `AlarmScheduler : IAlarmScheduler` (конструктор без параметров), `SoundPlayer : ISoundPlayer`, `ReliabilityChecks : IReliabilityChecks`, `AlarmRingService`, `BootReceiver`.

**Как выглядит молчаливый отказ на Android** (ревьюеру и исполнителю):
- сборка зелёная, но в итоговом манифесте нет `foregroundServiceType="systemExempted"` или действий BootReceiver;
- `StartForeground` не вызван при раннем выходе — процесс падает через ~5 с после срабатывания;
- вибрация идёт без атрибутов — в «Не беспокоить» её нет;
- `PendingIntent` различается только `requestCode` — `Cancel` снимает чужой будильник. Здесь будильники различаются Data-URI.

- [ ] **Шаг 1: Классы.**

Файл `src/Chrono/Platforms/Android/AlarmReceiver.cs`:

```csharp
using Android.App;
using Android.Content;

namespace Chrono.Platform;

/// <summary>Получает срабатывание AlarmManager и запускает foreground-сервис звонка.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AlarmReceiver : BroadcastReceiver
{
    public const string ExtraAlarmId = "alarm_id";

    /// <summary>
    /// PendingIntent срабатывания. Будильники различаются Data-URI (requestCode у всех 0),
    /// поэтому Cancel находит ровно тот же PendingIntent.
    /// </summary>
    public static PendingIntent CreatePendingIntent(Context context, Guid alarmId)
    {
        var intent = new Intent(context, typeof(AlarmReceiver))
            .SetData(global::Android.Net.Uri.Parse($"chrono://alarm/{alarmId}"))!
            .PutExtra(ExtraAlarmId, alarmId.ToString())!;
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        var service = new Intent(context!, typeof(AlarmRingService))
            .PutExtra(ExtraAlarmId, intent?.GetStringExtra(ExtraAlarmId));
        context!.StartForegroundService(service);
    }
}
```

Файл `src/Chrono/Platforms/Android/AlarmScheduler.cs`:

```csharp
using Android.App;
using Android.Content;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Планирование через AlarmManager.SetAlarmClock: точный, не ограничивается Doze.</summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var now = DateTime.Now;

        foreach (var alarm in alarms)
        {
            var operation = AlarmReceiver.CreatePendingIntent(context, alarm.Id);
            if (alarm.IsEnabled && alarm.At > now)
            {
                // Локальное «настенное» время → абсолютное по текущему поясу.
                var triggerAtMs = new DateTimeOffset(DateTime.SpecifyKind(alarm.At, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                var showIntent = PendingIntent.GetActivity(
                    context, 0, new Intent(context, typeof(MainActivity)), PendingIntentFlags.Immutable);
                manager.SetAlarmClock(new AlarmManager.AlarmClockInfo(triggerAtMs, showIntent), operation);
            }
            else
            {
                manager.Cancel(operation);
            }
        }
    }

    public void Cancel(Guid id)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        manager.Cancel(AlarmReceiver.CreatePendingIntent(context, id));
    }

    public void StopRinging(Guid id)
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(AlarmRingService)));

        // Экран сигнала больше не должен показываться поверх блокировки.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity && OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            activity.SetShowWhenLocked(false);
            activity.SetTurnScreenOn(false);
        }
    }
}
```

Файл `src/Chrono/Platforms/Android/BootReceiver.cs`:

```csharp
using Android.App;
using Android.Content;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Переустанавливает будильники после перезагрузки, обновления приложения и смены времени/пояса.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged, Intent.ActionMyPackageReplaced])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        // .NET кэширует TimeZoneInfo.Local: без сброса после смены пояса время пересчиталось бы по старому.
        TimeZoneInfo.ClearCachedData();
        var service = new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System);
        service.RescheduleAll();
    }
}
```

Файл `src/Chrono/Platforms/Android/SoundPlayer.cs`:

```csharp
using Android.Media;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Мелодия из assets ({id}.wav) через поток будильника; при ошибке — системный звук будильника.</summary>
public sealed class SoundPlayer : ISoundPlayer
{
    private MediaPlayer? player;

    public void Play(string soundId, bool loop)
    {
        this.Stop();

        var context = global::Android.App.Application.Context;
        var player = new MediaPlayer();
        player.SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Alarm)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!);
        try
        {
            using var fd = context.Assets!.OpenFd($"{soundId}.wav");
            player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
            player.Prepare();
        }
        catch (Exception)
        {
            // Мелодия недоступна — играем системный звук будильника, чтобы сигнал не пропал.
            player.Reset();
            player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Alarm)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!);
            player.SetDataSource(context, RingtoneManager.GetDefaultUri(RingtoneType.Alarm)!);
            player.Prepare();
        }

        player.Looping = loop;
        player.Start();
        this.player = player;
    }

    public void Stop()
    {
        if (this.player is null)
        {
            return;
        }

        this.player.Stop();
        this.player.Release();
        this.player = null;
    }
}
```

Файл `src/Chrono/Platforms/Android/AlarmRingService.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Foreground-сервис звонка: мелодия в цикле, вибрация, уведомление с full-screen intent.
/// Живёт только пока звенит сигнал, не дольше 10 минут.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSystemExempted)]
public sealed class AlarmRingService : Service
{
    public const string ActionStop = "chrono.action.STOP";
    public const string AlarmChannelId = "alarm";
    public const string MissedChannelId = "missed";
    public const int RingNotificationId = 1;
    public const int MissedNotificationId = 2;
    public static readonly TimeSpan RingTimeout = TimeSpan.FromMinutes(10);

    private readonly SoundPlayer soundPlayer = new();
    private Vibrator? vibrator;
    private PowerManager.WakeLock? wakeLock;
    private Handler? timeoutHandler;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            this.StopSelf();
            return StartCommandResult.NotSticky;
        }

        var notifications = (NotificationManager)this.GetSystemService(NotificationService)!;
        var alarmChannel = new NotificationChannel(AlarmChannelId, AppResources.ChannelAlarms, NotificationImportance.High);
        alarmChannel.SetSound(null, null);
        notifications.CreateNotificationChannel(alarmChannel);
        notifications.CreateNotificationChannel(
            new NotificationChannel(MissedChannelId, AppResources.ChannelMissed, NotificationImportance.Default));

        var alarmIdText = intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId);
        var alarm = Guid.TryParse(alarmIdText, out var alarmId)
            ? new AlarmStore(FileSystem.AppDataDirectory).Load().Alarms.FirstOrDefault(a => a.Id == alarmId)
            : null;
        var label = string.IsNullOrWhiteSpace(alarm?.Label) ? AppResources.DefaultLabel : alarm.Label;
        var timeText = (alarm?.At ?? DateTime.Now).ToString("HH:mm");

        var fullScreenIntent = PendingIntent.GetActivity(
            this,
            0,
            new Intent(this, typeof(MainActivity))
                .PutExtra(AlarmReceiver.ExtraAlarmId, alarmIdText)!
                .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var stopIntent = PendingIntent.GetService(
            this, 1, new Intent(this, typeof(AlarmRingService)).SetAction(ActionStop), PendingIntentFlags.Immutable);

        var notification = new Notification.Builder(this, AlarmChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
            .SetContentTitle(timeText)!
            .SetContentText(label)!
            .SetCategory(Notification.CategoryAlarm)!
            .SetOngoing(true)!
            .SetFullScreenIntent(fullScreenIntent, true)!
            .SetContentIntent(fullScreenIntent)!
            .AddAction(new Notification.Action.Builder(
                global::Android.Graphics.Drawables.Icon.CreateWithResource(this, Resource.Drawable.ic_stat_alarm),
                AppResources.Stop,
                stopIntent).Build())!
            .Build()!;

        // StartForeground обязан прозвучать в течение нескольких секунд после StartForegroundService —
        // вызываем его до любых ранних выходов, иначе система роняет процесс.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            this.StartForeground(RingNotificationId, notification, ForegroundService.TypeSystemExempted);
        }
        else
        {
            this.StartForeground(RingNotificationId, notification);
        }

        // Повторный старт (второй будильник в ту же минуту) заменяет текущий сигнал.
        this.soundPlayer.Stop();
        this.vibrator?.Cancel();
        this.timeoutHandler?.RemoveCallbacksAndMessages(null);

        if (alarm is null)
        {
            this.StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (this.wakeLock is null)
        {
            var power = (PowerManager)this.GetSystemService(PowerService)!;
            this.wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "chrono:ring")!;
            this.wakeLock.Acquire((long)(RingTimeout + TimeSpan.FromSeconds(10)).TotalMilliseconds);
        }

        if (alarm.SoundEnabled)
        {
            this.soundPlayer.Play(alarm.SoundId, loop: true);
        }

        if (alarm.VibrationEnabled)
        {
            this.vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? ((VibratorManager)this.GetSystemService(VibratorManagerService)!).DefaultVibrator
                : (Vibrator)this.GetSystemService(VibratorService)!;
            // Без атрибутов будильника система считает вибрацию UNKNOWN и может подавить её в «Не беспокоить».
            var pattern = VibrationEffect.CreateWaveform([0, 800, 600], 0)!;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                this.vibrator.Vibrate(pattern, VibrationAttributes.CreateForUsage((int)VibrationAttributesUsageType.Alarm));
            }
            else
            {
                this.vibrator.Vibrate(pattern, new AudioAttributes.Builder().SetUsage(AudioUsageKind.Alarm)!.Build()!);
            }
        }

        this.timeoutHandler = new Handler(Looper.MainLooper!);
        this.timeoutHandler.PostDelayed(
            () =>
            {
                var missed = new Notification.Builder(this, MissedChannelId)
                    .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
                    .SetContentTitle(string.Format(AppResources.MissedAlarm, timeText))!
                    .SetContentText(label)!
                    .SetContentIntent(PendingIntent.GetActivity(
                        this, 2, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable))!
                    .SetAutoCancel(true)!
                    .Build()!;
                notifications.Notify(MissedNotificationId, missed);
                this.StopSelf();
            },
            (long)RingTimeout.TotalMilliseconds);

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        this.soundPlayer.Stop();
        this.vibrator?.Cancel();
        this.timeoutHandler?.RemoveCallbacksAndMessages(null);
        if (this.wakeLock?.IsHeld == true)
        {
            this.wakeLock.Release();
        }

        this.StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}
```

Файл `src/Chrono/Platforms/Android/ReliabilityChecks.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Chrono.Services;

namespace Chrono.Platform;

public sealed class ReliabilityChecks : IReliabilityChecks
{
    public Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync()
    {
        var context = global::Android.App.Application.Context;
        var notifications = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var power = (PowerManager)context.GetSystemService(Context.PowerService)!;

        IReadOnlyList<ReliabilityStatus> result =
        [
            new(ReliabilityItem.Notifications, notifications.AreNotificationsEnabled()),
            new(ReliabilityItem.ExactAlarms, !OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms()),
            new(ReliabilityItem.FullScreen, !OperatingSystem.IsAndroidVersionAtLeast(34) || notifications.CanUseFullScreenIntent()),
            new(ReliabilityItem.Battery, power.IsIgnoringBatteryOptimizations(context.PackageName)),
        ];
        return Task.FromResult(result);
    }

    public async Task RequestPermissionsAsync()
    {
        await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    public void Fix(ReliabilityItem item)
    {
        var context = global::Android.App.Application.Context;
        var packageUri = global::Android.Net.Uri.Parse($"package:{context.PackageName}");
        var intent = item switch
        {
            ReliabilityItem.Notifications => new Intent(Settings.ActionAppNotificationSettings)
                .PutExtra(Settings.ExtraAppPackage, context.PackageName)!,
            ReliabilityItem.ExactAlarms when OperatingSystem.IsAndroidVersionAtLeast(31) =>
                new Intent(Settings.ActionRequestScheduleExactAlarm, packageUri),
            ReliabilityItem.FullScreen when OperatingSystem.IsAndroidVersionAtLeast(34) =>
                new Intent(Settings.ActionManageAppUseFullScreenIntent, packageUri),
            ReliabilityItem.Battery => new Intent(Settings.ActionIgnoreBatteryOptimizationSettings),
            _ => new Intent(Settings.ActionApplicationDetailsSettings, packageUri),
        };
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
}
```


- [ ] **Шаг 2: Сборка.**
  Run: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` и `-f net11.0-ios`. iOS не должна сломаться: файлы `Platforms/Android` в неё не входят.
  Expected: 0 ошибок и 0 предупреждений на обеих.

- [ ] **Шаг 3: Проверка формы — итоговый манифест.**
  Run:

```bash
M=$(ls -t "$TEMP"/chrono-build/*/Chrono/obj/Debug/net11.0-android/android/AndroidManifest.xml | head -1); echo "$M"
grep -E "receiver|service|action android:name" "$M" | grep -iE "Alarm|Boot|TIME|BOOT|PACKAGE"
```

  Expected: `AlarmReceiver` с `exported="false"`; `AlarmRingService` с `foregroundServiceType="systemExempted"`; `BootReceiver` с `exported="true"` и четырьмя действиями `BOOT_COMPLETED`, `TIME_SET`, `TIMEZONE_CHANGED`, `MY_PACKAGE_REPLACED`.
  Команда берёт самый свежий манифест: в `%TEMP%\chrono-build` лежит по каталогу-хешу на каждый worktree.
  Проверка смысла (срабатывание при убитом процессе) — задача 8. Код из этого шага прошёл её в спайке.

- [ ] **Шаг 4: Коммит.**

```bash
FILES="src/Chrono/Platforms/Android/AlarmReceiver.cs src/Chrono/Platforms/Android/AlarmScheduler.cs src/Chrono/Platforms/Android/BootReceiver.cs src/Chrono/Platforms/Android/SoundPlayer.cs src/Chrono/Platforms/Android/AlarmRingService.cs src/Chrono/Platforms/Android/ReliabilityChecks.cs"
git add $FILES
git commit -m "Android: SetAlarmClock, сервис сигнала, автозапуск, проверки надёжности" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 5: iOS — цепочка уведомлений, звук, «Надёжность»

**Модель:** haiku. **Изоляция:** git worktree от `feature/chrono-v1` после волны 2.

**Файлы:**
- Create: `src/Chrono/Platforms/iOS/{AlarmScheduler,SoundPlayer,ReliabilityChecks}.cs`.
- Не трогать: `NotificationDelegate.cs` (задача 7).

**Interfaces:**
- Consumes: `IosNotificationPlan`, `IAlarmScheduler`, `ISoundPlayer`, `IReliabilityChecks` (задача 2); `AppResources` (задача 1).
- Produces (namespace `Chrono.Platform`):
  - `AlarmScheduler(ISoundPlayer)`, `AlarmScheduler.AlarmIdKey = "alarm_id"`;
  - `SoundPlayer : ISoundPlayer`, `ReliabilityChecks : IReliabilityChecks`.

- [ ] **Шаг 1: Классы.**

Файл `src/Chrono/Platforms/iOS/AlarmScheduler.cs`:

```csharp
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>Планирование цепочкой time-sensitive уведомлений (IosNotificationPlan).</summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    public const string AlarmIdKey = "alarm_id";

    private readonly ISoundPlayer soundPlayer;

    public AlarmScheduler(ISoundPlayer soundPlayer)
    {
        this.soundPlayer = soundPlayer;
    }

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        var center = UNUserNotificationCenter.Current;
        center.RemoveAllPendingNotificationRequests();

        foreach (var planned in IosNotificationPlan.Build(alarms, DateTime.Now))
        {
            var content = new UNMutableNotificationContent
            {
                Title = string.IsNullOrWhiteSpace(planned.Alarm.Label) ? AppResources.DefaultLabel : planned.Alarm.Label,
                Body = planned.Alarm.At.ToString("HH:mm"),
                // TimeSensitive2, а не TimeSensitive: устаревший член имеет значение 3 = Critical на стороне iOS.
                InterruptionLevel = UNNotificationInterruptionLevel.TimeSensitive2,
                UserInfo = NSDictionary.FromObjectAndKey(new NSString(planned.Alarm.Id.ToString()), new NSString(AlarmIdKey)),
            };
            if (planned.Alarm.SoundEnabled)
            {
                content.Sound = UNNotificationSound.GetSound($"{planned.Alarm.SoundId}.wav");
            }

            var fireAt = planned.FireAt;
            var components = new NSDateComponents
            {
                Year = fireAt.Year,
                Month = fireAt.Month,
                Day = fireAt.Day,
                Hour = fireAt.Hour,
                Minute = fireAt.Minute,
                Second = fireAt.Second,
            };
            var request = UNNotificationRequest.FromIdentifier(
                planned.Id, content, UNCalendarNotificationTrigger.CreateTrigger(components, false));
            center.AddNotificationRequest(request, error =>
            {
                if (error is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"Chrono: уведомление {planned.Id} не запланировано: {error}");
                }
            });
        }
    }

    public void Cancel(Guid id)
    {
        var ids = IosNotificationPlan.ChainIds(id).ToArray();
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(ids);
    }

    public void StopRinging(Guid id)
    {
        this.soundPlayer.Stop();
        var ids = IosNotificationPlan.ChainIds(id).ToArray();
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(ids);
    }
}
```

Файл `src/Chrono/Platforms/iOS/SoundPlayer.cs`:

```csharp
using AVFoundation;
using Chrono.Services;
using Foundation;

namespace Chrono.Platform;

/// <summary>Мелодия из бандла ({id}.wav) через AVAudioPlayer; категория Playback — звучит и в беззвучном режиме.</summary>
public sealed class SoundPlayer : ISoundPlayer
{
    private AVAudioPlayer? player;

    public void Play(string soundId, bool loop)
    {
        this.Stop();

        var url = NSBundle.MainBundle.GetUrlForResource(soundId, "wav");
        if (url is null)
        {
            return;
        }

        AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Playback);
        AVAudioSession.SharedInstance().SetActive(true);
        this.player = AVAudioPlayer.FromUrl(url, out _);
        if (this.player is null)
        {
            return;
        }

        this.player.NumberOfLoops = loop ? -1 : 0;
        this.player.Play();
    }

    public void Stop()
    {
        this.player?.Stop();
        this.player?.Dispose();
        this.player = null;
    }
}
```

Файл `src/Chrono/Platforms/iOS/ReliabilityChecks.cs`:

```csharp
using Chrono.Services;
using UserNotifications;

namespace Chrono.Platform;

public sealed class ReliabilityChecks : IReliabilityChecks
{
    public async Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync()
    {
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        return
        [
            new(ReliabilityItem.Notifications, settings.AuthorizationStatus == UNAuthorizationStatus.Authorized),
            new(ReliabilityItem.TimeSensitive, settings.TimeSensitiveSetting == UNNotificationSetting.Enabled),
        ];
    }

    public async Task RequestPermissionsAsync()
    {
        await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
    }

    public void Fix(ReliabilityItem item)
    {
        AppInfo.Current.ShowSettingsUI();
    }
}
```


- [ ] **Шаг 2: Сборка.**
  Run: `dotnet build src/Chrono/Chrono.csproj -f net11.0-ios` и `-f net11.0-android`.
  Expected: 0 ошибок и 0 предупреждений.

- [ ] **Шаг 3: Защита от TimeSensitive (Critical).**
  Run: `grep -n "InterruptionLevel" src/Chrono/Platforms/iOS/AlarmScheduler.cs`.
  Expected: ровно одна строка с `UNNotificationInterruptionLevel.TimeSensitive2`. Строки с `.TimeSensitive,` или `.TimeSensitive;` (без 2) быть не должно.
  Чтобы убедиться, что iOS действительно компилируется: временно замените `TimeSensitive2` на `NoSuchLevel`, соберите `-f net11.0-ios` и получите `CS0117`. Затем верните файл и пересоберите. `git diff` по файлу должен быть пустым.

- [ ] **Шаг 4: Коммит.**

```bash
FILES="src/Chrono/Platforms/iOS/AlarmScheduler.cs src/Chrono/Platforms/iOS/SoundPlayer.cs src/Chrono/Platforms/iOS/ReliabilityChecks.cs"
git add $FILES
git commit -m "iOS: цепочка time-sensitive уведомлений, звук, проверки надёжности" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 6: ViewModels и страницы

**Модель:** haiku. **Изоляция:** git worktree от `feature/chrono-v1` после волны 2. **Skills:** `dotnet-maui:maui-data-binding`, `dotnet-maui:maui-collectionview`, `dotnet-maui:maui-shell-navigation`, `dotnet-maui:maui-safe-area`.

**Файлы:**
- Create:
  - `src/Chrono/Services/RingLauncher.cs`;
  - `src/Chrono/ViewModels/{AlarmListViewModel,AlarmEditViewModel,RingViewModel,ReliabilityViewModel}.cs`;
  - `src/Chrono/Views/SplashPage.cs`, `src/Chrono/Views/{AlarmListPage,AlarmEditPage,RingPage,ReliabilityPage}.xaml(.cs)`.

**Interfaces:**
- Consumes: `AlarmService`, `ISoundPlayer`, `IAlarmScheduler`, `IReliabilityChecks`, `Sounds`, `Alarm` (задача 2); `AppResources`, стили и цвета, `bell.png` (задача 1).
- Produces:
  - `RingLauncher.PendingId`, `RingLauncher.Request(Guid)`, `RingLauncher.ShowPendingAsync()`;
  - маршруты, которые ожидают страницы: `edit` (`?id=`, `&expired=true`), `ring` (`?id=`), `reliability`;
  - страницы с конструкторами из DI: `AlarmListPage(AlarmListViewModel)`, `AlarmEditPage(AlarmEditViewModel)`, `RingPage(RingViewModel)`, `ReliabilityPage(ReliabilityViewModel)`, `SplashPage(IServiceProvider)`. `SplashPage` берёт `AppShell` из DI.

- [ ] **Шаг 1: Запуск сигнала и ViewModels.**

Файл `src/Chrono/Services/RingLauncher.cs`:

```csharp
using Chrono.ViewModels;
using Chrono.Views;

namespace Chrono.Services;

/// <summary>
/// Открывает RingPage по запросу платформы (full-screen intent на Android, уведомление на iOS).
/// Если Shell ещё не создан (холодный старт, идёт SplashPage), id запоминается и показывается
/// из AlarmListPage.OnAppearing через ShowPendingAsync.
/// </summary>
public static class RingLauncher
{
    public static Guid? PendingId { get; private set; }

    public static void Request(Guid alarmId)
    {
        PendingId = alarmId;
        MainThread.BeginInvokeOnMainThread(async () => await ShowPendingAsync());
    }

    public static async Task ShowPendingAsync()
    {
        if (PendingId is not Guid alarmId || Shell.Current is null)
        {
            return;
        }

        PendingId = null;

        // Сигнал уже на экране — новый будильник заменяет текущий, а не открывает второй экран.
        if (Shell.Current.CurrentPage is RingPage { BindingContext: RingViewModel ring })
        {
            ring.Load(alarmId);
            return;
        }

        await Shell.Current.GoToAsync($"ring?id={alarmId}");
    }
}
```

Файл `src/Chrono/ViewModels/AlarmListViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Строка списка: готовый к показу текст и переключатель.</summary>
public sealed partial class AlarmItem : ObservableObject
{
    private readonly AlarmListViewModel owner;

    public AlarmItem(Alarm alarm, AlarmListViewModel owner)
    {
        this.owner = owner;
        this.Alarm = alarm;
        this.TimeText = alarm.At.ToString("HH:mm");
        var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
        this.Details = $"{alarm.At.ToString("ddd, d MMM", CultureInfo.CurrentUICulture)} · {label}";
        this.isEnabled = alarm.IsEnabled;
    }

    public Alarm Alarm { get; }

    public string TimeText { get; }

    public string Details { get; }

    // Поле, а не partial-свойство: начальное значение задаётся без вызова OnIsEnabledChanged.
    [ObservableProperty]
    private bool isEnabled;

    partial void OnIsEnabledChanged(bool value) => this.owner.OnItemToggled(this, value);
}

public sealed partial class AlarmListViewModel : ObservableObject
{
    private const string FirstRunKey = "first_run_done";

    private readonly AlarmService alarmService;
    private readonly IReliabilityChecks reliability;

    public AlarmListViewModel(AlarmService alarmService, IReliabilityChecks reliability)
    {
        this.alarmService = alarmService;
        this.reliability = reliability;
    }

    public ObservableCollection<AlarmItem> Alarms { get; } = [];

    [ObservableProperty]
    public partial string NextAlarmText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsWarningVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Вызывается из AlarmListPage.OnAppearing.</summary>
    public async Task AppearingAsync()
    {
        await RingLauncher.ShowPendingAsync();

        if (!Preferences.Default.Get(FirstRunKey, false))
        {
            Preferences.Default.Set(FirstRunKey, true);
            await this.reliability.RequestPermissionsAsync();
            await Shell.Current.GoToAsync("reliability");
            return;
        }

        this.Load();

        if (this.alarmService.RecoveredFromCorruptFile)
        {
            await Shell.Current.DisplayAlertAsync(AppResources.AppName, AppResources.CorruptFile, AppResources.Ok);
        }

        var checks = await this.reliability.GetStatusAsync();
        this.IsWarningVisible = this.alarmService.SchedulingError is not null || checks.Any(c => !c.Ok);
    }

    public void Load()
    {
        var alarms = this.alarmService.GetAll();
        this.Alarms.Clear();
        foreach (var alarm in alarms)
        {
            this.Alarms.Add(new AlarmItem(alarm, this));
        }

        this.IsEmpty = this.Alarms.Count == 0;

        var next = alarms.Where(a => a.IsEnabled).Select(a => (DateTime?)a.At).FirstOrDefault();
        if (next is null)
        {
            this.NextAlarmText = AppResources.NoActiveAlarms;
            return;
        }

        var left = next.Value - DateTime.Now;
        this.NextAlarmText = left.TotalHours >= 1
            ? string.Format(AppResources.NextInHours, (int)left.TotalHours, left.Minutes)
            : string.Format(AppResources.NextInMinutes, Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
    }

    public void OnItemToggled(AlarmItem item, bool enabled)
    {
        // Переключатель ещё в процессе анимации — перестраиваем список после неё.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!this.alarmService.Toggle(item.Alarm.Id, enabled))
            {
                // Время прошло: включать нечего — предлагаем выбрать новую дату.
                await Shell.Current.GoToAsync($"edit?id={item.Alarm.Id}&expired=true");
                return;
            }

            this.Load();
        });
    }

    [RelayCommand]
    private Task AddAsync() => Shell.Current.GoToAsync("edit");

    [RelayCommand]
    private Task EditAsync(AlarmItem item) => Shell.Current.GoToAsync($"edit?id={item.Alarm.Id}");

    [RelayCommand]
    private void Delete(AlarmItem item)
    {
        this.alarmService.Delete(item.Alarm.Id);
        this.Load();
    }

    [RelayCommand]
    private Task OpenReliabilityAsync() => Shell.Current.GoToAsync("reliability");
}
```

Файл `src/Chrono/ViewModels/AlarmEditViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class SoundOption : ObservableObject
{
    public SoundOption(string id, string name)
    {
        this.Id = id;
        this.Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Создание и редактирование. Сохранение всегда включает будильник (как в системных «Часах»)
/// и требует время в будущем.
/// </summary>
public sealed partial class AlarmEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly ISoundPlayer soundPlayer;
    private Alarm? existing;

    public AlarmEditViewModel(AlarmService alarmService, ISoundPlayer soundPlayer)
    {
        this.alarmService = alarmService;
        this.soundPlayer = soundPlayer;
        this.SoundOptions = new(Sounds.All.Select(id =>
            new SoundOption(id, AppResources.ResourceManager.GetString($"Sound_{id}", AppResources.Culture) ?? id)));

        var start = DateTime.Now.AddHours(1);
        this.Date = start.Date;
        this.Time = new TimeSpan(start.Hour, start.Minute, 0);
        // Инициализатор свойства не вызывает OnSoundIdChanged — отмечаем мелодию по умолчанию явно.
        this.OnSoundIdChanged(this.SoundId);
    }

    public ObservableCollection<SoundOption> SoundOptions { get; }

    public DateTime MinimumDate => DateTime.Today;

    [ObservableProperty]
    public partial string Title { get; set; } = AppResources.NewAlarm;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial TimeSpan? Time { get; set; }

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string SoundId { get; set; } = Sounds.Default;

    [ObservableProperty]
    public partial bool VibrationEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorText { get; set; } = "";

    public bool HasError => !string.IsNullOrEmpty(this.ErrorText);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var value) || !Guid.TryParse(value?.ToString(), out var id))
        {
            return;
        }

        this.existing = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
        if (this.existing is null)
        {
            return;
        }

        this.Title = AppResources.EditAlarm;
        this.IsExisting = true;
        this.Date = this.existing.At.Date;
        this.Time = this.existing.At.TimeOfDay;
        this.Label = this.existing.Label;
        this.SoundEnabled = this.existing.SoundEnabled;
        this.VibrationEnabled = this.existing.VibrationEnabled;
        this.SoundId = this.existing.SoundId;

        if (query.ContainsKey("expired"))
        {
            this.ErrorText = AppResources.PickNewDate;
        }
    }

    [RelayCommand]
    private void SelectSound(SoundOption option)
    {
        this.SoundId = option.Id;
        this.soundPlayer.Play(option.Id, loop: false);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var at = (this.Date ?? DateTime.Today).Date + (this.Time ?? TimeSpan.Zero);
        var alarm = (this.existing ?? new Alarm()) with
        {
            At = at,
            Label = this.Label.Trim(),
            SoundEnabled = this.SoundEnabled,
            SoundId = this.SoundId,
            VibrationEnabled = this.VibrationEnabled,
            IsEnabled = true,
        };

        if (!this.alarmService.Save(alarm))
        {
            this.ErrorText = AppResources.TimeInPast;
            return;
        }

        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (this.existing is not null)
        {
            this.alarmService.Delete(this.existing.Id);
        }

        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    public void StopPreview() => this.soundPlayer.Stop();

    partial void OnDateChanged(DateTime? value) => this.ErrorText = "";

    partial void OnTimeChanged(TimeSpan? value) => this.ErrorText = "";

    partial void OnSoundIdChanged(string value)
    {
        foreach (var option in this.SoundOptions)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
```

Файл `src/Chrono/ViewModels/RingViewModel.cs`:

```csharp
using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class RingViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly IAlarmScheduler scheduler;
    private Guid alarmId;

    public RingViewModel(AlarmService alarmService, IAlarmScheduler scheduler)
    {
        this.alarmService = alarmService;
        this.scheduler = scheduler;
    }

    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial string DateText { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            this.Load(id);
        }
    }

    public void Load(Guid id)
    {
        this.alarmId = id;
        var alarm = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
        var at = alarm?.At ?? DateTime.Now;
        this.TimeText = at.ToString("HH:mm");
        this.DateText = at.ToString("dddd, d MMMM", CultureInfo.CurrentUICulture);
        this.Label = string.IsNullOrWhiteSpace(alarm?.Label) ? AppResources.DefaultLabel : alarm.Label;
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        this.scheduler.StopRinging(this.alarmId);
        await Shell.Current.GoToAsync("..");
    }
}
```

Файл `src/Chrono/ViewModels/ReliabilityViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed record ReliabilityRow(ReliabilityItem Item, string Title, bool Ok)
{
    public string Mark => this.Ok ? "✅" : "⚠️";

    public bool NeedsFix => !this.Ok;
}

public sealed partial class ReliabilityViewModel : ObservableObject
{
    private readonly IReliabilityChecks reliability;

    public ReliabilityViewModel(IReliabilityChecks reliability)
    {
        this.reliability = reliability;
    }

    public ObservableCollection<ReliabilityRow> Rows { get; } = [];

    public bool IsAndroid => DeviceInfo.Current.Platform == DevicePlatform.Android;

    public bool IsIos => DeviceInfo.Current.Platform == DevicePlatform.iOS;

    public async Task RefreshAsync()
    {
        var statuses = await this.reliability.GetStatusAsync();
        this.Rows.Clear();
        foreach (var status in statuses)
        {
            var title = AppResources.ResourceManager.GetString($"Rel_{status.Item}", AppResources.Culture) ?? status.Item.ToString();
            this.Rows.Add(new ReliabilityRow(status.Item, title, status.Ok));
        }
    }

    [RelayCommand]
    private void Fix(ReliabilityRow row) => this.reliability.Fix(row.Item);

    [RelayCommand]
    private Task CloseAsync() => Shell.Current.GoToAsync("..");
}
```


- [ ] **Шаг 2: Страницы.**

Файл `src/Chrono/Views/SplashPage.cs`:

```csharp
using Microsoft.Maui.Controls.Shapes;

namespace Chrono.Views;

/// <summary>
/// Вторая ступень splash: тот же фон и колокол, что у нативного MauiSplashScreen, «оживают» ≈1,2 с,
/// затем окно переключается на AppShell.
/// </summary>
public sealed class SplashPage : ContentPage
{
    private readonly IServiceProvider services;
    private readonly Image bell;
    private readonly Ellipse[] waves;
    private readonly Label title;

    public SplashPage(IServiceProvider services)
    {
        this.services = services;
        // Сплошной цвет как у нативного splash — переход без вспышки.
        this.Background = new SolidColorBrush(Color.FromArgb("#0A0B1A"));

        this.bell = new Image { Source = "bell.png", WidthRequest = 128, HeightRequest = 146, AnchorY = 0.08 };
        this.waves = Enumerable.Range(0, 3).Select(_ => new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromArgb("#FFB35C")),
            StrokeThickness = 2,
            WidthRequest = 220,
            HeightRequest = 220,
            Opacity = 0,
        }).ToArray();
        this.title = new Label
        {
            Text = "Chrono",
            FontFamily = "ManropeSemiBold",
            FontSize = 30,
            TextColor = Color.FromArgb("#E9EBFF"),
            HorizontalOptions = LayoutOptions.Center,
            Opacity = 0,
            TranslationY = 140,
        };

        var center = new Grid { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        foreach (var wave in this.waves)
        {
            center.Add(wave);
        }

        center.Add(this.bell);
        center.Add(this.title);
        this.Content = center;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var swing = async () =>
        {
            await this.bell.RotateToAsync(-16, 150, Easing.SinOut);
            await this.bell.RotateToAsync(14, 260, Easing.SinInOut);
            await this.bell.RotateToAsync(-8, 220, Easing.SinInOut);
            await this.bell.RotateToAsync(0, 180, Easing.SinIn);
        };
        var ripple = async () =>
        {
            for (var i = 0; i < this.waves.Length; i++)
            {
                var wave = this.waves[i];
                wave.Scale = 0.5;
                wave.Opacity = 0.7;
                _ = wave.ScaleToAsync(1.3, 900, Easing.CubicOut);
                _ = wave.FadeToAsync(0, 900, Easing.CubicIn);
                await Task.Delay(180);
            }
        };

        await Task.WhenAll(swing(), ripple(), this.title.FadeToAsync(1, 700, Easing.CubicIn));
        await Task.Delay(250);

        this.Window!.Page = this.services.GetRequiredService<AppShell>();
    }
}
```

Файл `src/Chrono/Views/AlarmListPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:vm="clr-namespace:Chrono.ViewModels"
             xmlns:res="clr-namespace:Chrono.Resources.Strings"
             x:Class="Chrono.Views.AlarmListPage"
             x:DataType="vm:AlarmListViewModel">

    <Grid RowDefinitions="Auto,Auto,Auto,*" Padding="20,12,20,0" SafeAreaEdges="Container">

        <!-- Шапка: название и шестерёнка «Надёжность». -->
        <Grid ColumnDefinitions="*,Auto">
            <Label Text="{x:Static res:AppResources.AppName}" FontFamily="ManropeSemiBold" FontSize="28" />
            <Button Grid.Column="1" Text="⚙" FontSize="22" BackgroundColor="Transparent" TextColor="{StaticResource TextMuted}"
                    Padding="8,0" Command="{Binding OpenReliabilityCommand}"
                    SemanticProperties.Description="{x:Static res:AppResources.ReliabilityTitle}" />
        </Grid>

        <Label Grid.Row="1" Text="{Binding NextAlarmText}" TextColor="{StaticResource Accent}" FontSize="13" Margin="0,2,0,12" />

        <Border Grid.Row="2" IsVisible="{Binding IsWarningVisible}" Background="#33FF8F7A" Stroke="{StaticResource Danger}"
                StrokeShape="RoundRectangle 14" Padding="14,10" Margin="0,0,0,12">
            <Border.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding OpenReliabilityCommand}" />
            </Border.GestureRecognizers>
            <Label Text="{x:Static res:AppResources.WarningBanner}" TextColor="{StaticResource Danger}" FontFamily="ManropeSemiBold" />
        </Border>

        <CollectionView Grid.Row="3" ItemsSource="{Binding Alarms}" SelectionMode="None">
            <CollectionView.EmptyView>
                <Label Text="{x:Static res:AppResources.EmptyList}" Style="{StaticResource Muted}" FontSize="14"
                       HorizontalTextAlignment="Center" Margin="24,48" />
            </CollectionView.EmptyView>
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="vm:AlarmItem">
                    <SwipeView Margin="0,0,0,10">
                        <SwipeView.RightItems>
                            <SwipeItems Mode="Execute">
                                <SwipeItem Text="{x:Static res:AppResources.Delete}" BackgroundColor="{StaticResource Danger}"
                                           Command="{Binding DeleteCommand, Source={RelativeSource AncestorType={x:Type vm:AlarmListViewModel}}, x:DataType=vm:AlarmListViewModel}"
                                           CommandParameter="{Binding .}" />
                            </SwipeItems>
                        </SwipeView.RightItems>
                        <Border Style="{StaticResource Card}">
                            <Border.Triggers>
                                <DataTrigger TargetType="Border" Binding="{Binding IsEnabled}" Value="False">
                                    <Setter Property="Opacity" Value="0.45" />
                                </DataTrigger>
                            </Border.Triggers>
                            <Border.GestureRecognizers>
                                <TapGestureRecognizer
                                    Command="{Binding EditCommand, Source={RelativeSource AncestorType={x:Type vm:AlarmListViewModel}}, x:DataType=vm:AlarmListViewModel}"
                                    CommandParameter="{Binding .}" />
                            </Border.GestureRecognizers>
                            <Grid ColumnDefinitions="*,Auto">
                                <VerticalStackLayout>
                                    <Label Text="{Binding TimeText}" FontFamily="ManropeLight" FontSize="38" />
                                    <Label Text="{Binding Details}" Style="{StaticResource Muted}" />
                                </VerticalStackLayout>
                                <Switch Grid.Column="1" IsToggled="{Binding IsEnabled}" VerticalOptions="Center" />
                            </Grid>
                        </Border>
                    </SwipeView>
                </DataTemplate>
            </CollectionView.ItemTemplate>
            <CollectionView.Footer>
                <BoxView HeightRequest="96" Color="Transparent" />
            </CollectionView.Footer>
        </CollectionView>

        <!-- Плавающая кнопка «+». -->
        <Button Grid.Row="3" Text="+" FontSize="30" FontFamily="ManropeLight" Padding="0"
                WidthRequest="60" HeightRequest="60" CornerRadius="20"
                Background="{StaticResource AccentBrush}" TextColor="{StaticResource OnAccent}"
                HorizontalOptions="End" VerticalOptions="End" Margin="0,0,4,24"
                Command="{Binding AddCommand}" SemanticProperties.Description="{x:Static res:AppResources.NewAlarm}" />
    </Grid>
</ContentPage>
```

Файл `src/Chrono/Views/AlarmListPage.xaml.cs`:

```csharp
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmListPage : ContentPage
{
    private readonly AlarmListViewModel viewModel;

    public AlarmListPage(AlarmListViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await this.viewModel.AppearingAsync();
    }
}
```

Файл `src/Chrono/Views/AlarmEditPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:vm="clr-namespace:Chrono.ViewModels"
             xmlns:res="clr-namespace:Chrono.Resources.Strings"
             x:Class="Chrono.Views.AlarmEditPage"
             x:DataType="vm:AlarmEditViewModel">

    <!-- Снизу All: кнопки поднимаются над клавиатурой (нужен WindowSoftInputModeAdjust=Resize в App.xaml). -->
    <Grid RowDefinitions="*,Auto" Padding="20,12,20,16" SafeAreaEdges="Container, Container, Container, All">

        <ScrollView VerticalScrollBarVisibility="Never">
            <VerticalStackLayout>
                <Label Text="{Binding Title}" FontFamily="ManropeSemiBold" FontSize="20" Margin="0,0,0,12" />

                <!-- Крупные время и дата: нажатие открывает нативные пикеры. -->
                <Border Style="{StaticResource Card}" Padding="16,14">
                    <VerticalStackLayout HorizontalOptions="Center">
                        <TimePicker Time="{Binding Time}" Format="HH:mm" FontSize="56" HorizontalOptions="Center" />
                        <DatePicker Date="{Binding Date}" MinimumDate="{Binding MinimumDate}" Format="dddd, d MMMM"
                                    FontSize="14" HorizontalOptions="Center" />
                        <Label Text="{x:Static res:AppResources.TapToChange}" Style="{StaticResource Muted}" FontSize="11"
                               HorizontalOptions="Center" />
                    </VerticalStackLayout>
                </Border>
                <Label Text="{Binding ErrorText}" TextColor="{StaticResource Danger}" Margin="4,6,0,0"
                       IsVisible="{Binding HasError}" />

                <Label Text="{x:Static res:AppResources.LabelHeader}" Style="{StaticResource SectionHeader}" />
                <Border Style="{StaticResource Card}" Padding="12,2" Stroke="{StaticResource Accent}">
                    <Entry Text="{Binding Label}" Placeholder="{x:Static res:AppResources.LabelPlaceholder}" MaxLength="60"
                           ReturnType="Done" />
                </Border>

                <Label Text="{x:Static res:AppResources.SignalHeader}" Style="{StaticResource SectionHeader}" />
                <Border Style="{StaticResource Card}" Margin="0,0,0,8">
                    <VerticalStackLayout Spacing="10">
                        <Grid ColumnDefinitions="*,Auto">
                            <Label Text="{x:Static res:AppResources.Sound}" VerticalOptions="Center" />
                            <Switch Grid.Column="1" IsToggled="{Binding SoundEnabled}" />
                        </Grid>
                        <!-- Мелодии: нажатие выбирает и проигрывает один раз. -->
                        <ScrollView Orientation="Horizontal" HorizontalScrollBarVisibility="Never" IsVisible="{Binding SoundEnabled}">
                            <HorizontalStackLayout Spacing="8" BindableLayout.ItemsSource="{Binding SoundOptions}">
                                <BindableLayout.ItemTemplate>
                                    <DataTemplate x:DataType="vm:SoundOption">
                                        <Border StrokeShape="RoundRectangle 14" Padding="14,8" Stroke="{StaticResource SurfaceBorder}"
                                                Background="{StaticResource Surface}">
                                            <Border.Triggers>
                                                <DataTrigger TargetType="Border" Binding="{Binding IsSelected}" Value="True">
                                                    <Setter Property="Background" Value="{StaticResource AccentBrush}" />
                                                </DataTrigger>
                                            </Border.Triggers>
                                            <Border.GestureRecognizers>
                                                <TapGestureRecognizer
                                                    Command="{Binding SelectSoundCommand, Source={RelativeSource AncestorType={x:Type vm:AlarmEditViewModel}}, x:DataType=vm:AlarmEditViewModel}"
                                                    CommandParameter="{Binding .}" />
                                            </Border.GestureRecognizers>
                                            <Label Text="{Binding Name}" FontSize="13">
                                                <Label.Triggers>
                                                    <DataTrigger TargetType="Label" Binding="{Binding IsSelected}" Value="True">
                                                        <Setter Property="TextColor" Value="{StaticResource OnAccent}" />
                                                    </DataTrigger>
                                                </Label.Triggers>
                                            </Label>
                                        </Border>
                                    </DataTemplate>
                                </BindableLayout.ItemTemplate>
                            </HorizontalStackLayout>
                        </ScrollView>
                    </VerticalStackLayout>
                </Border>
                <Border Style="{StaticResource Card}">
                    <Grid ColumnDefinitions="*,Auto">
                        <Label Text="{x:Static res:AppResources.Vibration}" VerticalOptions="Center" />
                        <Switch Grid.Column="1" IsToggled="{Binding VibrationEnabled}" />
                    </Grid>
                </Border>
            </VerticalStackLayout>
        </ScrollView>

        <!-- Действия: «Удалить» над кнопками; «Отмена» и «Сохранить» прижаты вправо, «Сохранить» — крайняя справа. -->
        <VerticalStackLayout Grid.Row="1" Spacing="4" Margin="0,8,0,0">
            <Button Text="{x:Static res:AppResources.DeleteAlarm}" Style="{StaticResource DangerLink}"
                    IsVisible="{Binding IsExisting}" Command="{Binding DeleteCommand}" HorizontalOptions="Center" />
            <HorizontalStackLayout HorizontalOptions="End" Spacing="10">
                <Button Text="{x:Static res:AppResources.Cancel}" Style="{StaticResource SecondaryButton}" Command="{Binding CancelCommand}" />
                <Button Text="{x:Static res:AppResources.Save}" Style="{StaticResource PrimaryButton}" Command="{Binding SaveCommand}" />
            </HorizontalStackLayout>
        </VerticalStackLayout>
    </Grid>
</ContentPage>
```

Файл `src/Chrono/Views/AlarmEditPage.xaml.cs`:

```csharp
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmEditPage : ContentPage
{
    private readonly AlarmEditViewModel viewModel;

    public AlarmEditPage(AlarmEditViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        // Прослушивание мелодии не должно продолжаться после ухода со страницы (в т.ч. системной кнопкой «Назад»).
        this.viewModel.StopPreview();
        base.OnDisappearing();
    }
}
```

Файл `src/Chrono/Views/RingPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:vm="clr-namespace:Chrono.ViewModels"
             xmlns:res="clr-namespace:Chrono.Resources.Strings"
             x:Class="Chrono.Views.RingPage"
             x:DataType="vm:RingViewModel">

    <Grid RowDefinitions="*,Auto,Auto,Auto,Auto,*,Auto" Padding="24,16,24,24" SafeAreaEdges="Container">

        <!-- Колокол со звуковыми волнами; анимация — в RingPage.xaml.cs. -->
        <Grid Grid.Row="1" WidthRequest="180" HeightRequest="180" HorizontalOptions="Center">
            <Ellipse x:Name="Wave1" Stroke="{StaticResource Accent}" StrokeThickness="2" Opacity="0" />
            <Ellipse x:Name="Wave2" Stroke="{StaticResource Accent}" StrokeThickness="2" Opacity="0" />
            <Ellipse x:Name="Wave3" Stroke="{StaticResource Accent}" StrokeThickness="2" Opacity="0" />
            <Image x:Name="Bell" Source="bell.png" WidthRequest="90" HeightRequest="103" AnchorY="0.08"
                   HorizontalOptions="Center" VerticalOptions="Center" />
        </Grid>

        <Label Grid.Row="2" Text="{Binding TimeText}" FontFamily="ManropeLight" FontSize="72"
               HorizontalOptions="Center" Margin="0,24,0,0" />
        <Label Grid.Row="3" Text="{Binding Label}" FontFamily="ManropeSemiBold" FontSize="20" HorizontalOptions="Center" />
        <Label Grid.Row="4" Text="{Binding DateText}" Style="{StaticResource Muted}" FontSize="14" HorizontalOptions="Center" />

        <Button Grid.Row="6" Text="{x:Static res:AppResources.Stop}" Style="{StaticResource PrimaryButton}"
                FontSize="18" CornerRadius="24" Padding="0,18" Command="{Binding StopCommand}" />
    </Grid>
</ContentPage>
```

Файл `src/Chrono/Views/RingPage.xaml.cs`:

```csharp
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class RingPage : ContentPage
{
    public RingPage(RingViewModel viewModel)
    {
        this.InitializeComponent();
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Колокол качается -14° ↔ 14° бесконечно.
        new Animation
        {
            { 0, 0.5, new Animation(v => this.Bell.Rotation = v, -14, 14, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => this.Bell.Rotation = v, 14, -14, Easing.SinInOut) },
        }.Commit(this.Bell, "swing", length: 1000, repeat: () => true);

        // Три волны расходятся со сдвигом в треть периода.
        var waves = new[] { this.Wave1, this.Wave2, this.Wave3 };
        for (var i = 0; i < waves.Length; i++)
        {
            var wave = waves[i];
            var shift = i / 3.0;
            new Animation(v =>
            {
                var phase = (v + shift) % 1;
                wave.Scale = 0.55 + 0.7 * phase;
                wave.Opacity = 0.7 * (1 - phase);
            }).Commit(wave, "wave", length: 2000, repeat: () => true);
        }
    }

    protected override void OnDisappearing()
    {
        this.Bell.AbortAnimation("swing");
        this.Wave1.AbortAnimation("wave");
        this.Wave2.AbortAnimation("wave");
        this.Wave3.AbortAnimation("wave");
        base.OnDisappearing();
    }
}
```

Файл `src/Chrono/Views/ReliabilityPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:vm="clr-namespace:Chrono.ViewModels"
             xmlns:res="clr-namespace:Chrono.Resources.Strings"
             x:Class="Chrono.Views.ReliabilityPage"
             x:DataType="vm:ReliabilityViewModel">

    <Grid RowDefinitions="Auto,*,Auto" Padding="20,12,20,16" SafeAreaEdges="Container">
        <Label Text="{x:Static res:AppResources.ReliabilityTitle}" FontFamily="ManropeSemiBold" FontSize="24" Margin="0,0,0,12" />

        <ScrollView Grid.Row="1">
            <VerticalStackLayout Spacing="10">
                <VerticalStackLayout Spacing="10" BindableLayout.ItemsSource="{Binding Rows}">
                    <BindableLayout.ItemTemplate>
                        <DataTemplate x:DataType="vm:ReliabilityRow">
                            <Border Style="{StaticResource Card}">
                                <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
                                    <Label Text="{Binding Mark}" FontSize="18" VerticalOptions="Center" />
                                    <Label Grid.Column="1" Text="{Binding Title}" VerticalOptions="Center" />
                                    <Button Grid.Column="2" Text="{x:Static res:AppResources.Fix}" Style="{StaticResource SecondaryButton}"
                                            Padding="14,6" FontSize="13" IsVisible="{Binding NeedsFix}"
                                            Command="{Binding FixCommand, Source={RelativeSource AncestorType={x:Type vm:ReliabilityViewModel}}, x:DataType=vm:ReliabilityViewModel}"
                                            CommandParameter="{Binding .}" />
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </BindableLayout.ItemTemplate>
                </VerticalStackLayout>
                <Label Text="{x:Static res:AppResources.OemHint}" Style="{StaticResource Muted}" IsVisible="{Binding IsAndroid}" Margin="4,8" />
                <Label Text="{x:Static res:AppResources.SilentHint}" Style="{StaticResource Muted}" IsVisible="{Binding IsIos}" Margin="4,8" />
            </VerticalStackLayout>
        </ScrollView>

        <Button Grid.Row="2" Text="{x:Static res:AppResources.Ok}" Style="{StaticResource PrimaryButton}"
                HorizontalOptions="End" Command="{Binding CloseCommand}" />
    </Grid>
</ContentPage>
```

Файл `src/Chrono/Views/ReliabilityPage.xaml.cs`:

```csharp
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class ReliabilityPage : ContentPage
{
    private readonly ReliabilityViewModel viewModel;
    private Window? window;

    public ReliabilityPage(ReliabilityViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Возврат из системных настроек не вызывает OnAppearing — обновляемся и при возобновлении окна.
        // Page.Window у страницы, открытой через Shell, в OnAppearing ещё null (проверено на эмуляторе: NRE),
        // поэтому берём окно приложения.
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed += this.OnWindowResumed;
        }

        await this.viewModel.RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
        }

        base.OnDisappearing();
    }

    private async void OnWindowResumed(object? sender, EventArgs e) => await this.viewModel.RefreshAsync();
}
```


- [ ] **Шаг 3: Сборка.**
  Run: `dotnet build src/Chrono/Chrono.csproj -f net11.0-android` и `-f net11.0-ios`.
  Expected: 0 ошибок и 0 предупреждений.
  Compiled bindings (`x:DataType`) проверяются при сборке, поэтому опечатка в имени свойства даёт ошибку, а не пустое поле. Внешний вид проверяется рендером в задаче 7: страницы ещё не подключены к Shell.

- [ ] **Шаг 4: Коммит.**

```bash
FILES="src/Chrono/Services/RingLauncher.cs src/Chrono/ViewModels/AlarmListViewModel.cs src/Chrono/ViewModels/AlarmEditViewModel.cs src/Chrono/ViewModels/RingViewModel.cs src/Chrono/ViewModels/ReliabilityViewModel.cs src/Chrono/Views/SplashPage.cs src/Chrono/Views/AlarmListPage.xaml src/Chrono/Views/AlarmListPage.xaml.cs src/Chrono/Views/AlarmEditPage.xaml src/Chrono/Views/AlarmEditPage.xaml.cs src/Chrono/Views/RingPage.xaml src/Chrono/Views/RingPage.xaml.cs src/Chrono/Views/ReliabilityPage.xaml src/Chrono/Views/ReliabilityPage.xaml.cs"
git add $FILES
git commit -m "Экраны: splash, список, редактор, сигнал, надёжность и их ViewModel" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 7: Интеграция и первый запуск

**Модель:** sonnet. **Ветка:** `feature/chrono-v1` после слияния волны 3.
**Skills:** `dotnet-maui:maui-dependency-injection`, `maui-shell-navigation`, `maui-app-lifecycle`, `maui-safe-area`. **Прочитать до начала:** `~/.claude/references/fix-user-experience.md`.

**Файлы:**
- Modify (заменить промежуточные версии): `src/Chrono/App.xaml.cs`, `src/Chrono/AppShell.xaml`, `src/Chrono/AppShell.xaml.cs`, `src/Chrono/MauiProgram.cs`, `src/Chrono/Platforms/Android/MainActivity.cs`.
- Create: `src/Chrono/Platforms/iOS/NotificationDelegate.cs`.
- Delete: `src/Chrono/MainPage.xaml`, `src/Chrono/MainPage.xaml.cs`.

**Interfaces:**
- Consumes: всё из задач 2, 4, 5, 6.
- Produces: собранное приложение. Регистрации DI:
  - singleton: `TimeProvider.System`, `AlarmStore`, `ISoundPlayer`, `IAlarmScheduler`, `IReliabilityChecks`, `AlarmService`, `AppShell`;
  - transient: страницы и ViewModel.

- [ ] **Шаг 1: Файлы интеграции.**

Файл `src/Chrono/App.xaml.cs`:

```csharp
using Chrono.Services;
using Chrono.Views;

namespace Chrono;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services, AlarmService alarmService)
    {
        this.InitializeComponent();
        this.services = services;
        // Приложение всегда тёмное: светлые иконки статус-бара и тёмные диалоги пикеров при любой системной теме.
        this.UserAppTheme = AppTheme.Dark;
        // Восстанавливаем расписание: после «Остановить принудительно» Android стирает будильники приложения.
        alarmService.RescheduleAll();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Запуск по сигналу — без splash, сразу Shell; RingPage откроет AlarmListPage через RingLauncher.
        Page first = RingLauncher.PendingId is null
            ? new SplashPage(this.services)
            : this.services.GetRequiredService<AppShell>();
        return new Window(first);
    }
}
```

Файл `src/Chrono/AppShell.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell xmlns:views="clr-namespace:Chrono.Views"
       x:Class="Chrono.AppShell"
       Shell.NavBarIsVisible="False"
       Shell.FlyoutBehavior="Disabled">

    <ShellContent ContentTemplate="{DataTemplate views:AlarmListPage}" Route="list" />

</Shell>
```

Файл `src/Chrono/AppShell.xaml.cs`:

```csharp
using Chrono.Views;

namespace Chrono;

public partial class AppShell : Shell
{
    public AppShell()
    {
        this.InitializeComponent();
        Routing.RegisterRoute("edit", typeof(AlarmEditPage));
        Routing.RegisterRoute("ring", typeof(RingPage));
        Routing.RegisterRoute("reliability", typeof(ReliabilityPage));
    }
}
```

Файл `src/Chrono/MauiProgram.cs`:

```csharp
using Chrono.Platform;
using Chrono.Services;
using Chrono.ViewModels;
using Chrono.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Chrono;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Light.ttf", "ManropeLight");
                fonts.AddFont("Manrope-Regular.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if IOS
                events.AddiOS(ios => ios.FinishedLaunching((app, options) =>
                {
                    // Делегат назначается до окончания запуска — иначе нажатие на уведомление при холодном старте теряется.
                    UserNotifications.UNUserNotificationCenter.Current.Delegate = new NotificationDelegate();
                    return true;
                }));
#endif
            });

#if ANDROID
        // Нативное подчёркивание Android у полей лишнее: поля уже стоят в карточках с рамкой.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new AlarmStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<ISoundPlayer, SoundPlayer>();
        builder.Services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
        builder.Services.AddSingleton<IReliabilityChecks, ReliabilityChecks>();
        builder.Services.AddSingleton<AlarmService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<AlarmListViewModel>();
        builder.Services.AddTransient<AlarmListPage>();
        builder.Services.AddTransient<AlarmEditViewModel>();
        builder.Services.AddTransient<AlarmEditPage>();
        builder.Services.AddTransient<RingViewModel>();
        builder.Services.AddTransient<RingPage>();
        builder.Services.AddTransient<ReliabilityViewModel>();
        builder.Services.AddTransient<ReliabilityPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

Файл `src/Chrono/Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Chrono.Platform;
using Chrono.Services;

namespace Chrono;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Запуск из full-screen intent сигнала: показываемся поверх блокировки и открываем RingPage.
        if (Guid.TryParse(this.Intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId))
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(27))
            {
                this.SetShowWhenLocked(true);
                this.SetTurnScreenOn(true);
            }
            else
            {
                this.Window!.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
            }

            RingLauncher.Request(alarmId);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // Активити уже запущена (SingleTop): тот же сценарий, что и в OnCreate.
        if (Guid.TryParse(intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var alarmId))
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(27))
            {
                this.SetShowWhenLocked(true);
                this.SetTurnScreenOn(true);
            }
            else
            {
                this.Window!.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
            }

            RingLauncher.Request(alarmId);
        }
    }
}
```

Файл `src/Chrono/Platforms/iOS/NotificationDelegate.cs`:

```csharp
using Chrono.Services;
using Foundation;
using UserNotifications;

namespace Chrono.Platform;

/// <summary>
/// Нажатие на уведомление и уведомление при открытом приложении → RingPage.
/// Назначается в FinishedLaunching, чтобы ловить и запуск приложения нажатием.
/// </summary>
public sealed class NotificationDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        // Приложение на экране: баннер не показываем, сразу экран сигнала с мелодией в цикле.
        if (Guid.TryParse(notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var services = IPlatformApplication.Current!.Services;
            var alarm = services.GetRequiredService<AlarmStore>().Load().Alarms.FirstOrDefault(a => a.Id == alarmId);
            var ids = IosNotificationPlan.ChainIds(alarmId).ToArray();
            center.RemovePendingNotificationRequests(ids);
            center.RemoveDeliveredNotifications(ids);

            if (alarm is { SoundEnabled: true })
            {
                services.GetRequiredService<ISoundPlayer>().Play(alarm.SoundId, loop: true);
            }

            if (alarm is { VibrationEnabled: true })
            {
                Vibration.Default.Vibrate(TimeSpan.FromSeconds(1));
            }

            RingLauncher.Request(alarmId);
        }

        completionHandler(UNNotificationPresentationOptions.None);
    }

    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        if (Guid.TryParse(response.Notification.Request.Content.UserInfo[AlarmScheduler.AlarmIdKey]?.ToString(), out var alarmId))
        {
            var ids = IosNotificationPlan.ChainIds(alarmId).ToArray();
            center.RemovePendingNotificationRequests(ids);
            center.RemoveDeliveredNotifications(ids);
            RingLauncher.Request(alarmId);
        }

        completionHandler();
    }
}
```


- [ ] **Шаг 2: Удалить заглушку.**
  PowerShell: `Remove-Item -LiteralPath "src/Chrono/MainPage.xaml","src/Chrono/MainPage.xaml.cs" -Force`.

- [ ] **Шаг 3: Полная сборка и тесты.**
  Run: `dotnet build Chrono.slnx` и `dotnet test Chrono.slnx`.
  Expected: `0 Warning(s)`, `0 Error(s)`; тесты `succeeded: 21`.

- [ ] **Шаг 4: Первый запуск — рендер.**
  Подготовка:
  - установить APK по правилу из «Окружения»;
  - `adb shell pm clear com.chrono.alarm`;
  - `adb logcat -c`;
  - запустить через `monkey`.

  Скриншоты:
  1. Через 0,9 с — **splash**: колокол со свечением по центру на сплошном `#0A0B1A`.
  2. Через 6 с — **список** («Chrono», «No alarms yet…», кнопка «+») с системным запросом уведомлений поверх.
  3. Нажать «Allow» → **«Надёжность»**: три пункта ✅, батарея ⚠️ с кнопкой «Fix», подсказка про Xiaomi/Huawei/Samsung, кнопка «OK» справа внизу.
  4. «OK» → список. «+» → **редактор**: крупное время, дата янтарным, поле «Text» в рамке, чипы мелодий, переключатели, внизу справа «Cancel» и «Save».

  Иконки статус-бара светлые. Подчёркиваний у полей нет.
  Проверка падений: `adb logcat -d | grep -E "E AndroidRuntime|F monodroid"` — пусто.

- [ ] **Шаг 5: Живой ввод по памятке** (поле «Текст»).
  Включите экранную клавиатуру (см. «Окружение»), коснитесь поля и выполните:

```bash
export MSYS_NO_PATHCONV=1; A="/c/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe"; D="-s <serial>"
readf() { "$A" $D shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1; "$A" $D shell cat /sdcard/ui.xml | grep -oE '<node [^>]*class="android.widget.EditText"[^>]*hint="Alarm text"[^>]*' | grep -oE ' text="[^"]*"'; }
text="Wake up"; acc=""
for ((i=0;i<${#text};i++)); do ch="${text:$i:1}"; if [ "$ch" = " " ]; then "$A" $D shell input keyevent KEYCODE_SPACE; else "$A" $D shell input text "$ch"; fi; acc="$acc$ch"; echo "'$ch' →$(readf)  ожидается \"$acc\""; done
for k in 1 2; do "$A" $D shell input keyevent KEYCODE_DEL; done; echo "Backspace×2 →$(readf)  ожидается \"Wake \""
"$A" $D shell input text "u"; "$A" $D shell input text "p"; echo "+up →$(readf)  ожидается \"Wake up\""
"$A" $D exec-out screencap -p > keyboard.png
```

  Expected:
  - каждая строка совпадает с ожиданием посимвольно;
  - на `keyboard.png` видны поле в фокусе и кнопки «Cancel»/«Save» **над** полной клавиатурой.
  Затем «Save» → в списке карточка `HH:mm` с подписью «ddd, d MMM · Wake up» и строка «Next alarm in …».
  Верните настройки клавиатуры.

- [ ] **Шаг 6: Коммит.**

```bash
FILES="src/Chrono/App.xaml.cs src/Chrono/AppShell.xaml src/Chrono/AppShell.xaml.cs src/Chrono/MauiProgram.cs src/Chrono/Platforms/Android/MainActivity.cs src/Chrono/Platforms/iOS/NotificationDelegate.cs src/Chrono/MainPage.xaml src/Chrono/MainPage.xaml.cs"
git add $FILES
git commit -m "Интеграция: DI, Shell-маршруты, splash, запуск сигнала из ОС" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

  (`git add` удалённых `MainPage.*` индексирует их удаление.)

---

## Task 8: Приёмка на Android-эмуляторе

**Модель:** sonnet. **Ветка:** `feature/chrono-v1`. **Инструменты:** adb, MCP `maui-devflow` (по желанию), skill `impeccable:impeccable` (сценарий A13).
**Результат:** отчёт `docs/superpowers/reports/2026-10-06-chrono-acceptance.md` — таблица «сценарий | шаги | ожидалось | факт | доказательство (скриншот/вывод dumpsys)».
Код **не менять**. Дефекты описать в отчёте и вернуть координатору.
**A2 (перезагрузка) и A9 (смена пояса) меняют состояние эмулятора.** Если запущен чужой AVD (например, `mobileasr_test`), сначала спросите координатора — он получит согласие пользователя. Пояс после A9 вернуть.

Общая подготовка:
- установить APK по правилу из «Окружения» и выполнить `pm clear`;
- разрешить уведомления;
- `D="-s <serial>"`, `P=com.chrono.alarm`.

Будильник создаётся одним из двух способов:
- через UI (как в задаче 7);
- записью файла с последующим запуском приложения: `App` при старте вызывает `RescheduleAll`.

```bash
export MSYS_NO_PATHCONV=1; A="/c/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe"; D="-s <serial>"; P=com.chrono.alarm
# Будильник через N секунд по часам эмулятора (Id и Label — параметры).
mkalarm() { local at=$("$A" $D shell date -d @$(( $("$A" $D shell date +%s | tr -d '\r') + $1 )) +%Y-%m-%dT%H:%M:%S | tr -d '\r')
  echo "{\"Id\":\"$2\",\"At\":\"$at\",\"Label\":\"$3\",\"SoundEnabled\":${4:-true},\"SoundId\":\"bell\",\"VibrationEnabled\":${5:-true},\"IsEnabled\":true}"; }
putalarms() { "$A" $D shell "run-as $P sh -c 'mkdir -p files && echo '\''$1'\'' > files/alarms.json'"; "$A" $D shell monkey -p $P -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1; sleep 8; }
killapp() { "$A" $D shell input keyevent KEYCODE_HOME; sleep 1; "$A" $D shell run-as $P kill -9 $("$A" $D shell pidof $P | tr -d '\r'); "$A" $D shell input keyevent KEYCODE_SLEEP; }
```

- [ ] **A1. Убитый процесс, заблокированный экран** (Review Focus 1).
  Шаги: `putalarms "[$(mkalarm 90 11111111-0000-0000-0000-000000000001 A1)]"`, затем `killapp`, затем ждать до времени будильника + 10 с.
  Expected:
  - `dumpsys activity services $P`: `AlarmRingService`, `isForeground=true`, `types=0x00000400`;
  - `dumpsys power`: `mWakefulness=Awake`;
  - `dumpsys activity activities`: top — `MainActivity`;
  - `dumpsys audio`: плеер `usage=USAGE_ALARM`, `state:started`. Записать в отчёт значение `mutedState` и строку `AudioHardening`, если есть;
  - `dumpsys vibrator_manager`: вибрация с usage ALARM;
  - скриншот: `RingPage` (качающийся колокол, время, «A1», дата, «Stop»).
  После нажатия «Stop» `AlarmRingService` и уведомление пропадают.
- [ ] **A2. Перезагрузка.**
  Шаги: будильник на +240 с, затем `adb reboot`, ожидание `sys.boot_completed=1`, разблокировка (`adb shell wm dismiss-keyguard`).
  Expected: `dumpsys alarm` содержит `AlarmReceiver` для `com.chrono.alarm`; сигнал приходит вовремя.
- [ ] **A3. Doze.**
  Шаги: будильник на +120 с, `killapp`, `adb shell dumpsys deviceidle force-idle`.
  Expected: сигнал приходит в пределах 5 с от заданного времени (сравнить время из logcat). В конце — `adb shell dumpsys deviceidle unforce`.
- [ ] **A4. Выключенный не звонит; сработавший выключается.**
  Шаги: два будильника, один выключить в UI, дождаться времени обоих.
  Expected: выключенный не звонит. После «Stop» у сработавшего в списке переключатель выключен, карточка приглушена.
- [ ] **A5. Только вибрация** (`SoundEnabled=false`).
  Expected: в `dumpsys audio` нет плеера `USAGE_ALARM` от приложения, вибрация есть.
  **Только звук** (`VibrationEnabled=false`): плеер есть, вибрации от приложения нет.
- [ ] **A6. Ввод в поле «Текст»** — повтор шага 5 задачи 7 на свежей установке.
- [ ] **A7. Локали.** Сравнить скриншоты списка, редактора, «Надёжности» и сигнала в `en-US` и в `ru-RU` (`cmd locale set-app-locales`). Все строки переведены, кириллица в Manrope.
- [ ] **A8. Визуал:** скриншоты splash (0,9 с после запуска), иконки в лаунчере, «Надёжности».
- [ ] **A9. Смена часового пояса** (Review Focus 2).
  Шаги: будильник 06:30 через UI, затем `adb shell cmd alarm set-timezone Asia/Tashkent`. Если команды нет, сменить пояс в настройках.
  Expected:
  - в `alarms.json` (`run-as ... cat files/alarms.json`) `At` без смещения и с прежним «06:30»;
  - в `dumpsys alarm` время срабатывания пересчитано на 06:30 нового пояса;
  - в списке карточка по-прежнему показывает 06:30.
  Вернуть исходный пояс.
- [ ] **A10. Срабатывание удалённого будильника** (Review Focus 3).
  Шаги: будильник на +90 с через `putalarms`, затем `run-as` перезаписать `files/alarms.json` в `[]`, не открывая приложение; ждать.
  Expected: в logcat нет `E AndroidRuntime`, `ForegroundServiceDidNotStartInTimeException` и `F monodroid`. Сервис стартовал и остановился (`dumpsys activity services` пусто через 10 с).
- [ ] **A11. Два будильника в одну минуту** (Review Focus 4).
  Шаги: два будильника с одинаковым `At` (+90 с), `killapp`, ждать.
  Expected: один экран сигнала. После «Stop» экран со списком, а не второй `RingPage` (скриншот).
- [ ] **A12. Включение прошедшего** (Review Focus 5).
  Шаги: будильник на +60 с через UI; после срабатывания и «Stop» включить его переключателем в списке.
  Expected: открывается редактор «Edit alarm» с красной подсказкой «This alarm's time has passed. Choose a new date.»; в списке он не включился.
- [ ] **A13. Дизайн-ревью.** Skill `impeccable:impeccable` по скриншотам A1, A7, A8 в режиме critique. Отчёт — список замечаний с приоритетом; изменения не вносить.
- [ ] **Итог.** Отчёт закоммитить:

```bash
FILES="docs/superpowers/reports/2026-10-06-chrono-acceptance.md"
git add $FILES
git commit -m "Отчёт приёмки Chrono на Android-эмуляторе" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $FILES
```

---

## Task 9: Финальное ревью ветки

**Модель:** opus. **Skill:** `superpowers:requesting-code-review`. Только чтение. Дифф `main...feature/chrono-v1` передаётся файлом (`git diff main...feature/chrono-v1 > %TEMP%\chrono-branch.diff`).

Конкретные риски для проверки. Ревьюеру разрешено открывать смежные файлы целиком: **конец хунка не равен концу файла**.
1. Все пути `AlarmRingService.OnStartCommand` — таблицей, включая `intent == null`, нераспознанный id, отсутствующий будильник и повторный старт. В каждом пути `StartForeground` вызван до выхода, ресурсы освобождаются в `OnDestroy`.
2. Все вызовы `AlarmStore.Save` идут с главного потока: UI, `BootReceiver.OnReceive`, `AlarmRingService`.
3. Нет ли пути, где включённый будильник в файле не попадает в расписание ОС: ошибка `Sync` при `Save`/`Toggle`/`Delete`/`RescheduleAll` и её отображение плашкой.
4. `RingLauncher` при холодном старте (Splash → Shell → `AlarmListPage.OnAppearing`) и при активном `RingPage`.
5. iOS: `TimeSensitive2`, `UserInfo["alarm_id"]` совпадает в `AlarmScheduler` и `NotificationDelegate`, цепочка снимается при нажатии, при показе на переднем плане и при «Стоп».
6. Соответствие Global Constraints: стиль полей, отсутствие лишних private-помощников, строки только в resx, кнопки модальной формы справа.

Находки ранжировать по серьёзности. Спорные находки координатор проверяет сам до отправки на исправление.
