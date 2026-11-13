# Иконки дел: 12 → 50

Сделано по брифу: 38 новых ключей в конец `ItemIcons.Keys` (порядок и значения из брифа), 38 SVG `icon_item_{key}.svg` (24×24, штрих 2.2, #2E3136, без заливки, нарисованы вручную), подписи `Icon_{key}` в `AppResources.resx` и `AppResources.ru.resx`, `IconSheet.xaml`: 5 колонок (`Basis="20%"`, кружок 56), сетка в `ScrollView MaximumHeightRequest="320"`, кнопки «Отмена»/«OK» вне ScrollView, комментарий обновлён.

Высота 320 dp выбрана так: панель шторки прижата к низу страницы (`BottomSheet`, `VerticalOptions="End"`), служебная часть (ручка, заголовок, кнопки, отступы) около 195 dp; 320 + 195 = 515 dp, влезает в экран ~640 dp с запасом на статус-бар. Подгонка не нужна: `MaximumHeightRequest` у `ScrollView` внутри `VerticalStackLayout` работает.

## Проверки
- `dotnet test tests/Chrono.Tests`: 299 из 299 (число тестов не менялось).
- `dotnet build -f net11.0-android -p:EmbedAssembliesIntoApk=true -o %TEMP%/chrono-icons`: 0 ошибок.
- Эмулятор emulator-5554: `adb install -r` (данные сохранены), шторка открыта, прокручена до конца, выбран цветок, виден в строке редактора. Выход через «Discard» без сохранения. Таймер 17:32 в `dumpsys alarm` на месте, `force-stop` не делался.

## Файлы (не в git)
- Лист иконок (все 50, крупно и в 26 px): `icons-sheet.png` (+ `icons-sheet.html`)
- Шторка вверху: `icons-shot-top.png`
- Шторка внизу: `icons-shot-bottom.png`
- Редактор с иконкой «Цветы»: `icons-shot-editor.png`

## Замечания
- `egg` («Завтрак») — просто яйцо; жареное яйцо читалось как мишень, яйцо в рюмке — как корзина. `tea` — чайник, `child` — лицо ребёнка с завитком, `money` — банкнота без знака валюты (без текста).
- Ключ `camera`: en «Photo», ru «Фото» (по брифу; у ключа `game` en «Games»).
- Пятый ряд при прокрутке виден лишь краем: подсказки прокрутки почти нет, но жест прокрутки работает (проверено).
- Запуск приложения на эмуляторе: Activity `com.chrono.alarm/crc64fd681c359b5f5c68.MainActivity`.
