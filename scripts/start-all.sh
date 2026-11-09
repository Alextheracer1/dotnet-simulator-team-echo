#!/usr/bin/env bash
# ==============================================================================
# Energy Twin 2026 - All-in-One Startup Script
# Startet alle Simulationskomponenten in getrennten Terminal-Tabs (macOS Terminal)
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "⚡ Starte Energy Twin 2026 Ökosystem..."
echo "📂 Arbeitsverzeichnis: $REPO_ROOT"

# Prüfen auf .NET
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_BIN="dotnet"
elif [ -x "/usr/local/share/dotnet/dotnet" ]; then
    DOTNET_BIN="/usr/local/share/dotnet/dotnet"
else
    echo "❌ Fehler: .NET SDK nicht gefunden."
    exit 1
fi

# 1. RabbitMQ prüfen
if command -v docker >/dev/null 2>&1; then
    echo "🐳 Stelle sicher, dass RabbitMQ via Docker läuft..."
    (cd "$REPO_ROOT" && docker compose up -d)
else
    echo "⚠️ Hinweis: Docker CLI nicht direkt im Pfad verfügbar. Bitte sicherstellen, dass RabbitMQ auf Port 5672 läuft."
fi

# macOS Terminal Tab Automation
run_in_tab() {
    local title="$1"
    local cmd="$2"

    if [[ "$OSTYPE" == "darwin"* ]]; then
        osascript <<EOF
tell application "Terminal"
    activate
    tell application "System Events" to tell process "Terminal" to keystroke "t" using command down
    delay 0.5
    do script "cd '$REPO_ROOT' && echo -n -e \"\033]0;$title\007\" && $cmd" in selected tab of the front window
end tell
EOF
    else
        echo "Starte $title im Hintergrund..."
        (cd "$REPO_ROOT" && eval "$cmd") &
    fi
}

echo "1/7 Starte Simulations-Master..."
run_in_tab "Sim-Master" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Simulations-Master/Simulations-Master.csproj"
sleep 2

echo "2/7 Starte Weather-Simulator..."
run_in_tab "Weather-Simulator" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project WeatherSimulator/WeatherSimulator.csproj"
sleep 2

echo "3/7 Starte Dual-Port Charger..."
run_in_tab "Charger" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Dual-Port-Charger/Dual-Port-Charger.csproj"
sleep 1

echo "4/7 Starte Battery..."
run_in_tab "Battery" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Battery/Battery.csproj"
sleep 1

echo "5/7 Starte PV-Simulator..."
run_in_tab "PV-Simulator" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj"
sleep 1

echo "6/7 Starte WindTurbine..."
run_in_tab "WindTurbine" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project WindTurbine_Simulator/WindTurbine_Simulator.csproj"
sleep 1

echo "7/7 Starte Modbus-RabbitMQ Gateway..."
run_in_tab "Modbus-Gateway" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj"
sleep 1

echo "✨ Optional: Starte RabbitMQ Viewer..."
run_in_tab "RabbitMQ-Viewer" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project RabbitMqViewer/RabbitMqViewer.csproj"

echo ""
echo "✅ Alle Komponenten wurden gestartet!"
echo "👉 Wechseln Sie in den Tab 'Sim-Master' und drücken Sie 'S', um den Zeittakt zu starten."
