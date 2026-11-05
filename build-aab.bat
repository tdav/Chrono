@echo off
rem Сборка .aab для Google Play, подпись ключом загрузки SataKeyStore (Keystore\ в .gitignore).
rem Google Play переподписывает пакет своим ключом (Play App Signing) — OAuth-клиент нужен и под его SHA-1.
rem Использование: build-aab.bat [versionCode] — каждая загрузка в Play требует versionCode больше прежнего.
rem Пароль спрашивается без отображения и передаётся сборке через переменную окружения, а не в командной строке.
chcp 65001 >nul
setlocal

set "KEYSTORE=%~dp0Keystore\SataKeyStore\SataKeyStore.keystore"
if not defined CHRONO_UPLOAD_ALIAS set "CHRONO_UPLOAD_ALIAS=satakeystore"
set "OUT=%~dp0bin\aab"

if not exist "%KEYSTORE%" (
    echo Не найден ключ загрузки: %KEYSTORE%
    exit /b 1
)

set "VERSION_ARG="
if not "%~1"=="" set "VERSION_ARG=-p:ApplicationVersion=%~1"

if not defined CHRONO_UPLOAD_PASS (
    for /f "usebackq delims=" %%p in (`powershell -NoProfile -Command "$s = Read-Host 'Пароль ключа SataKeyStore' -AsSecureString; [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))"`) do set "CHRONO_UPLOAD_PASS=%%p"
)
if not defined CHRONO_UPLOAD_PASS (
    echo Пароль не введён.
    exit /b 1
)

dotnet publish "%~dp0src\Chrono\Chrono.csproj" -f net11.0-android -c Release -o "%OUT%" %VERSION_ARG% ^
    -p:AndroidPackageFormat=aab ^
    -p:AndroidKeyStore=true ^
    -p:AndroidSigningKeyStore="%KEYSTORE%" ^
    -p:AndroidSigningKeyAlias=%CHRONO_UPLOAD_ALIAS% ^
    -p:AndroidSigningKeyPass=env:CHRONO_UPLOAD_PASS ^
    -p:AndroidSigningStorePass=env:CHRONO_UPLOAD_PASS
if errorlevel 1 (
    echo Сборка не удалась.
    exit /b 1
)

echo.
echo Готово: %OUT%\com.chrono.sata-Signed.aab
echo Загрузка: Play Console - Тестирование - выпуск - загрузить этот файл.
