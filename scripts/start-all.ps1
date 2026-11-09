<#
.SYNOPSIS
    Energy Twin 2026 - Windows Startup Script (PowerShell)
.DESCRIPTION
    Interaktive Auswahl und geordneter Start der Simulationskomponenten.
    Unterstützt sowohl Windows Terminal (wt.exe) mit Tabs als auch separate PowerShell-Fenster.
#>

[CmdletBinding()]
param (
    [switch]$ForceSeparateWindows,
    [switch]$All
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path "$scriptDir\..").Path
Set-Location -LiteralPath $repoRoot

# 1. Prüfen, ob dotnet verfügbar ist
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "❌ .NET SDK ('dotnet') wurde im PATH nicht gefunden! Bitte installieren Sie das .NET 10 SDK."
    exit 1
}

$apps = @(
    [PSCustomObject]@{ Name = "Simulations-Master";   Path = "Simulations-Master\Simulations-Master.csproj";          Delay = 3; Color = "Green";     Desc = "TCP 1510 - Zentraler Taktgeber & Synchronisation"; Selected = $true },
    [PSCustomObject]@{ Name = "Weather-Simulator";    Path = "WeatherSimulator\WeatherSimulator.csproj";            Delay = 2; Color = "Cyan";      Desc = "HTTP 5005 - Realdaten-Wetterdienst (CSV Salzburg)"; Selected = $true },
    [PSCustomObject]@{ Name = "Dual-Port-Charger";    Path = "Dual-Port-Charger\Dual-Port-Charger.csproj";          Delay = 1; Color = "Magenta";   Desc = "Modbus 1502 - 2-Port DC-Schnellladestation"; Selected = $true },
    [PSCustomObject]@{ Name = "Battery";              Path = "Battery\Battery.csproj";                              Delay = 1; Color = "Blue";      Desc = "Modbus 1503 - Großbatteriespeicher-Simulator"; Selected = $true },
    [PSCustomObject]@{ Name = "PV-Simulator";         Path = "PV_Simulator\PV_Simulator\PV_Simulator.csproj";      Delay = 1; Color = "Yellow";    Desc = "Modbus 1504 - Photovoltaik-Simulator"; Selected = $true },
    [PSCustomObject]@{ Name = "WindTurbine";          Path = "WindTurbine_Simulator\WindTurbine_Simulator.csproj"; Delay = 1; Color = "DarkCyan";  Desc = "Modbus 1505 - Windkraftanlage (Vestas V90)"; Selected = $true },
    [PSCustomObject]@{ Name = "Modbus-Gateway";       Path = "ModbusRabbitMqGateway\ModbusRabbitMqGateway.csproj";  Delay = 1; Color = "DarkYellow";Desc = "Bridge Modbus Polling <-> RabbitMQ Telemetrie"; Selected = $true },
    [PSCustomObject]@{ Name = "RabbitMQ-Viewer";      Path = "RabbitMqViewer\RabbitMqViewer.csproj";                Delay = 1; Color = "DarkGreen"; Desc = "Spectre.Console Live-Telemetrie-Dashboard"; Selected = $true },
    [PSCustomObject]@{ Name = "Modbus-Diagnose";      Path = "ModbusDiagnoseClient\ModbusDiagnoseClient.csproj";    Delay = 0; Color = "White";     Desc = "Interaktives Modbus Lese-/Schreibwerkzeug"; Selected = $false }
)

# Interaktives Menü (falls interaktive Konsole und nicht -All)
$isInteractive = (-not $All) -and (-not [Console]::IsInputRedirected)
if ($isInteractive) {
    try {
        [Console]::CursorVisible = $false
        $cursor = 0
        $statusMsg = ""
        $firstDraw = $true
        $totalLines = 6 + $apps.Count

        while ($true) {
            if (-not $firstDraw) {
                try {
                    $pos = $host.UI.RawUI.CursorPosition
                    $pos.Y = [Math]::Max(0, $pos.Y - $totalLines)
                    $pos.X = 0
                    $host.UI.RawUI.CursorPosition = $pos
                } catch {
                    [Console]::SetCursorPosition(0, [Math]::Max(0, [Console]::CursorTop - $totalLines))
                }
            }
            $firstDraw = $false

            $selectedCount = ($apps | Where-Object { $_.Selected }).Count

            Write-Host ("=" * 78) -ForegroundColor Cyan
            Write-Host "⚡ Energy Twin 2026 - Modulauswahl" -ForegroundColor Cyan
            Write-Host "↑/↓: Navigieren | Leertaste: Wählen | A: Alle | 1-9: Direktwahl | Enter/S: Starten" -ForegroundColor DarkGray
            Write-Host ("=" * 78) -ForegroundColor Cyan

            for ($i = 0; $i -lt $apps.Count; $i++) {
                $app = $apps[$i]
                $isCurrent = ($i -eq $cursor)
                $prefix = if ($isCurrent) { " ➜ " } else { "   " }
                $check = if ($app.Selected) { "[X]" } else { "[ ]" }
                $checkColor = if ($app.Selected) { "Green" } else { "DarkGray" }
                $numStr = "$($i + 1). "
                $nameFormatted = ($numStr + $app.Name).PadRight(26)
                $nameColor = if ($isCurrent) { "Cyan" } else { "White" }

                Write-Host $prefix -NoNewline -ForegroundColor Cyan
                Write-Host $check -NoNewline -ForegroundColor $checkColor
                Write-Host (" " + $nameFormatted) -NoNewline -ForegroundColor $nameColor
                Write-Host (" (" + $app.Desc + ")") -ForegroundColor DarkGray
            }

            Write-Host ("-" * 78) -ForegroundColor DarkGray
            if ($statusMsg) {
                Write-Host $statusMsg -ForegroundColor Red
            } else {
                Write-Host "Ausgewählt: $selectedCount von $($apps.Count) Projekten | 'Q': Abbrechen" -ForegroundColor White
            }

            $key = [Console]::ReadKey($true)
            $statusMsg = ""

            switch ($key.Key) {
                ([ConsoleKey]::UpArrow) {
                    $cursor = if ($cursor -gt 0) { $cursor - 1 } else { $apps.Count - 1 }
                }
                ([ConsoleKey]::DownArrow) {
                    $cursor = if ($cursor -lt ($apps.Count - 1)) { $cursor + 1 } else { 0 }
                }
                ([ConsoleKey]::K) {
                    $cursor = if ($cursor -gt 0) { $cursor - 1 } else { $apps.Count - 1 }
                }
                ([ConsoleKey]::J) {
                    $cursor = if ($cursor -lt ($apps.Count - 1)) { $cursor + 1 } else { 0 }
                }
                ([ConsoleKey]::Spacebar) {
                    $apps[$cursor].Selected = -not $apps[$cursor].Selected
                }
                ([ConsoleKey]::A) {
                    $hasUnselected = ($apps | Where-Object { -not $_.Selected }).Count -gt 0
                    foreach ($app in $apps) { $app.Selected = $hasUnselected }
                }
                ([ConsoleKey]::Enter) {
                    if ($selectedCount -gt 0) { break }
                    $statusMsg = "⚠️ Keine Projekte ausgewählt! Bitte mindestens ein Projekt wählen."
                }
                ([ConsoleKey]::S) {
                    if ($selectedCount -gt 0) { break }
                    $statusMsg = "⚠️ Keine Projekte ausgewählt! Bitte mindestens ein Projekt wählen."
                }
                ([ConsoleKey]::Q) {
                    Write-Host "`n❌ Startvorgang durch Benutzer abgebrochen." -ForegroundColor Yellow
                    exit 0
                }
                ([ConsoleKey]::Escape) {
                    Write-Host "`n❌ Startvorgang durch Benutzer abgebrochen." -ForegroundColor Yellow
                    exit 0
                }
                default {
                    $char = $key.KeyChar
                    if ($char -ge '1' -and $char -le '9') {
                        $idx = [int][string]$char - 1
                        if ($idx -lt $apps.Count) {
                            $apps[$idx].Selected = -not $apps[$idx].Selected
                        }
                    }
                }
            }
        }
    } finally {
        [Console]::CursorVisible = $true
    }
}

$appsToStart = @($apps | Where-Object { $_.Selected })
if ($appsToStart.Count -eq 0) {
    Write-Host "⚠️ Keine Projekte zum Starten ausgewählt. Beende." -ForegroundColor Yellow
    exit 0
}

Write-Host "`n==============================================================================" -ForegroundColor Cyan
Write-Host "⚡ Starte $($appsToStart.Count) ausgewählte Komponenten..." -ForegroundColor Cyan
Write-Host "📂 Arbeitsverzeichnis: $repoRoot" -ForegroundColor DarkGray
Write-Host "==============================================================================" -ForegroundColor Cyan

# 2. RabbitMQ über Docker starten (falls Docker verfügbar)
if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    Write-Host "🐳 Stelle sicher, dass RabbitMQ via Docker Compose läuft..." -ForegroundColor Yellow
    docker-compose up -d
} elseif (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "🐳 Stelle sicher, dass RabbitMQ via Docker Compose läuft..." -ForegroundColor Yellow
    docker compose up -d
} else {
    Write-Host "⚠️ Hinweis: Docker CLI nicht im PATH gefunden. Bitte sicherstellen, dass RabbitMQ auf Port 5672 läuft." -ForegroundColor Yellow
}

$hasWt = (Get-Command wt.exe -ErrorAction SilentlyContinue) -and (-not $ForceSeparateWindows)

Write-Host "`nStarte Simulationsmodule in geordneter Reihenfolge..." -ForegroundColor White

for ($i = 0; $i -lt $appsToStart.Count; $i++) {
    $app = $appsToStart[$i]
    $stepNum = $i + 1
    $totalApps = $appsToStart.Count
    Write-Host "[$stepNum/$totalApps] Starte $($app.Name)..." -ForegroundColor $app.Color

    $projectFullPath = Join-Path $repoRoot $app.Path
    $title = "EnergyTwin: $($app.Name)"
    $command = "Set-Location -LiteralPath '$repoRoot'; `$host.UI.RawUI.WindowTitle = '$title'; dotnet run --project '$projectFullPath'"

    $startedInWt = $false
    if ($hasWt) {
        try {
            if ($i -eq 0) {
                Start-Process wt.exe -ArgumentList @("-d", $repoRoot, "--title", $title, "powershell.exe", "-NoExit", "-Command", $command)
            } else {
                Start-Process wt.exe -ArgumentList @("-w", "0", "nt", "-d", $repoRoot, "--title", $title, "powershell.exe", "-NoExit", "-Command", $command)
            }
            $startedInWt = $true
        } catch {
            Write-Warning "Windows Terminal konnte nicht gestartet werden, wechsle auf separates Fenster..."
            $hasWt = $false
        }
    }

    if (-not $startedInWt) {
        Start-Process powershell.exe -ArgumentList @("-NoExit", "-Command", $command)
    }

    if ($stepNum -lt $totalApps -and $app.Delay -gt 0) {
        Start-Sleep -Seconds $app.Delay
    }
}

Write-Host "`n==============================================================================" -ForegroundColor Green
Write-Host "✅ $($appsToStart.Count) Komponenten wurden erfolgreich gestartet!" -ForegroundColor Green
$hasMaster = ($appsToStart | Where-Object { $_.Name -eq "Simulations-Master" }).Count -gt 0
if ($hasMaster) {
    Write-Host "👉 Wechseln Sie in den Tab/das Fenster 'Simulations-Master' und drücken Sie 'S'," -ForegroundColor Yellow
    Write-Host "   um den virtuellen Zeittakt zu starten!" -ForegroundColor Yellow
}
Write-Host "==============================================================================" -ForegroundColor Green
