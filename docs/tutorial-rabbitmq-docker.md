# 🐳 Tutorial: RabbitMQ mit Docker und .env-Datei einrichten

Dieses Tutorial beschreibt Schritt für Schritt, wie der Message Broker **RabbitMQ** (inklusive Management Web-UI) für das Projekt **Energy Twin 2026** über Docker und Docker Compose mit einer `.env`-Konfigurationsdatei aufgesetzt und betrieben wird.

---

## 1. Übersicht & Architektur

Im Simulationssystem **Energy Twin 2026** dient RabbitMQ als asynchroner Nachrichten-Broker für Telemetrie- und Steuerdaten:
* **Exchange:** `sim.exchange` (Topic Exchange, durable)
* **Telemetrie (Publish):** `sim.telemetry.<device-type>.<device-id>` (z. B. `sim.telemetry.charger.1`, `sim.telemetry.pv.1`)
* **Steuerbefehle (Subscribe):** `sim.commands.<device-type>.<device-id>` (z. B. `sim.commands.charger.1`)
* **AMQP-Port (5672):** Wird von [.NET-Projekten](file:///Users/alex/Github/dotnet-simulator-team-echo/dotNET_Programmierung_2026_Aufgabe_02.slnx) wie [`ModbusRabbitMqGateway`](file:///Users/alex/Github/dotnet-simulator-team-echo/ModbusRabbitMqGateway/Program.cs), [`RabbitMqViewer`](file:///Users/alex/Github/dotnet-simulator-team-echo/RabbitMqViewer/Program.cs) und [`Dual-Port-Charger`](file:///Users/alex/Github/dotnet-simulator-team-echo/Dual-Port-Charger/SimulatorEngine.cs) verwendet.
* **Management-Port (15672):** Bietet eine browserbasierte Weboberfläche zur Überwachung von Queues, Exchanges und Verbindungsstatistiken.

---

## 2. Voraussetzungen

Stellen Sie sicher, dass auf Ihrem System folgende Werkzeuge installiert sind:
1. **Docker Desktop** (macOS / Windows) oder **Docker Engine mit Docker Compose V2** (Linux).
2. Terminal bzw. Shell-Zugriff.
3. Überprüfen Sie Ihre Docker-Installation:
   ```bash
   docker --version
   docker compose version
   ```

---

## 3. Konfiguration: `.env` und `docker-compose.yml`

Das Projekt stellt eine vorkonfigurierte [`.env.example`](file:///Users/alex/Github/dotnet-simulator-team-echo/.env.example) sowie eine [docker-compose.yml](file:///Users/alex/Github/dotnet-simulator-team-echo/docker-compose.yml) im Projekt-Root bereit.

### 3.1 Die Datei `.env` erstellen

Kopieren Sie die Vorlage [`.env.example`](file:///Users/alex/Github/dotnet-simulator-team-echo/.env.example) in eine neue Datei `.env`:

```bash
cp .env.example .env
```

> [!NOTE]
> Die Datei `.env` ist in [.gitignore](file:///Users/alex/Github/dotnet-simulator-team-echo/.gitignore) eingetragen und wird nicht in Git versioniert. So können Entwickler lokale Einstellungen vornehmen, ohne Teamkonfigurationen zu überschreiben.

### 3.2 Parameter in `.env` verstehen & anpassen

Inhalte der `.env`-Datei:

```env
# ==============================================================================
# RabbitMQ Docker Konfiguration (Energy Twin 2026 - Team Echo)
# ==============================================================================

# RabbitMQ Benutzerzugangsdaten (wird beim ersten Start des Containers erzeugt)
RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=guest
RABBITMQ_DEFAULT_VHOST=/

# Externe Ports für Docker-Host-Mapping
RABBITMQ_PORT=5672
RABBITMQ_MGMT_PORT=15672

# ==============================================================================
# .NET Simulatoren & Gateway Umgebungsvariablen
# ==============================================================================

# RabbitMQ Verbindungsdaten für lokale C#-Applikationen
RABBITMQ_HOST=localhost
RABBITMQ_USER=guest
RABBITMQ_PASS=guest

# Simulations-Master Zeitsynchronisation
SIMULATION_MASTER_HOST=127.0.0.1
SIMULATION_MASTER_PORT=1510

# Weather-Simulator Minimal API Endpunkt
WEATHER_SIMULATOR_URL=http://localhost:5005

# Polling-Intervall des Modbus-RabbitMQ-Gateways in ms
MODBUS_POLL_INTERVAL_MS=1000
```

| Variable | Beschreibung | Standardwert |
| :--- | :--- | :--- |
| `RABBITMQ_DEFAULT_USER` | Benutzername des initialen RabbitMQ-Administrators | `guest` |
| `RABBITMQ_DEFAULT_PASS` | Passwort des RabbitMQ-Administrators | `guest` |
| `RABBITMQ_DEFAULT_VHOST` | Initialer Virtual Host | `/` |
| `RABBITMQ_PORT` | Host-Port für das AMQP-Nachrichtenprotokoll | `5672` |
| `RABBITMQ_MGMT_PORT` | Host-Port für das Web-Management-Interface | `15672` |
| `RABBITMQ_HOST` | Hostname, den .NET-Applikationen zum Verbindungsaufbau ansprechen | `localhost` |

### 3.3 Die Docker Compose Konfiguration ([docker-compose.yml](file:///Users/alex/Github/dotnet-simulator-team-echo/docker-compose.yml))

Die [docker-compose.yml](file:///Users/alex/Github/dotnet-simulator-team-echo/docker-compose.yml) nutzt das leichtgewichtige alpine-Image `rabbitmq:3.13-management-alpine` und verknüpft die Variablen aus `.env`:

```yaml
services:
  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    container_name: energy-twin-rabbitmq
    restart: unless-stopped
    ports:
      - "${RABBITMQ_PORT:-5672}:5672"
      - "${RABBITMQ_MGMT_PORT:-15672}:15672"
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_DEFAULT_USER:-guest}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_DEFAULT_PASS:-guest}
      RABBITMQ_DEFAULT_VHOST: ${RABBITMQ_DEFAULT_VHOST:-/}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s

volumes:
  rabbitmq_data:
    name: energy-twin-rabbitmq-data
    driver: local
```

---

## 4. RabbitMQ starten und verwalten

Führen Sie alle folgenden Befehle im Hauptverzeichnis des Repositories aus:

### 4.1 Container im Hintergrund starten

```bash
docker compose up -d
```

Erwartete Ausgabe:
```text
[+] Running 2/2
 ✔ Network dotnet-simulator-team-echo_default  Created
 ✔ Container energy-twin-rabbitmq              Started
```

### 4.2 Status und Healthcheck prüfen

```bash
docker compose ps
```

Sobald der Container betriebsbereit ist, sehen Sie den Status `healthy`:
```text
NAME                   IMAGE                            COMMAND                  SERVICE    STATUS
energy-twin-rabbitmq   rabbitmq:3.13-management-alpine  "docker-entrypoint.s…"   rabbitmq   Up (healthy)
```

### 4.3 Logs einsehen

Um zu überprüfen, ob der Broker fehlerfrei hochgefahren ist:
```bash
docker compose logs -f rabbitmq
```
*(Mit `Strg + C` beenden Sie die Log-Ansicht)*

### 4.4 Container stoppen und neu starten

* **Pausieren/Stoppen:**
  ```bash
  docker compose stop
  ```
* **Erneut starten:**
  ```bash
  docker compose start
  ```
* **Herunterfahren (Daten bleiben im Volume erhalten):**
  ```bash
  docker compose down
  ```
* **Vollständiger Reset (Löscht auch alle Queues, Exchanges und persistierte Daten):**
  ```bash
  docker compose down -v
  ```

---

## 5. Zugriff auf das RabbitMQ Management Web-UI

Nach erfolgreichem Start steht die Weboberfläche bereit:

1. Öffnen Sie Ihren Browser unter: **[http://localhost:15672](http://localhost:15672)**
2. Melden Sie sich mit den Zugangsdaten aus Ihrer `.env`-Datei an:
   * **Username:** `guest` (bzw. Ihr konfigurierter `RABBITMQ_DEFAULT_USER`)
   * **Password:** `guest` (bzw. Ihr konfigurierter `RABBITMQ_DEFAULT_PASS`)
3. **Wichtige Bereiche in der UI:**
   * **Overview:** Systemauslastung, Nachrichtenrate, verbundene Clients.
   * **Connections / Channels:** Zeigt aktive Verbindungen der .NET-Applikationen ([`ModbusRabbitMqGateway`](file:///Users/alex/Github/dotnet-simulator-team-echo/ModbusRabbitMqGateway/GatewayService.cs), [`RabbitMqViewer`](file:///Users/alex/Github/dotnet-simulator-team-echo/RabbitMqViewer/Program.cs)).
   * **Exchanges:** Hier erscheint nach Start der ersten Applikation der Topic Exchange `sim.exchange`.
   * **Queues:** Zeigt temporäre oder persistente Queues an, die von Consumern deklariert wurden.

---

## 6. Anbindung an die .NET-Applikationen

Die C#-Bibliothek [`TeamRabbitMQShared`](file:///Users/alex/Github/dotnet-simulator-team-echo/TeamRabbitMQShared/README.md) liest Verbindungsparameter über [`RabbitMqConfiguration.CreateDefault()`](file:///Users/alex/Github/dotnet-simulator-team-echo/TeamRabbitMQShared/Configuration/RabbitMqConfiguration.cs#L51-L65) automatisch aus den Umgebungsvariablen.

Falls Sie abweichende Ports oder Zugangsdaten in `.env` konfiguriert haben, exportieren Sie diese vor dem Start Ihrer .NET-Konsolenprogramme in Ihrem Terminal:

```bash
export RABBITMQ_HOST=localhost
export RABBITMQ_PORT=5672
export RABBITMQ_USER=guest
export RABBITMQ_PASS=guest
```

Starten Sie anschließend beispielsweise das Gateway oder den Viewer:
```bash
# Gateway starten:
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj

# Live-Viewer starten:
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
```

Dank der integrierten Wiederverbindungslogik ([`AutoReconnect`](file:///Users/alex/Github/dotnet-simulator-team-echo/TeamRabbitMQShared/Configuration/RabbitMqConfiguration.cs#L28)) stürzen die Simulatoren nicht ab, wenn RabbitMQ kurzzeitig neu gestartet wird – sie verbinden sich nach Wiederverfügbarkeit automatisch erneut.

---

## 7. Fehlerbehebung & Häufige Probleme

### 1. Port 5672 oder 15672 bereits belegt
* **Fehler:** `Bind for 0.0.0.0:5672 failed: port is already allocated`
* **Ursache:** Ein lokaler RabbitMQ-Dienst (z. B. via Homebrew oder systemd) läuft bereits auf dem Rechner.
* **Lösung:**
  1. Prüfen, welcher Prozess den Port belegt:
     ```bash
     lsof -i :5672
     lsof -i :15672
     ```
  2. Bestehenden Dienst stoppen (z. B. `brew services stop rabbitmq`) oder in der `.env`-Datei alternative Ports wählen (z. B. `RABBITMQ_PORT=5673` und `RABBITMQ_MGMT_PORT=15673`).

### 2. Login im Web-UI schlägt fehl
* **Ursache:** Wurde der Container zuvor bereits mit anderen Zugangsdaten gestartet, behält das Docker-Volume `energy-twin-rabbitmq-data` die alten Credentials bei. Änderungen an `RABBITMQ_DEFAULT_USER` in `.env` greifen erst nach Neuerstellung des Volumes.
* **Lösung:** Volume zurücksetzen:
  ```bash
  docker compose down -v
  docker compose up -d
  ```

### 3. Container wird nicht "healthy"
* **Lösung:** Prüfen Sie die Container-Logs:
  ```bash
  docker compose logs rabbitmq
  ```
  Stellen Sie sicher, dass genügend freier Festplattenspeicher zur Verfügung steht (RabbitMQ stoppt bei weniger als 50 MB freiem Speicherplatz automatisch die Listener zum Schutz der Queue-Daten).
