#!/usr/bin/env bash
# ==============================================================================
# Energy Twin 2026 - All-in-One Startup Script (macOS / Linux)
# Startet alle Simulationskomponenten in geordneter Reihenfolge
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=============================================================================="
echo "⚡ Starte Energy Twin 2026 Ökosystem..."
echo "📂 Arbeitsverzeichnis: $REPO_ROOT"
echo "=============================================================================="

# 1. Prüfen auf .NET
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_BIN="dotnet"
elif [ -x "/usr/local/share/dotnet/dotnet" ]; then
    DOTNET_BIN="/usr/local/share/dotnet/dotnet"
else
    echo "❌ Fehler: .NET SDK ('dotnet') wurde nicht gefunden."
    echo "Bitte stellen Sie sicher, dass .NET 10 installiert und im PATH hinterlegt ist."
    exit 1
fi

# 2. RabbitMQ über Docker prüfen
if command -v docker >/dev/null 2>&1; then
    echo "🐳 Stelle sicher, dass RabbitMQ via Docker Compose läuft..."
    (cd "$REPO_ROOT" && docker compose up -d)
else
    echo "⚠️ Hinweis: Docker CLI nicht im PATH gefunden. Bitte sicherstellen, dass RabbitMQ auf Port 5672 läuft."
fi

# macOS Terminal Tab / Window Automation
run_in_terminal() {
    local title="$1"
    local cmd="$2"

    if [[ "$OSTYPE" == "darwin"* ]]; then
        local full_cmd="cd '$REPO_ROOT' && printf '\\e]0;%s\\a' '$title' && $cmd"
        osascript - "$full_cmd" "$title" <<'EOF'
on run argv
    set scriptCmd to item 1 of argv
    set tabTitle to item 2 of argv
    
    tell application "Terminal"
        activate
        set isFirst to false
        if (count of windows) = 0 then
            set isFirst to true
        end if
    end tell
    
    if isFirst then
        tell application "Terminal"
            do script scriptCmd
        end tell
    else
        try
            tell application "System Events"
                tell process "Terminal"
                    keystroke "t" using {command down}
                end tell
            end tell
            delay 0.4
            tell application "Terminal"
                do script scriptCmd in selected tab of front window
            end tell
        on error
            tell application "Terminal"
                do script scriptCmd
            end tell
        end try
    end if
end run
EOF
    else
        echo "Starte $title im Hintergrund..."
        (cd "$REPO_ROOT" && eval "$cmd") &
    fi
}

echo ""
echo "Starte Simulationsmodule in geordneter Reihenfolge..."
echo ""

echo "[1/8] Starte Simulations-Master..."
run_in_terminal "Simulations-Master" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Simulations-Master/Simulations-Master.csproj"
sleep 3

echo "[2/8] Starte Weather-Simulator..."
run_in_terminal "Weather-Simulator" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project WeatherSimulator/WeatherSimulator.csproj"
sleep 2

echo "[3/8] Starte Dual-Port-Charger..."
run_in_terminal "Dual-Port-Charger" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Dual-Port-Charger/Dual-Port-Charger.csproj"
sleep 1

echo "[4/8] Starte Battery..."
run_in_terminal "Battery" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project Battery/Battery.csproj"
sleep 1

echo "[5/8] Starte PV-Simulator..."
run_in_terminal "PV-Simulator" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj"
sleep 1

echo "[6/8] Starte WindTurbine..."
run_in_terminal "WindTurbine-Simulator" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project WindTurbine_Simulator/WindTurbine_Simulator.csproj"
sleep 1

echo "[7/8] Starte Modbus-RabbitMQ Gateway..."
run_in_terminal "Modbus-RabbitMQ-Gateway" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj"
sleep 1

echo "[8/8] Starte RabbitMQ Live-Viewer..."
run_in_terminal "RabbitMQ-Viewer" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project RabbitMqViewer/RabbitMqViewer.csproj"

echo ""
echo "=============================================================================="
echo "✅ Alle 8 Komponenten wurden erfolgreich gestartet!"
echo "👉 Wechseln Sie in den Tab 'Simulations-Master' und drücken Sie 'S',"
echo "   um den virtuellen Zeittakt zu starten!"
echo "=============================================================================="
