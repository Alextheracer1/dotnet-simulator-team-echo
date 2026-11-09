@echo off
setlocal enabledelayedexpansion

rem Basisverzeichnis ermitteln
set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

rem Wenn PowerShell verfuegbar ist, interaktives PowerShell-Skript mit UI ausfuehren
where powershell >nul 2>nul
if %ERRORLEVEL% equ 0 (
    powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT_DIR%start-all.ps1" %*
    exit /b %ERRORLEVEL%
)

echo ==============================================================================
echo ⚡ Energy Twin 2026 - Windows Startup Script (Batch Fallback)
echo ==============================================================================
cd /d "%REPO_ROOT%"
echo 📂 Arbeitsverzeichnis: %REPO_ROOT%
echo.

rem 1. Prüfen, ob dotnet verfügbar ist
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [FEHLER] .NET SDK wurde im PATH nicht gefunden!
    echo Bitte stellen Sie sicher, dass .NET 10 installiert und im PATH hinterlegt ist.
    pause
    exit /b 1
)

rem 2. Prüfen, ob Docker verfügbar ist und RabbitMQ starten
where docker >nul 2>nul
if errorlevel 1 (
    echo [HINWEIS] Docker CLI nicht gefunden. Bitte sicherstellen, dass RabbitMQ auf Port 5672 laeuft.
) else (
    echo [DOCKER] Stelle sicher, dass RabbitMQ via Docker laeuft...
    docker compose up -d
)

echo.
echo Starte Simulationskomponenten in getrennten Konsolenfenstern...
echo.

echo [1/8] Starte Simulations-Master...
start "EnergyTwin: Simulations-Master (Port 1510)" cmd /k "title Simulations-Master && dotnet run --project Simulations-Master\Simulations-Master.csproj"
timeout /t 3 /nobreak >nul

echo [2/8] Starte Weather-Simulator...
start "EnergyTwin: Weather-Simulator (Port 5005)" cmd /k "title Weather-Simulator && dotnet run --project WeatherSimulator\WeatherSimulator.csproj"
timeout /t 2 /nobreak >nul

echo [3/8] Starte Dual-Port-Charger...
start "EnergyTwin: Dual-Port-Charger (Modbus 1502)" cmd /k "title Dual-Port-Charger && dotnet run --project Dual-Port-Charger\Dual-Port-Charger.csproj"
timeout /t 1 /nobreak >nul

echo [4/8] Starte Battery...
start "EnergyTwin: Battery (Modbus 1503)" cmd /k "title Battery && dotnet run --project Battery\Battery.csproj"
timeout /t 1 /nobreak >nul

echo [5/8] Starte PV-Simulator...
start "EnergyTwin: PV-Simulator (Modbus 1504)" cmd /k "title PV-Simulator && dotnet run --project PV_Simulator\PV_Simulator\PV_Simulator.csproj"
timeout /t 1 /nobreak >nul

echo [6/8] Starte WindTurbine-Simulator...
start "EnergyTwin: WindTurbine-Simulator (Modbus 1505)" cmd /k "title WindTurbine-Simulator && dotnet run --project WindTurbine_Simulator\WindTurbine_Simulator.csproj"
timeout /t 1 /nobreak >nul

echo [7/8] Starte Modbus-RabbitMQ Gateway...
start "EnergyTwin: Modbus-RabbitMQ Gateway" cmd /k "title Modbus-RabbitMQ Gateway && dotnet run --project ModbusRabbitMqGateway\ModbusRabbitMqGateway.csproj"
timeout /t 1 /nobreak >nul

echo [8/8] Starte RabbitMQ Live-Viewer...
start "EnergyTwin: RabbitMQ Live-Viewer" cmd /k "title RabbitMQ Live-Viewer && dotnet run --project RabbitMqViewer\RabbitMqViewer.csproj"

echo.
echo ==============================================================================
echo ✅ Alle Komponenten wurden in eigenen Fenstern gestartet!
echo 👉 Wechseln Sie in das Fenster "Simulations-Master" und druecken Sie 'S',
echo    um den virtuellen Zeittakt zu starten!
echo ==============================================================================
pause
