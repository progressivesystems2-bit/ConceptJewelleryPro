@echo off
title ConceptJewelleryPro Unified Web Portal & API
cd /d "%~dp0"

:: Kill any previously stuck instance of ConceptJewelleryPro.API
taskkill /f /im ConceptJewelleryPro.API.exe 2>nul

echo ======================================================
echo Starting ConceptJewelleryPro Server (.NET 8)...
echo.
echo Staff Web Portal:  http://localhost:5080/
echo Swagger API Docs:  http://localhost:5080/swagger
echo Diagnostics URL:   http://localhost:5080/api/diagnostics/status
echo ======================================================
echo.

cd /d "%~dp0API"
dotnet run --project ConceptJewelleryPro.API.csproj -c Release --urls "http://0.0.0.0:5080"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Server encountered an error and stopped.
    pause
)
