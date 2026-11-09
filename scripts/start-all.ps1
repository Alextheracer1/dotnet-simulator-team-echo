<#
.SYNOPSIS
    Energy Twin 2026 - Windows Startup Script (PowerShell)
.DESCRIPTION
    Interaktive Auswahl und geordneter Start der Simulationskomponenten.
    Standardmäßig startet jede Komponente in einem eigenen separaten Konsolenfenster.
#>

[CmdletBinding()]
param (
    [switch]$ForceSeparateWindows,
    [switch]$UseTabs,
    [switch]$All
)

$ErrorActionPreference = "Stop"

# UTF-8 Konsolen-Ausgabe sicherstellen
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path "$scriptDir\..").Path
Set-Location -LiteralPath $repoRoot

# 1. Prüfen, ob dotnet verfügbar ist
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "❌ .NET SDK ('dotnet') wurde im PATH nicht gefunden! Bitte installieren Sie das .NET 10 SDK."
    exit 1
}

$apps = @(
    [PSCustomObject]@{ Name = "Simulations-Master";   Path = "Simulations-Master\Simulations-Master.csproj";          Delay = 3; Color = "Green";     Desc = "TCP 1510 - Taktgeber & Synchronisation";     Selected = $true },
    [PSCustomObject]@{ Name = "Weather-Simulator";    Path = "WeatherSimulator\WeatherSimulator.csproj";            Delay = 2; Color = "Cyan";      Desc = "HTTP 5005 - Realdaten-Wetterdienst";         Selected = $true },
    [PSCustomObject]@{ Name = "Dual-Port-Charger";    Path = "Dual-Port-Charger\Dual-Port-Charger.csproj";          Delay = 1; Color = "Magenta";   Desc = "Modbus 1502 - 2-Port Schnellladestation";    Selected = $true },
    [PSCustomObject]@{ Name = "Battery";              Path = "Battery\Battery.csproj";                              Delay = 1; Color = "Blue";      Desc = "Modbus 1503 - Grossbatteriespeicher";        Selected = $true },
    [PSCustomObject]@{ Name = "PV-Simulator";         Path = "PV_Simulator\PV_Simulator\PV_Simulator.csproj";      Delay = 1; Color = "Yellow";    Desc = "Modbus 1504 - Photovoltaik-Simulator";       Selected = $true },
    [PSCustomObject]@{ Name = "WindTurbine";          Path = "WindTurbine_Simulator\WindTurbine_Simulator.csproj"; Delay = 1; Color = "DarkCyan";  Desc = "Modbus 1505 - Windkraft (Vestas V90)";       Selected = $true },
    [PSCustomObject]@{ Name = "Modbus-Gateway";       Path = "ModbusRabbitMqGateway\ModbusRabbitMqGateway.csproj";  Delay = 1; Color = "DarkYellow";Desc = "Bridge Modbus Polling <-> RabbitMQ";         Selected = $true },
    [PSCustomObject]@{ Name = "RabbitMQ-Viewer";      Path = "RabbitMqViewer\RabbitMqViewer.csproj";                Delay = 1; Color = "DarkGreen"; Desc = "Spectre.Console Live-Dashboard";             Selected = $true },
    [PSCustomObject]@{ Name = "Modbus-Diagnose";      Path = "ModbusDiagnoseClient\ModbusDiagnoseClient.csproj";    Delay = 0; Color = "White";     Desc = "Interaktives Modbus Test-Tool";              Selected = $false }
)

# Interaktives Menü (falls interaktive Konsole und nicht -All)
$isInteractive = (-not $All) -and (-not [Console]::IsInputRedirected)
if ($isInteractive) {
    try {
        [Console]::CursorVisible = $false
        $cursor = 0
        $statusMsg = ""
        $firstDraw = $true
        $totalLines = 7 + $apps.Count

        :menuLoop while ($true) {
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

            Write-Host ("=" * 76) -ForegroundColor Cyan
            Write-Host "⚡ Energy Twin 2026 - Modulauswahl" -ForegroundColor Cyan
            Write-Host "↑ / ↓ : Navigieren        |  LEERTASTE : Checkbox [X]/[ ] an- und abwählen" -ForegroundColor DarkGray
            Write-Host "1 - 9 : Modul umschalten  |  A : Alle an-/abwählen  |  ENTER / S : Starten" -ForegroundColor DarkGray
            Write-Host ("=" * 76) -ForegroundColor Cyan

            for ($i = 0; $i -lt $apps.Count; $i++) {
                $app = $apps[$i]
                $isCurrent = ($i -eq $cursor)
                $prefix = if ($isCurrent) { " ➜ " } else { "   " }
                $check = if ($app.Selected) { "[X]" } else { "[ ]" }
                $checkColor = if ($app.Selected) { "Green" } else { "DarkGray" }
                $numStr = "$($i + 1). "
                $nameFormatted = ($numStr + $app.Name).PadRight(22)
                $nameColor = if ($isCurrent) { "Cyan" } else { "White" }

                Write-Host $prefix -NoNewline -ForegroundColor Cyan
                Write-Host $check -NoNewline -ForegroundColor $checkColor
                Write-Host (" " + $nameFormatted) -NoNewline -ForegroundColor $nameColor
                Write-Host (" (" + $app.Desc + ")") -ForegroundColor DarkGray
            }

            Write-Host ("-" * 76) -ForegroundColor DarkGray
            if ($statusMsg) {
                Write-Host $statusMsg.PadRight(76) -ForegroundColor Red
            } else {
                Write-Host ("Ausgewählt: $selectedCount von $($apps.Count) | LEERTASTE: Umschalten | ENTER/S: Starten").PadRight(76) -ForegroundColor White
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
                    if ($selectedCount -gt 0) { break menuLoop }
                    $statusMsg = "⚠️ Keine Projekte ausgewählt! Mindestens 1 Projekt wählen."
                }
                ([ConsoleKey]::S) {
                    if ($selectedCount -gt 0) { break menuLoop }
                    $statusMsg = "⚠️ Keine Projekte ausgewählt! Mindestens 1 Projekt wählen."
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
                    } elseif ($char -eq 's' -or $char -eq 'S') {
                        if ($selectedCount -gt 0) { break menuLoop }
                        $statusMsg = "⚠️ Keine Projekte ausgewählt! Mindestens 1 Projekt wählen."
                    } elseif ($char -eq 'q' -or $char -eq 'Q') {
                        Write-Host "`n❌ Startvorgang durch Benutzer abgebrochen." -ForegroundColor Yellow
                        exit 0
                    } elseif ($char -eq 'a' -or $char -eq 'A') {
                        $hasUnselected = ($apps | Where-Object { -not $_.Selected }).Count -gt 0
                        foreach ($app in $apps) { $app.Selected = $hasUnselected }
                    }
                }
            }
        }
    } finally {
        [Console]::CursorVisible = $true
    }
    Write-Host ""
}

$appsToStart = @($apps | Where-Object { $_.Selected })
if ($appsToStart.Count -eq 0) {
    Write-Host "⚠️ Keine Projekte zum Starten ausgewählt. Beende." -ForegroundColor Yellow
    exit 0
}

Write-Host "`n==============================================================================" -ForegroundColor Cyan
Write-Host "⚡ Starte $($appsToStart.Count) ausgewählte Komponenten in separaten Fenstern..." -ForegroundColor Cyan
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

# Standard: Jeder Simulator in eigenem Terminal-Fenster
# Windows Terminal Tabs nur dann nutzen, wenn explizit -UseTabs angegeben ist
$hasWt = $UseTabs -and (Get-Command wt.exe -ErrorAction SilentlyContinue) -and (-not $ForceSeparateWindows)

Write-Host "`nStarte Simulationsmodule in geordneter Reihenfolge..." -ForegroundColor White

for ($i = 0; $i -lt $appsToStart.Count; $i++) {
    $app = $appsToStart[$i]
    $stepNum = $i + 1
    $totalApps = $appsToStart.Count
    Write-Host "[$stepNum/$totalApps] Starte $($app.Name)..." -ForegroundColor $app.Color

    $projectFullPath = Join-Path $repoRoot $app.Path
    $title = "EnergyTwin: $($app.Name)"
    $command = "Set-Location -LiteralPath '$repoRoot'; try { `$host.UI.RawUI.WindowTitle = '$title' } catch {}; dotnet run --project '$projectFullPath'"

    $startedInWt = $false
    if ($hasWt) {
        try {
            $wtArgs = if ($i -eq 0) {
                "-d `"$repoRoot`" --title `"$title`" powershell.exe -NoExit -Command `"$command`""
            } else {
                "-w 0 nt -d `"$repoRoot`" --title `"$title`" powershell.exe -NoExit -Command `"$command`""
            }
            Start-Process wt.exe -ArgumentList $wtArgs -ErrorAction Stop
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
Write-Host "✅ $($appsToStart.Count) Komponenten wurden in separaten Fenstern gestartet!" -ForegroundColor Green
$hasMaster = ($appsToStart | Where-Object { $_.Name -eq "Simulations-Master" }).Count -gt 0
if ($hasMaster) {
    Write-Host "👉 Wechseln Sie in das Fenster 'Simulations-Master' und drücken Sie 'S'," -ForegroundColor Yellow
    Write-Host "   um den virtuellen Zeittakt zu starten!" -ForegroundColor Yellow
}
Write-Host "==============================================================================" -ForegroundColor Green
