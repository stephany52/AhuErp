@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem =========================================================================
rem AhuErp - регенерация .resx-снапшота последней EF6-миграции.
rem
rem Что делает:
rem   1. Патчит tools\MigrationGenerator\App.config твоей строкой подключения
rem      (в конце скрипта восстанавливает оригинал).
rem   2. Удаляет scaffold-БД (по умолчанию AhuErpDb_Scaffold).
rem   3. Билдит src\AhuErp.Core и tools\MigrationGenerator (через dotnet).
rem   4. Запускает MigrationGenerator: он накатывает все Up()-миграции
rem      на пустую БД и просит EF6 MigrationScaffolder сгенерировать
rem      "дельту" между текущей моделью DbContext и последним .resx-Target.
rem      В сгенерированном .resx будет КОРРЕКТНЫЙ полный снапшот модели.
rem   5. Копирует свежий .resx поверх последней миграции
rem      (по умолчанию 202604300000000_AddSearchIndex.resx) и удаляет
rem      временные .cs / .Designer.cs / *_ResxSnapshot.resx.
rem
rem Использование:
rem   tools\regen-migrations.bat
rem   tools\regen-migrations.bat "DESKTOP-I1OTVEB\SQLEXPRESS"
rem   tools\regen-migrations.bat "DESKTOP-I1OTVEB\SQLEXPRESS" AhuErpDb_Scaffold
rem
rem Требования:
rem   - dotnet SDK (любой современный, скачивает net48 reference assemblies)
rem   - sqlcmd (SQL Server Command Line Utilities)
rem   - SQL Server (LocalDB / Express / любой), доступный по Integrated Security
rem
rem После успешного прогона:
rem   - в Migrations\ обновлён только .resx последней миграции;
rem   - тебе остаётся открыть Package Manager Console в Visual Studio и
rem     выполнить `Add-Migration TestEmpty` - получится пустая Up()/Down()
rem     - значит модель и слепок синхронны, .resx можно коммитить.
rem =========================================================================

if "%~1" neq "" (
    set "SQLSERVER=%~1"
) else if not defined SQLSERVER (
    set "SQLSERVER=DESKTOP-I1OTVEB\SQLEXPRESS"
)

if "%~2" neq "" (
    set "SCAFFOLD_DB=%~2"
) else if not defined SCAFFOLD_DB (
    set "SCAFFOLD_DB=AhuErpDb_Scaffold"
)

rem --- валидируем имена, которые подставляются в T-SQL, от инъекции -------
rem  SCAFFOLD_DB идёт в `DROP DATABASE [...]`, SQLSERVER - в `-S ...` и в
rem  App.config; допускаем только безопасный набор символов.
echo %SCAFFOLD_DB%| findstr /R /X "[A-Za-z0-9_][A-Za-z0-9_]*" >nul || (
    echo [X] SCAFFOLD_DB contains invalid characters: %SCAFFOLD_DB%
    echo     Allowed: letters, digits, underscore.
    goto :err
)
echo %SQLSERVER%| findstr /R /X "[A-Za-z0-9_.\\-]*" >nul || (
    echo [X] SQLSERVER contains invalid characters: %SQLSERVER%
    echo     Allowed: letters, digits, underscore, dot, backslash, hyphen.
    goto :err
)

set "TARGETMIG=202604300000000_AddSearchIndex"

set "ROOT=%~dp0.."
pushd "%ROOT%" >nul
set "ROOT=%CD%"
popd >nul

set "CORE=%ROOT%\src\AhuErp.Core"
set "MIGS=%CORE%\Migrations"
set "GEN=%ROOT%\tools\MigrationGenerator"
set "APPCFG=%GEN%\App.config"
rem  GENEXE / GENEXEDIR определяются после сборки (находим .exe рекурсивно,
rem  чтобы не зависеть от того, кладёт ли SDK в bin\Debug\ или bin\Debug\net48\)
set "GENEXE="
set "GENEXEDIR="

echo.
echo === AhuErp resx regenerator ===
echo SQL Server instance : %SQLSERVER%
echo Scaffold DB         : %SCAFFOLD_DB%
echo Repo root           : %ROOT%
echo Target migration    : %TARGETMIG%
echo.

rem --- 0/7: проверяем инструменты ------------------------------------------
where dotnet >nul 2>&1 || (
    echo [X] dotnet not found in PATH. Install .NET SDK from https://dotnet.microsoft.com/download
    goto :err
)
where sqlcmd >nul 2>&1 || (
    echo [X] sqlcmd not found in PATH. Install "SQL Server Command Line Utilities".
    goto :err
)
where powershell >nul 2>&1 || (
    echo [X] powershell.exe not found, что странно. Скрипт использует его для патча App.config.
    goto :err
)

rem --- 1/7: патчим App.config (с бекапом) ----------------------------------
echo === [1/7] Patching App.config connection string
copy /Y "%APPCFG%" "%APPCFG%.bak" >nul || goto :err

set "PATCHED_CONN=Data Source=%SQLSERVER%;Initial Catalog=%SCAFFOLD_DB%;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$cfg = '%APPCFG%';" ^
    "$xml = [xml](Get-Content -Raw -LiteralPath $cfg);" ^
    "$node = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AhuErpDb' };" ^
    "$node.providerName = 'System.Data.SqlClient';" ^
    "$node.connectionString = '%PATCHED_CONN%';" ^
    "$xml.Save($cfg);" || goto :err

rem --- 2/7: дропаем scaffold-БД --------------------------------------------
echo === [2/7] Drop %SCAFFOLD_DB% (если существует)
sqlcmd -S "%SQLSERVER%" -E -b -Q "IF DB_ID('%SCAFFOLD_DB%') IS NOT NULL BEGIN ALTER DATABASE [%SCAFFOLD_DB%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [%SCAFFOLD_DB%]; END" || goto :err

rem --- 3/7: билдим Core ----------------------------------------------------
echo === [3/7] Build AhuErp.Core
dotnet build "%CORE%\AhuErp.Core.csproj" -c Debug -v minimal --nologo || goto :err

rem --- 4/7: билдим MigrationGenerator --------------------------------------
echo === [4/7] Build MigrationGenerator
dotnet build "%GEN%\MigrationGenerator.csproj" -c Debug -v minimal --nologo || goto :err

rem --- 5/7: бекапим старые .resx и запускаем scaffolder --------------------
echo === [5/7] Backup existing .resx and scaffold a fresh snapshot
set "BACKUPDIR=%ROOT%\Migrations.backup-%RANDOM%%RANDOM%"
mkdir "%BACKUPDIR%" >nul 2>&1
copy /Y "%MIGS%\202604270000000_AddOrgAndSubstitution.resx" "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\202604280000000_AddNotifications.resx"     "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\202604290000000_AddSignatures.resx"        "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\202604300000000_AddSearchIndex.resx"       "%BACKUPDIR%\" >nul
echo Old .resx backed up to: %BACKUPDIR%

for /f "delims=" %%F in ('dir /B /S /A:-D "%GEN%\bin\Debug\MigrationGenerator.exe" 2^>nul') do set "GENEXE=%%F"
if not defined GENEXE (
    echo [X] Build succeeded but MigrationGenerator.exe not found under %GEN%\bin\Debug
    goto :err
)
for %%D in ("%GENEXE%") do set "GENEXEDIR=%%~dpD"
echo Using: %GENEXE%

pushd "%GENEXEDIR%" >nul || goto :err
"%GENEXE%" "%MIGS%" "ResxSnapshot"
set "RC=%ERRORLEVEL%"
popd >nul
if %RC% neq 0 goto :err

rem --- 6/7: подменяем .resx на свежий и удаляем временные файлы -----------
echo === [6/7] Apply fresh snapshot to %TARGETMIG%.resx
set "TEMPRESX="
for /f "delims=" %%F in ('dir /B /OD "%MIGS%\*_ResxSnapshot.resx" 2^>nul') do set "TEMPRESX=%%F"
if not defined TEMPRESX (
    echo [X] Не найден свежесгенерированный *_ResxSnapshot.resx в %MIGS%
    goto :err
)
set "TEMPBASE=!TEMPRESX:.resx=!"
copy /Y "%MIGS%\!TEMPRESX!" "%MIGS%\%TARGETMIG%.resx" >nul || goto :err

del "%MIGS%\!TEMPBASE!.cs"          >nul 2>&1
del "%MIGS%\!TEMPBASE!.Designer.cs" >nul 2>&1
del "%MIGS%\!TEMPRESX!"             >nul 2>&1

rem --- 7/7: пересборка Core, чтобы новый .resx попал в EmbeddedResource ----
rem  EF6 хранит снапшот ДВАЖДЫ: как файл .resx на диске и как embedded
rem  resource внутри AhuErp.Core.dll. Add-Migration / migrate.exe читают
rem  embedded версию, а не файл на диске. После подмены .resx нужно
rem  заново слинковать AhuErp.Core, иначе VS PMC будет видеть старый
rem  снапшот и Add-Migration TestEmpty покажет фантомные изменения.
echo === [7/7] Rebuild AhuErp.Core to embed the fresh .resx
del /Q "%CORE%\bin\Debug\AhuErp.Core.dll" >nul 2>&1
del /Q "%CORE%\bin\Debug\net48\AhuErp.Core.dll" >nul 2>&1
dotnet build "%CORE%\AhuErp.Core.csproj" -c Debug -v minimal --nologo --no-restore || goto :err

call :restore_appconfig

echo.
echo === Done ===
echo  Updated: %MIGS%\%TARGETMIG%.resx
echo  Backup : %BACKUPDIR%
echo.
echo Next steps:
echo   0. (Один раз!) Дропни runtime LocalDB, чтобы EF6 не споткнулся на
echo      старых 14-значных ID в __MigrationHistory:
echo        sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "IF DB_ID('AhuErpDb') IS NOT NULL BEGIN ALTER DATABASE [AhuErpDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [AhuErpDb]; END"
echo      (dev-БД, данных не жалко - EF6 пересоздаст при первом запуске UI).
echo   1. Открой решение в Visual Studio.
echo   2. ВАЖНО: Build - Rebuild Solution (Ctrl+Shift+B - Rebuild). Это
echo      пересоберёт AhuErp.UI и подцепит свежий AhuErp.Core.dll с
echo      обновлённым embedded .resx.
echo   3. Tools - NuGet Package Manager - Package Manager Console:
echo        Add-Migration TestEmpty -ProjectName AhuErp.Core -StartUpProjectName AhuErp.UI
echo      Если Up()/Down() пусты - значит модель и слепок синхронны.
echo      Удали TestEmpty (Remove-Migration) и закоммить только обновлённый .resx.
echo   4. Если Up() не пустой - значит модель ушла дальше .resx, дополни код
echo      и повтори этот скрипт.
echo.
endlocal
exit /b 0

:err
call :restore_appconfig
echo.
echo [X] regen-migrations.bat failed.
endlocal
exit /b 1

:restore_appconfig
if exist "%APPCFG%.bak" (
    move /Y "%APPCFG%.bak" "%APPCFG%" >nul
)
exit /b 0
