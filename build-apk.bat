@echo off
rem Сборка подписанного release APK Chrono ключом MyTest (тот же ключ, что у сборки на телефоне:
rem под его SHA-1 заведён OAuth-клиент «Chrono Android (release)», иначе вход Google не пройдёт).
rem Пароль спрашивается без отображения и передаётся сборке через переменную окружения, а не в командной строке.
chcp 65001 >nul
setlocal

set "KEYSTORE=%LOCALAPPDATA%\Xamarin\Mono for Android\Keystore\MyTest\MyTest.keystore"
if not defined CHRONO_KEY_ALIAS set "CHRONO_KEY_ALIAS=MyTest"
set "OUT=%~dp0bin\apk"

if not exist "%KEYSTORE%" (
    echo Не найден ключ: %KEYSTORE%
    exit /b 1
)

if not defined CHRONO_KEY_PASS (
    for /f "usebackq delims=" %%p in (`powershell -NoProfile -Command "$s = Read-Host 'Пароль ключа MyTest' -AsSecureString; [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))"`) do set "CHRONO_KEY_PASS=%%p"
)
if not defined CHRONO_KEY_PASS (
    echo Пароль не введён.
    exit /b 1
)

dotnet publish "%~dp0src\Chrono\Chrono.csproj" -f net11.0-android -c Release -o "%OUT%" ^
    -p:AndroidKeyStore=true ^
    -p:AndroidSigningKeyStore="%KEYSTORE%" ^
    -p:AndroidSigningKeyAlias=%CHRONO_KEY_ALIAS% ^
    -p:AndroidSigningKeyPass=env:CHRONO_KEY_PASS ^
    -p:AndroidSigningStorePass=env:CHRONO_KEY_PASS
if errorlevel 1 (
    echo Сборка не удалась.
    exit /b 1
)

echo.
echo Готово: %OUT%\com.chrono.alarm-Signed.apk
echo Установка на подключённый телефон: adb install -r "%OUT%\com.chrono.alarm-Signed.apk"
