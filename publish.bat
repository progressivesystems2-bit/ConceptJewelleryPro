@echo off
title ConceptJewelleryPro Unified Web Portal & API
cd /d "%~dp0"

:: Signal and terminate any running instance, watchdog loop, and scheduled task
schtasks /End /TN "ConceptJewellery_WebServer" 2>nul
taskkill /f /fi "WINDOWTITLE eq ConceptJewelleryPro*" 2>nul
taskkill /f /t /im ConceptJewelleryPro.API.exe 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_Process | Where-Object { ($_.Name -match '^(cmd|wscript|cscript|dotnet|ConceptJewelleryPro\.API)\.exe$') -and ($_.CommandLine -match 'ConceptJewellery_Watchdog|ConceptJewellery_SilentWatchdog|ConceptJewellery_WebServer|ConceptJewelleryPro\.API') -and ($_.ProcessId -ne $PID) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetTCPConnection -LocalPort 5080 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | Where-Object { $_ -ne $PID } | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }" 2>nul
timeout /t 1 /nobreak >nul

echo ======================================================
echo Starting ConceptJewelleryPro Server (.NET 8)...
echo.
echo Staff Web Portal:  http://localhost:5080/
echo Swagger API Docs:  http://localhost:5080/swagger
echo Diagnostics URL:   http://localhost:5080/api/diagnostics/status
echo ======================================================
echo.

cd /d "%~dp0API"
dotnet publish ConceptJewelleryPro.API.csproj -c Release -r win-x64 --self-contained true -o ./publish-jewel

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Server encountered an error and stopped.
    pause
)
