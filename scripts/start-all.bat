@echo off
setlocal enabledelayedexpansion

echo ==============================================================================
echo ⚡ Energy Twin 2026 - Windows Startup Script (Batch)
echo ==============================================================================

rem Basisverzeichnis ermitteln (Repository-Root)
set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

cd /d "%REPO_ROOT%"
echo 📂 Arbeitsverzeichnis: %REPO_ROOT%

rem 1. Prüfen, ob dotnet verfügbar ist
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ❌ Fehler: .NET SDK (dotnet) wurde im PATH nicht gefunden!
    echo Bitte stellen Sie sicher, dass .NET 10 installiert und im PATH hinterlegt ist.
    pause
    exit /b 1
)

rem 2. Prüfen, ob Docker verfügbar ist und RabbitMQ starten
where docker >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo 🐳 Stelle sicher, dass RabbitMQ via Docker läuft...
    docker compose up -d
) else (
    echo ⚠️ Hinweis: Docker CLI nicht im PATH gefunden. Bitte sicherstellen, dass RabbitMQ auf Port 5672 laeuft.
)

echo.
echo Starte Simulationskomponenten in getrennten Konsolenfenstern...
echo.

rem 3. Simulations-Master (Taktgeber auf TCP 1510)
echo [1/8] Starte Simulations-Master...
start "EnergyTwin: Simulations-Master (Port 1510)" cmd /k "title Simulations-Master && cd /d "%REPO_ROOT%" && dotnet run --project Simulations-Master/Simulations-Master.csproj"
timeout /t 3 /nobreak >nul

rem 4. WeatherSimulator (Wetter-Minimal-API & Push)
echo [2/8] Starte Weather-Simulator...
start "EnergyTwin: Weather-Simulator (Port 5005)" cmd /k "title Weather-Simulator && cd /d "%REPO_ROOT%" && dotnet run --project WeatherSimulator/WeatherSimulator.csproj"
timeout /t 2 /nobreak >nul

rem 5. Dual-Port-Charger (Modbus 1502)
echo [3/8] Starte Dual-Port-Charger...
start "EnergyTwin: Dual-Port-Charger (Modbus 1502)" cmd /k "title Dual-Port-Charger && cd /d "%REPO_ROOT%" && dotnet run --project Dual-Port-Charger/Dual-Port-Charger.csproj"
timeout /t 1 /nobreak >nul

rem 6. Battery (Modbus 1503)
echo [4/8] Starte Battery...
start "EnergyTwin: Battery (Modbus 1503)" cmd /k "title Battery && cd /d "%REPO_ROOT%" && dotnet run --project Battery/Battery.csproj"
timeout /t 1 /nobreak >nul

rem 7. PV_Simulator (Modbus 1504)
echo [5/8] Starte PV-Simulator...
start "EnergyTwin: PV-Simulator (Modbus 1504)" cmd /k "title PV-Simulator && cd /d "%REPO_ROOT%" && dotnet run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj"
timeout /t 1 /nobreak >nul

rem 8. WindTurbine_Simulator (Modbus 1505)
echo [6/8] Starte WindTurbine-Simulator...
start "EnergyTwin: WindTurbine-Simulator (Modbus 1505)" cmd /k "title WindTurbine-Simulator && cd /d "%REPO_ROOT%" && dotnet run --project WindTurbine_Simulator/WindTurbine_Simulator.csproj"
timeout /t 1 /nobreak >nul

rem 9. ModbusRabbitMqGateway
echo [7/8] Starte Modbus-RabbitMQ Gateway...
start "EnergyTwin: Modbus-RabbitMQ Gateway" cmd /k "title Modbus-RabbitMQ Gateway && cd /d "%REPO_ROOT%" && dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj"
timeout /t 1 /nobreak >nul

rem 10. RabbitMqViewer
echo [8/8] Starte RabbitMQ Live-Viewer...
start "EnergyTwin: RabbitMQ Live-Viewer" cmd /k "title RabbitMQ Live-Viewer && cd /d "%REPO_ROOT%" && dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj"

echo.
echo ==============================================================================
echo ✅ Alle Komponenten wurden in eigenen Fenstern gestartet!
echo 👉 Wechseln Sie in das Fenster "Simulations-Master" und druecken Sie 'S',
echo    um den virtuellen Zeittakt zu starten!
echo ==============================================================================
