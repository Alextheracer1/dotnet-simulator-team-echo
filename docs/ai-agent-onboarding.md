# 🤖 KI-Agenten Onboarding & Skill-Handbuch: Energy Twin 2026 (Team Echo)

Dieses Dokument dient als **zentraler Schnellstart- und Orientierungsleitfaden für neue KI-Agenten** (wie Google Antigravity, Claude, Copilot oder andere LLMs), die neu in diesem Repository arbeiten. Es fasst die Architektur, Konventionen, Kommunikationsschichten und alle verfügbaren **Agent-Skills** (`.agents/skills/`) kompakt und handlungsorientiert zusammen.

---

## 1. Executive Summary & Projektzweck

* **Projektname:** Energy Twin 2026 – Verteiltes Simulationssystem (Team Echo / Team 01)
* **Technologie-Stack:** .NET 10.0 (`net10.0`), C# 13/14, ASP.NET Core Minimal APIs, Spectre.Console, Modbus TCP, RabbitMQ AMQP.
* **Kernaufgabe:** Echtzeitsimulation und digitaler Zwilling eines verteilten Energiesystems bestehend aus:
  * **Erzeugern:** Photovoltaik (`PV_Simulator`), Windkraft (`WindTurbine_Simulator`)
  * **Speichern:** Großbatteriespeicher (`Battery`)
  * **Verbrauchern / Ladeinfrastruktur:** 2-Port DC-Schnellladestation (`Dual-Port-Charger`)
  * **Infrastruktur:** Zentraler Taktgeber (`Simulations-Master`), Realdaten-Wetterdienst (`WeatherSimulator`), Feldbus-Bridge (`ModbusRabbitMqGateway`) und Monitoring (`RabbitMqViewer`, `ModbusDiagnoseClient`).

---

## 2. Systemarchitektur & Kommunikationsschichten

Das System basiert auf vier klar getrennten Kommunikationsmechanismen:

```
                                ┌────────────────────────┐
                                │   Simulations-Master   │ (Zentraler Taktgeber & Barriere)
                                │       (TCP 1510)       │
                                └───────────┬────────────┘
                                            │ Virtuelle Zeit & Zeitschritte
         ┌──────────────────────────────────┼──────────────────────────────────┐
         ▼                                  ▼                                  ▼
 ┌─────────────────┐             ┌─────────────────────┐             ┌─────────────────────┐
 │ WeatherSimulator│             │   PV_Simulator      │             │WindTurbine_Simulator│
 │   (HTTP 5005)   │───Push─────►│ (Modbus TCP 1504)   │   ┌Push────►│ (Modbus TCP 1505)   │
 └─────────────────┘  (Callbacks)│ (Callback ab 6000)  │   │(Callbacks│(Callback ab 6100)  │
         │                       └──────────┬──────────┘   │         └──────────┬──────────┘
         │                                  │              │                    │
         │         ┌────────────────────────┼──────────────┴────────────────────┘
         │         │                        │
         │         ▼                        ▼
         │   ┌───────────────┐        ┌───────────────┐
         │   │Dual-Port-Charg│        │    Battery    │
         │   │(Modbus 1502)  │        │ (Modbus 1503) │
         │   └───────┬───────┘        └───────┬───────┘
         │           │                        │
         └───────────┼────────────────────────┘
                     ▼ (Modbus Polling)
         ┌─────────────────────────┐
         │  ModbusRabbitMqGateway  │
         └───────────┬─────────────┘
                     ▼ (sim.exchange)
         ┌─────────────────────────┐
         │     RabbitMQ Broker     │ (Port 5672)
         └───────────┬─────────────┘
                     ▼ (sim.telemetry.#)
         ┌─────────────────────────┐
         │     RabbitMqViewer      │ (Echtzeit-Dashboard)
         └─────────────────────────┘
```

### Die 4 Protokoll-Schichten:

1. **Virtuelle Zeit & Taktung (Master-TCP `1510`):**
   * Zentraler Koordinator ist der `Simulations-Master`.
   * Simulatoren melden sich an (`ITimeMasterClient`), empfangen `TimeStep`-Nachrichten (JSON zeilengetrennt) und bestätigen mit `Acknowledgement` (**Zeitschritt-Barriere**). Erst wenn alle Clients bestätigt haben, schaltet der Master den Zeitschritt weiter.
2. **Wetter-Push (HTTP REST `5005` & Callbacks ab `6000`):**
   * `WeatherSimulator` liest 8.784 Stunden realer Salzburger Wetterdaten (CSV) ein, interpoliert linear und berechnet Sonnenazimut/-elevation.
   * PV und Wind registrieren sich dynamisch per Callback (`POST /api/weather/register`) und erhalten pro Zeitschritt Wetter-Snapshots (mit lokalem Offline-Fallback bei Timeout).
3. **Feldbusebene (Modbus TCP Ports `1502–1505`):**
   * Alle Feldgeräte implementieren standardisierte **LB02 Holding-Register 1–12** (OperatingState, ErrorCode, ActivePower Watts, CumulativeEnergy kWh, VirtualTime Unix-Timestamp, OnOffState, Setpoint, ResetError) sowie gerätespezifische Register (13+).
4. **Messaging & Telemetrie (RabbitMQ `5672` / Exchange `sim.exchange`):**
   * Topic: `sim.telemetry.<deviceType>.<id>` für Sensordaten (`DeviceTelemetryMessage`).
   * Topic: `sim.commands.<deviceType>.<id>` für Steuerbefehle (`DeviceCommandMessage`).
   * Dualer Weg: Feldgeräte verfügen über native Publisher/Consumer; `ModbusRabbitMqGateway` dient als Modbus-Polling-Bridge.

---

## 3. Modul- und Port-Übersicht

| Modul / Ordner | Projekt-Typ | Port / Endpunkt | Hauptaufgabe |
| :--- | :--- | :--- | :--- |
| **`TeamShared`** | Class Library | – | Modbus-Wrapper, `CommonRegisterMap`, Master-Protokoll & `ITimeMasterClient`. |
| **`TeamConsoleShared`** | Class Library | – | Wiederverwendbare Spectre.Console UI-Komponenten (Widgets, Power-Bars, Badges). |
| **`TeamRabbitMQShared`** | Class Library | – | Publisher/Consumer-Klassen, Konfiguration und DTOs für RabbitMQ. |
| **`TeamRabbitMQShared.Tests`**| Unit Tests | – | Automatisierte Tests für RabbitMQ-DTOs und Mapper (xUnit). |
| **`Simulations-Master`** | Console App | TCP `1510` | Virtueller Zeitgeber mit Barriere und Tastatursteuerung (`S`, `P`, `R`, `+`, `-`, `Q`). |
| **`WeatherSimulator`** | Web Minimal API | HTTP `5005`, Master-TCP `1510` | Liest Salzburger Wetter-CSV, Push via HTTP POST an PV/Wind. |
| **`Dual-Port-Charger`** | Console App | Modbus `1502`, Master-TCP `1510` | 2-Port DC-Schnelllader mit dynamischer Lastverteilung. |
| **`Battery`** | Console App | Modbus `1503`, Master-TCP `1510` | Großbatteriespeicher (SOC, Lade-/Entladeleistung). |
| **`PV_Simulator`** | Console App | Modbus `1504`, Master-TCP `1510`, Callback `6000+` | Photovoltaikanlage mit realer Einstrahlung & Sonnenstand. |
| **`WindTurbine_Simulator`** | Console App | Modbus `1505`, Master-TCP `1510`, Callback `6100+` | Windkraftanlage (Vestas V90) mit Wind- & Turbulenzmodell. |
| **`ModbusRabbitMqGateway`**| Console App | Modbus Client, RabbitMQ Publisher/Consumer | Pollt Modbus-Register 1502–1505 und leitet Befehle an Modbus weiter. |
| **`RabbitMqViewer`** | Console App | RabbitMQ Consumer | Interaktives Live-Dashboard für alle Telemetrieströme. |
| **`ModbusDiagnoseClient`** | Console App | Modbus Client | Interaktives Tool zum manuellen Lesen/Schreiben von Modbus-Registern. |

---

## 4. Verfügbare Agent-Skills (`.agents/skills/`)

Im Verzeichnis `.agents/skills/` sind 6 spezialisierte Workflows und Skills abgelegt. Ein neuer KI-Agent sollte diese Skills für wiederkehrende Aufgaben stets heranziehen:

```
.agents/skills/
├── build-and-test/            # Kompilieren & Validieren der Solution
├── development-journal/       # Führen der Entwicklungsjournale
├── modbus-rabbitmq-gateway/   # Betrieb der Modbus-RabbitMQ-Bridge
├── rabbitmq-messaging/        # Vorlagen für Telemetrie-Pub/Sub im Code
├── rabbitmq-viewer/           # Live-Telemetrie-Monitor starten
└── run-simulators/            # Geordnetes Starten der Simulationsknoten
```

### Detailübersicht der Skills:

### 🛠️ 1. `build-and-test`
* **Zweck:** Bauen und Validieren der gesamten .NET 10 Solution `dotNET_Programmierung_2026_Aufgabe_02.slnx`.
* **Wann nutzen:** Vor jedem Commit, nach Refactorings und vor der Dokumentation im Journal.
* **Besonderheit:** Unter macOS / Sandbox **immer mit `-m:1`** ausführen, um IPC-Fehler zu vermeiden:
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
  ```
* **Tests ausführen:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet test TeamRabbitMQShared.Tests/TeamRabbitMQShared.Tests.csproj -m:1
  ```

---

### 📝 2. `development-journal`
* **Zweck:** Führen und Aktualisieren der individuellen studentischen Entwicklungsjournale in `docs/development-journal/`.
* **Wann nutzen:** Nach jedem abgeschlossenen Arbeitspaket, Feature oder Refactoring.
* **Pflicht-Tags (Format-Vorschrift):**
  * `- **Done:**` Was wurde konkret umgesetzt?
  * `- **KI:**` Welches Modell, welche Prompting-/Einsatzform, kritische Prüfung (oder `keine`).
  * `- **Artefact:**` Exakte Dateipfade aller neuen/geänderten Dateien.
  * `- **Test:**` (Optional) Testergebnis (automatisiert oder manuell).
  * `- **Comment:**` (Optional) Architekturentscheidungen, Herausforderungen.
* **Dateien:** `docs/development-journal/Alexander-Graf.md`, `docs/development-journal/Nikolas-Giesen.md`.

---

### 🚀 3. `run-simulators`
* **Zweck:** Anleitung zum Starten des Gesamtsystems in separaten Terminal-Instanzen.
* **Wann nutzen:** Wenn die Simulation live ausgeführt, getestet oder vorgeführt werden soll.
* **Start-Sequenz (4 Phasen):**
  1. **Phase 1 (Infrastruktur & Takt):** Docker RabbitMQ (`docker compose up -d`) & `Simulations-Master` (Port 1510)
  2. **Phase 2 (Wetterdienst):** `WeatherSimulator` (Port 5005)
  3. **Phase 3 (Feldgeräte):** `Dual-Port-Charger` (1502), `Battery` (1503), `PV_Simulator` (1504), `WindTurbine_Simulator` (1505)
  4. **Phase 4 (Bridge & Viewer):** `ModbusRabbitMqGateway` & `RabbitMqViewer`
* **Bequeme Alternative:** Die plattformübergreifenden Skripte `scripts/start-all.sh` (macOS/Linux) bzw. `scripts/start-all.bat` / `scripts/start-all.ps1` (Windows).

---

### 🌉 4. `modbus-rabbitmq-gateway`
* **Zweck:** Dokumentiert Konfiguration und Betrieb des Gateways zwischen Modbus TCP und RabbitMQ.
* **Wann nutzen:** Wenn Feldgeräte ohne direkte RabbitMQ-Anbindung angebunden oder Modbus-Register über RabbitMQ ferngesteuert werden sollen.
* **Befehl:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj
  ```

---

### 📨 5. `rabbitmq-messaging`
* **Zweck:** Bereitstellung von Code-Snippets und Best Practices für `TeamRabbitMQShared`.
* **Wann nutzen:** Wenn ein neues Feldgerät oder ein Werkzeug Telemetriedaten publizieren oder Steuerbefehle empfangen soll.
* **Kernklassen:** `RabbitMqPublisher`, `RabbitMqConsumer`, `DeviceTelemetryMessage`, `DeviceCommandMessage`.

---

### 📊 6. `rabbitmq-viewer`
* **Zweck:** Starten und Steuern des interaktiven Spectre.Console Dashboards.
* **Wann nutzen:** Zur visuellen Live-Überprüfung von Datenströmen, kW-Werten, SOC und Paketraten aller Simulatoren.
* **Befehl:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
  ```

---

## 5. Verbindliche Entwicklungs- und Arbeitsregeln für KIs

Als neuer KI-Agent in diesem Projekt musst du folgende Regeln strikt beachten:

1. **Build-Umgebung unter macOS / Sandbox:**
   * Verwende immer `DOTNET_CLI_HOME=/tmp/dotnet_home`.
   * Ergänze bei `dotnet build`, `dotnet restore` und `dotnet test` **immer `-m:1`** (deaktiviert MSBuild Multi-Process Node Worker).
2. **Keine Reconnect-Blockaden:**
   * Alle Netzwerk-Clients (TCP-Master, Modbus, RabbitMQ, HTTP) müssen asynchron und resilient sein. Fällt ein Server aus, darf die Anwendung niemals einfrieren oder abstürzen.
3. **Register-Treue (LB02):**
   * Register 1–12 sind heilig. Änderungen an Holding-Registern müssen dem LB02-Standard entsprechen.
4. **Wetter-Push & Master-Synchronisation:**
   * Erst lokale Berechnungen für den Zeitschritt abschließen, dann das `Acknowledgement` an den Master senden.
5. **Entwicklungsjournal pflegen:**
   * Bei Code-Änderungen immer das Entwicklungsjournal des jeweiligen Entwicklers nach dem Schema aus dem `development-journal`-Skill fortschreiben.

---

## 6. Schnelle Orientierung (Dateipfade & Dokumente)

* **Tutorials:**
  * [Ausführen der .NET Applikationen](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-dotnet-applications.md)
  * [RabbitMQ mit Docker einrichten](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-rabbitmq-docker.md)
* **Architektur-Spezifikation:**
  * [Gesamt-Projektkontext (PROJECT_CONTEXT.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/PROJECT_CONTEXT.md)
  * [Master-Client-Protokoll (SimulationMaster-Client-Protocol.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/TeamShared/SimulationMaster-Client-Protocol.md)
  * [Agent Guidelines (AGENTS.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/AGENTS.md)
* **Startskripte:**
  * macOS / Linux: `scripts/start-all.sh`, `scripts/stop-all.sh`
  * Windows: `scripts/start-all.bat`, `scripts/start-all.ps1`
