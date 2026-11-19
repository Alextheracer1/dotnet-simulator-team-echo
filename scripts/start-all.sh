#!/usr/bin/env bash
# ==============================================================================
# Energy Twin 2026 - All-in-One Startup Script (macOS / Linux)
# Interaktive Auswahl und geordneter Start der Simulationskomponenten
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# ANSI-Farben & Stile
CLR_RESET=$(printf "\033[0m")
CLR_BOLD=$(printf "\033[1m")
CLR_DIM=$(printf "\033[2m")
CLR_CYAN=$(printf "\033[1;36m")
CLR_GREEN=$(printf "\033[1;32m")
CLR_YELLOW=$(printf "\033[1;33m")
CLR_RED=$(printf "\033[1;31m")
CLR_GRAY=$(printf "\033[90m")
ESC=$(printf "\033")

# Projektdefinitionen
PROJECT_NAMES=(
    "Simulations-Master"
    "Weather-Simulator"
    "Dual-Port-Charger"
    "Battery"
    "PV-Simulator"
    "WindTurbine-Simulator"
    "Modbus-RabbitMQ-Gateway"
    "RabbitMQ-Viewer"
    "Modbus-Diagnose-Client"
)

PROJECT_DESCS=(
    "TCP 1510 - Zentraler Taktgeber & Synchronisation"
    "HTTP 5005 - Realdaten-Wetterdienst (CSV Salzburg)"
    "Modbus 1502 - 2-Port DC-Schnellladestation"
    "Modbus 1503 - Großbatteriespeicher-Simulator"
    "Modbus 1504 - Photovoltaik-Simulator"
    "Modbus 1505 - Windkraftanlage (Vestas V90)"
    "Bridge Modbus Polling <-> RabbitMQ Telemetrie"
    "Spectre.Console Live-Telemetrie-Dashboard"
    "Interaktives Modbus Lese-/Schreibwerkzeug"
)

PROJECT_PATHS=(
    "Simulations-Master/Simulations-Master.csproj"
    "WeatherSimulator/WeatherSimulator.csproj"
    "Dual-Port-Charger/Dual-Port-Charger.csproj"
    "Battery/Battery.csproj"
    "PV_Simulator/PV_Simulator/PV_Simulator.csproj"
    "WindTurbine_Simulator/WindTurbine_Simulator.csproj"
    "ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj"
    "RabbitMqViewer/RabbitMqViewer.csproj"
    "ModbusDiagnoseClient/ModbusDiagnoseClient.csproj"
)

# Startverzögerungen in Sekunden, damit Server-Ports vor Client-Verbindungen bereitstehen
PROJECT_DELAYS=(3 2 1 1 1 1 1 0 0)

# Standardauswahl: Module 1-8 aktiv (1), Diagnoseclient optional inaktiv (0)
PROJECT_SELECTED=(1 1 1 1 1 1 1 1 0)

# 1. Argumente verarbeiten
START_ALL_IMMEDIATELY=false
for arg in "$@"; do
    case "$arg" in
        --all|-y)
            START_ALL_IMMEDIATELY=true
            ;;
        --help|-h)
            echo "Verwendung: $0 [OPTIONEN]"
            echo ""
            echo "Optionen:"
            echo "  --all, -y    Startet sofort alle 8 Standardkomponenten ohne Menü"
            echo "  --help, -h   Zeigt diesen Hilfetext an"
            echo ""
            echo "Tastenkombinationen im Menü:"
            echo "  ↑ / ↓        Auswahlzeiger nach oben / unten bewegen (auch k/j)"
            echo "  Leertaste    Projekt an- oder abwählen"
            echo "  1-9          Projekt direkt an-/abwählen"
            echo "  a / A        Alle Projekte an- oder abwählen"
            echo "  Enter / S    Ausgewählte Projekte starten"
            echo "  q / Q        Startvorgang abbrechen"
            exit 0
            ;;
    esac
done

# 2. Prüfen auf .NET
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_BIN="dotnet"
elif [ -x "/usr/local/share/dotnet/dotnet" ]; then
    DOTNET_BIN="/usr/local/share/dotnet/dotnet"
else
    echo "❌ Fehler: .NET SDK ('dotnet') wurde nicht gefunden."
    echo "Bitte stellen Sie sicher, dass .NET 10 installiert und im PATH hinterlegt ist."
    exit 1
fi

# Cursor-Wiederherstellung bei Script-Beendigung
cleanup() {
    printf "\033[?25h"
}
trap cleanup EXIT INT TERM

# Interaktive Auswahl ausführen (falls Terminal vorhanden und nicht --all)
if [ "$START_ALL_IMMEDIATELY" = false ] && [ -t 0 ]; then
    CURSOR_POS=0
    FIRST_DRAW=true
    STATUS_MSG=""
    TOTAL_LINES=$(( 6 + ${#PROJECT_NAMES[@]} ))

    draw_menu() {
        if [ "$FIRST_DRAW" = false ]; then
            printf "\033[%dA" "$TOTAL_LINES"
        fi
        FIRST_DRAW=false

        local sel_count=0
        for s in "${PROJECT_SELECTED[@]}"; do
            if [ "$s" -eq 1 ]; then
                sel_count=$((sel_count + 1))
            fi
        done

        printf "\033[2K\r%b\n" "${CLR_CYAN}==============================================================================${CLR_RESET}"
        printf "\033[2K\r%b\n" "${CLR_CYAN}⚡ Energy Twin 2026 - Modulauswahl${CLR_RESET}"
        printf "\033[2K\r%b\n" "${CLR_GRAY}↑/↓: Navigieren | Leertaste: Wählen | 'a': Alle | 1-9: Direktwahl | Enter/'s': Starten${CLR_RESET}"
        printf "\033[2K\r%b\n" "${CLR_CYAN}==============================================================================${CLR_RESET}"

        for i in "${!PROJECT_NAMES[@]}"; do
            local prefix="   "
            local num="$((i + 1))"
            local check="${CLR_GRAY}[ ]${CLR_RESET}"
            if [ "${PROJECT_SELECTED[$i]}" -eq 1 ]; then
                check="${CLR_GREEN}[X]${CLR_RESET}"
            fi

            local name="${PROJECT_NAMES[$i]}"
            local desc="${PROJECT_DESCS[$i]}"

            if [ "$i" -eq "$CURSOR_POS" ]; then
                prefix="${CLR_CYAN} ➜ ${CLR_RESET}"
                printf "\033[2K\r%b%b ${CLR_BOLD}${CLR_CYAN}%d. %-24s${CLR_RESET} ${CLR_GRAY}(%s)${CLR_RESET}\n" "$prefix" "$check" "$num" "$name" "$desc"
            else
                printf "\033[2K\r%b%b %d. %-24s ${CLR_GRAY}(%s)${CLR_RESET}\n" "$prefix" "$check" "$num" "$name" "$desc"
            fi
        done

        printf "\033[2K\r%b\n" "${CLR_GRAY}------------------------------------------------------------------------------${CLR_RESET}"
        if [ -n "$STATUS_MSG" ]; then
            printf "\033[2K\r%b\n" "$STATUS_MSG"
        else
            printf "\033[2K\r%b\n" "Ausgewählt: ${CLR_BOLD}${sel_count}${CLR_RESET} von ${#PROJECT_NAMES[@]} Projekten | '${CLR_YELLOW}q${CLR_RESET}': Abbrechen"
        fi
    }

    # Cursor für das Menü verstecken
    printf "\033[?25l"
    set +e

    while true; do
        draw_menu
        STATUS_MSG=""

        key=""
        rest=""
        IFS= read -r -s -n 1 key || true

        if [[ "$key" == "$ESC" ]]; then
            IFS= read -r -s -n 2 -t 1 rest || true
            case "$rest" in
                "[A"|"OA") # Pfeil nach oben
                    if [ "$CURSOR_POS" -gt 0 ]; then
                        CURSOR_POS=$((CURSOR_POS - 1))
                    else
                        CURSOR_POS=$((${#PROJECT_NAMES[@]} - 1))
                    fi
                    ;;
                "[B"|"OB") # Pfeil nach unten
                    if [ "$CURSOR_POS" -lt $((${#PROJECT_NAMES[@]} - 1)) ]; then
                        CURSOR_POS=$((CURSOR_POS + 1))
                    else
                        CURSOR_POS=0
                    fi
                    ;;
            esac
        elif [[ "$key" == "k" || "$key" == "K" ]]; then # Vim Hoch
            if [ "$CURSOR_POS" -gt 0 ]; then
                CURSOR_POS=$((CURSOR_POS - 1))
            else
                CURSOR_POS=$((${#PROJECT_NAMES[@]} - 1))
            fi
        elif [[ "$key" == "j" || "$key" == "J" ]]; then # Vim Runter
            if [ "$CURSOR_POS" -lt $((${#PROJECT_NAMES[@]} - 1)) ]; then
                CURSOR_POS=$((CURSOR_POS + 1))
            else
                CURSOR_POS=0
            fi
        elif [[ "$key" == " " ]]; then # Leertaste: Umschalten
            if [ "${PROJECT_SELECTED[$CURSOR_POS]}" -eq 1 ]; then
                PROJECT_SELECTED[$CURSOR_POS]=0
            else
                PROJECT_SELECTED[$CURSOR_POS]=1
            fi
        elif [[ "$key" == "a" || "$key" == "A" ]]; then # 'a': Alle an/abwählen
            any_unselected=0
            for s in "${PROJECT_SELECTED[@]}"; do
                if [ "$s" -eq 0 ]; then
                    any_unselected=1
                    break
                fi
            done
            for i in "${!PROJECT_SELECTED[@]}"; do
                PROJECT_SELECTED[$i]=$any_unselected
            done
        elif [[ "$key" =~ ^[1-9]$ ]]; then # Zifferntasten 1-9
            idx=$((key - 1))
            if [ "$idx" -lt "${#PROJECT_NAMES[@]}" ]; then
                if [ "${PROJECT_SELECTED[$idx]}" -eq 1 ]; then
                    PROJECT_SELECTED[$idx]=0
                else
                    PROJECT_SELECTED[$idx]=1
                fi
            fi
        elif [[ "$key" == "s" || "$key" == "S" || "$key" == "" ]]; then # Enter oder 'S': Starten
            sel_count=0
            for s in "${PROJECT_SELECTED[@]}"; do
                if [ "$s" -eq 1 ]; then
                    sel_count=$((sel_count + 1))
                fi
            done

            if [ "$sel_count" -eq 0 ]; then
                STATUS_MSG="${CLR_RED}⚠️ Keine Projekte ausgewählt! Mindestens ein Projekt wählen.${CLR_RESET}"
            else
                break
            fi
        elif [[ "$key" == "q" || "$key" == "Q" ]]; then
            printf "\033[?25h\n"
            printf "${CLR_YELLOW}❌ Startvorgang durch Benutzer abgebrochen.${CLR_RESET}\n"
            exit 0
        fi
    done

    # Cursor wieder anzeigen und Menü abschließen
    printf "\033[?25h\n"
    set -e
fi

# Zählen wie viele Projekte gestartet werden sollen
TOTAL_SELECTED=0
for s in "${PROJECT_SELECTED[@]}"; do
    if [ "$s" -eq 1 ]; then
        TOTAL_SELECTED=$((TOTAL_SELECTED + 1))
    fi
done

if [ "$TOTAL_SELECTED" -eq 0 ]; then
    echo "⚠️ Keine Projekte ausgewählt. Beende."
    exit 0
fi

# 3. RabbitMQ über Docker prüfen
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
echo "Starte $TOTAL_SELECTED ausgewählte Simulationsmodule in geordneter Reihenfolge..."
echo ""

STEP=0
for i in "${!PROJECT_NAMES[@]}"; do
    if [ "${PROJECT_SELECTED[$i]}" -eq 1 ]; then
        STEP=$((STEP + 1))
        NAME="${PROJECT_NAMES[$i]}"
        PATH_PROJ="${PROJECT_PATHS[$i]}"
        DELAY="${PROJECT_DELAYS[$i]}"

        echo "[$STEP/$TOTAL_SELECTED] Starte $NAME..."
        run_in_terminal "$NAME" "DOTNET_CLI_HOME=/tmp/dotnet_home $DOTNET_BIN run --project $PATH_PROJ"

        # Nur pausieren, wenn noch weitere Komponenten folgen
        if [ "$STEP" -lt "$TOTAL_SELECTED" ] && [ "$DELAY" -gt 0 ]; then
            sleep "$DELAY"
        fi
    fi
done

echo ""
echo "=============================================================================="
echo "✅ $TOTAL_SELECTED von ${#PROJECT_NAMES[@]} Komponenten wurden erfolgreich gestartet!"
if [ "${PROJECT_SELECTED[0]}" -eq 1 ]; then
    echo "👉 Wechseln Sie in den Tab 'Simulations-Master' und drücken Sie 'S',"
    echo "   um den virtuellen Zeittakt zu starten!"
fi
echo "=============================================================================="
