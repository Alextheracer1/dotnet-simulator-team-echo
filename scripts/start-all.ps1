<#
.SYNOPSIS
    Energy Twin 2026 - Windows Startup Script (PowerShell)
.DESCRIPTION
    Startet alle Komponenten der Simulation in geordneter Reihenfolge.
    Unterstützt sowohl Windows Terminal (wt.exe) mit Tabs als auch separate Konsolenfenster.
#>

[CmdletBinding()]
param (
    [switch]$ForceSeparateWindows
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path "$scriptDir\..").Path
Set-Location -Path $repoRoot

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "⚡ Energy Twin 2026 - Windows Startup Script (PowerShell)" -ForegroundColor Cyan
Write-Host "📂 Arbeitsverzeichnis: $repoRoot" -ForegroundColor DarkGray
Write-Host "==============================================================================" -ForegroundColor Cyan

# 1. Prüfen, ob dotnet verfügbar ist
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "❌ .NET SDK ('dotnet') wurde im PATH nicht gefunden! Bitte installieren Sie das .NET 10 SDK."
    exit 1
}

# 2. RabbitMQ über Docker starten
if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "🐳 Stelle sicher, dass RabbitMQ via Docker Compose läuft..." -ForegroundColor Yellow
    docker compose up -d
} else {
    Write-Host "⚠️ Hinweis: Docker CLI nicht im PATH gefunden. Bitte sicherstellen, dass RabbitMQ auf Port 5672 läuft." -ForegroundColor Yellow
}

$hasWt = (Get-Command wt.exe -ErrorAction SilentlyContinue) -and (-not $ForceSeparateWindows)

$apps = @(
    @{ Name = "Simulations-Master"; Path = "Simulations-Master/Simulations-Master.csproj"; Delay = 3; Color = "Green" },
    @{ Name = "Weather-Simulator";   Path = "WeatherSimulator/WeatherSimulator.csproj";   Delay = 2; Color = "Cyan" },
    @{ Name = "Dual-Port-Charger";   Path = "Dual-Port-Charger/Dual-Port-Charger.csproj"; Delay = 1; Color = "Magenta" },
    @{ Name = "Battery";             Path = "Battery/Battery.csproj";                     Delay = 1; Color = "Blue" },
    @{ Name = "PV-Simulator";        Path = "PV_Simulator/PV_Simulator/PV_Simulator.csproj"; Delay = 1; Color = "Yellow" },
    @{ Name = "WindTurbine";         Path = "WindTurbine_Simulator/WindTurbine_Simulator.csproj"; Delay = 1; Color = "DarkCyan" },
    @{ Name = "Modbus-Gateway";      Path = "ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj"; Delay = 1; Color = "DarkYellow" },
    @{ Name = "RabbitMQ-Viewer";     Path = "RabbitMqViewer/RabbitMqViewer.csproj";       Delay = 1; Color = "DarkGreen" }
)

Write-Host "`nStarte Simulationsmodule in geordneter Reihenfolge..." -ForegroundColor White

for ($i = 0; $i -lt $apps.Count; $i++) {
    $app = $apps[$i]
    $step = "[$($i + 1)/$($apps.Count)]"
    Write-Host "$step Starte $($app.Name)..." -ForegroundColor $app.Color

    $title = "EnergyTwin: $($app.Name)"
    $proj = $app.Path

    if ($hasWt) {
        # Windows Terminal (wt.exe): -w 0 öffnet neue Tabs im bestehenden Fenster
        Start-Process wt.exe -ArgumentList "-w", "0", "nt", "-d", $repoRoot, "--title", $title, "cmd", "/k", "dotnet run --project $proj"
    } else {
        # Fallback: Eigenständige Konsolenfenster via CMD (verhindert PowerShell ExecutionPolicy-Probleme)
        Start-Process cmd.exe -WorkingDirectory $repoRoot -ArgumentList "/k", "title $title && dotnet run --project $proj"
    }

    Start-Sleep -Seconds $app.Delay
}

Write-Host "`n==============================================================================" -ForegroundColor Green
Write-Host "✅ Alle Komponenten wurden erfolgreich gestartet!" -ForegroundColor Green
Write-Host "👉 Wechseln Sie in den Tab/das Fenster 'Simulations-Master' und drücken Sie 'S'," -ForegroundColor Yellow
Write-Host "   um den virtuellen Zeittakt zu starten!" -ForegroundColor Yellow
Write-Host "==============================================================================" -ForegroundColor Green
