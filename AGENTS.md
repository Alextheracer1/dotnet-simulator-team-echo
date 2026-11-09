# 🤖 Agent Guidelines, Development Standards & Onboarding: Energy Twin 2026 (Team Echo)

Dieses Dokument definiert die verbindlichen Richtlinien, Architekturstandards, Protokollspezifikationen und Vorgehensweisen für Entwickler und autonome KI-Agenten (wie Google Antigravity, Claude, Copilot oder andere LLMs) im Repository `dotnet-simulator-team-echo`. Es dient als zentraler Onboarding- und Referenzleitfaden für das Gesamtsystem.

---

## 1. Executive Summary & Projektzweck

* **Projektname:** Energy Twin 2026 – Verteiltes Simulationssystem (Team Echo / Team 01)
* **Technologie-Stack:** .NET 10.0 (`net10.0`), C# 13/14, ASP.NET Core Minimal APIs, Spectre.Console, Modbus TCP, RabbitMQ AMQP.
* **Architektur:** Verteilte .NET 10 Konsolen- und Webapplikationen, die als eigenständige, isolierte Prozesse laufen.
* **Kernaufgabe:** Echtzeitsimulation und digitaler Zwilling eines verteilten Energiesystems bestehend aus:
  * **Erzeugern:** Photovoltaik (`PV_Simulator`), Windkraft (`WindTurbine_Simulator`)
  * **Speichern:** Großbatteriespeicher (`Battery`)
  * **Verbrauchern / Ladeinfrastruktur:** 2-Port DC-Schnellladestation (`Dual-Port-Charger`)
  * **Infrastruktur & Taktung:** Zentraler Zeitgeber (`Simulations-Master`), Realdaten-Wetterdienst (`WeatherSimulator`), Feldbus-Bridge (`ModbusRabbitMqGateway`) und Monitoring (`RabbitMqViewer`, `ModbusDiagnoseClient`).

---

## 2. Systemarchitektur & Kommunikationsschichten

Das System basiert auf vier klar voneinander abgegrenzten Kommunikationsmechanismen:

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

### Die 4 Protokoll-Schichten im Detail:

1. **Virtuelle Zeit & Taktung (Master-TCP `1510`):**
   * Zentraler Koordinator ist der `Simulations-Master`.
   * Simulatoren melden sich an (`ITimeMasterClient`), empfangen `TimeStep`-Nachrichten (JSON, zeilengetrennt) und bestätigen mit `Acknowledgement` (**Zeitschritt-Barriere**). Erst wenn alle Clients bestätigt haben, schaltet der Master den Zeitschritt weiter.
2. **Wetter-Push (HTTP REST `5005` & Callbacks ab `6000` / `6100`):**
   * `WeatherSimulator` liest 8.784 Stunden realer Salzburger Wetterdaten (CSV) ein, interpoliert linear und berechnet Sonnenazimut/-elevation.
   * PV und Wind registrieren sich dynamisch per Callback (`POST /api/weather/register`) und erhalten pro Zeitschritt Wetter-Snapshots (mit lokalem Offline-Fallback bei Timeout).
3. **Feldbusebene (Modbus TCP Ports `1502–1505`):**
   * Jeder Feldgeräte-Simulator betreibt einen Modbus TCP-Server auf einem unprivilegierten Port (1502–1510) mit standardisiertem **LB02 Holding-Register-Layout (1–12)** sowie gerätespezifischen Registern (13+).
4. **Messaging & Telemetrie (RabbitMQ `5672` / Exchange `sim.exchange`):**
   * Asynchrone Verteilung von Sensordaten und Telemetrie über Topic-Exchanges (`sim.exchange`, Typ `topic`, durable) sowie Empfang von Steuerbefehlen/Sollwerten.
   * Dualer Weg: Feldgeräte verfügen über native Publisher/Consumer; `ModbusRabbitMqGateway` dient zusätzlich als Modbus-Polling-Bridge.

---

## 3. Modul- und Port-Übersicht

| Modul / Ordner | Projekt-Typ | Port / Endpunkt | Hauptaufgabe |
| :--- | :--- | :--- | :--- |
| **`TeamShared`** | Class Library | – | Modbus-Wrapper (`FieldDeviceModbusServer`), `CommonRegisterMap`, Master-Protokoll & `ITimeMasterClient`. |
| **`TeamConsoleShared`** | Class Library | – | Wiederverwendbare Spectre.Console UI-Komponenten (`SimulationDashboard`, `StatusBadge`, `GaugeBar`, `LiveSimulationView`). |
| **`TeamRabbitMQShared`** | Class Library | – | Wiederverwendbare Bibliothek für RabbitMQ Publisher, Consumer, Konfiguration und DTOs. |
| **`TeamRabbitMQShared.Tests`**| Unit Tests | – | Automatisierte Tests für RabbitMQ-DTOs und Mapper (xUnit). |
| **`Simulations-Master`** | Console App | TCP `1510` | Zentraler Taktgeber & Zeitsynchronisation mit Zeitschritt-Barriere und interaktiver Steuerung (`S`, `P`, `R`, `+`, `-`, `Q`). |
| **`WeatherSimulator`** | Web Minimal API | HTTP `5005`, Master-TCP `1510` | Wetter- und Sonnendaten-Provider für PV & Windkraft (Salzburger CSV-Daten, Push via HTTP POST). |
| **`Dual-Port-Charger`** | Console App | Modbus `1502`, Master-TCP `1510` | Feldgerät: DC-Schnellladesäule mit 2 Ladepunkten und dynamischer Lastverteilung (RabbitMQ Pub & Sub). |
| **`Battery`** | Console App | Modbus `1503`, Master-TCP `1510` | Feldgerät: Großbatteriespeicher-Simulator (SOC, Lade-/Entladeleistung, Spannung, Strom). |
| **`PV_Simulator`** | Console App | Modbus `1504`, Master-TCP `1510`, Callback `6000+` | Feldgerät: Photovoltaik-Simulator mit realer Einstrahlung & Sonnenstandsberechnung. |
| **`WindTurbine_Simulator`** | Console App | Modbus `1505`, Master-TCP `1510`, Callback `6100+` | Feldgerät: Windkraftanlage (Vestas V90) mit Wind- & Turbulenzmodell. |
| **`ModbusRabbitMqGateway`**| Console App | Modbus Client, RabbitMQ Pub/Sub | Gateway: Pollt Modbus-Register 1502–1505, publiziert via RabbitMQ und leitet Befehle an Modbus weiter. |
| **`RabbitMqViewer`** | Console App | RabbitMQ Consumer | Interaktiver Live-Monitor und Spectre.Console Dashboard für alle Telemetrieströme. |
| **`ModbusDiagnoseClient`** | Console App | Modbus Client | Interaktives Testwerkzeug zum manuellen Lesen und Schreiben von Modbus-Registern. |

---

## 4. Modbus Registerbelegung (LB02 Standard)

Alle Feldgeräte implementieren im Registerkopf (**Holding Registers 1–12**) den einheitlichen LB02-Standard:

| Register | Name | Datentyp | Einheit / Wertebereich | Beschreibung |
| :---: | :--- | :--- | :--- | :--- |
| **1** | `OperatingState` | Int16 | 0=Off, 1=Booting, 2=Running, 3=Error | Aktueller Betriebszustand des Geräts |
| **2** | `ErrorCode` | Int16 | 0=None, >0=Error-Code | Spezifischer Fehlercode |
| **3–4** | `ActivePower` | Float32 | Watt (High Word / Low Word) | Aktuelle Wirkleistung (Erzeugung positiv, Verbrauch negativ) |
| **5–6** | `CumulativeEnergy`| Float32 | kWh (High Word / Low Word) | Kumulierte Gesamtenergie |
| **7–8** | `VirtualTime` | Int32 | Unix-Timestamp (Low Word / High Word) | Aktuelle virtuelle Simulationszeit |
| **9** | `OnOffState` | Int16 | 0=Off, 1=On | Hauptschalter des Simulators |
| **10–11**| `Setpoint` | Float32 | High Word / Low Word | Leistungssollwert oder Ladeleistungsvorgabe |
| **12** | `ResetError` | Int16 | 1=Reset ausführen | Schreiben von 1 setzt Fehlerzustand zurück |
| **13+** | *Gerätespezifisch*| Variabel | Je nach Gerätetyp | Siehe gerätespezifische Registerdokumentation |

### Gerätespezifische Register (Auswahl):
* **Dual-Port Charger (Port 1502):** Reg 13–14 (SOC Port 1 Float32), Reg 15–16 (Power Port 1 Float32), Reg 17–18 (SOC Port 2 Float32), Reg 19–20 (Power Port 2 Float32).
* **Battery (Port 1503):** Reg 13–14 (State of Charge in % Float32), Reg 15–16 (Batteriespannung in V Float32), Reg 17–18 (Batteriestrom in A Float32).
* **PV Simulator (Port 1504):** Reg 13–14 (Globalstrahlung W/m² Float32), Reg 15–16 (Modultemperatur °C Float32).
* **Wind Turbine (Port 1505):** Reg 13–14 (Windgeschwindigkeit m/s Float32), Reg 15–16 (Rotordrehzahl RPM Float32).

---

## 5. RabbitMQ Messaging Konventionen

* **Exchange:** `sim.exchange` (Typ: `topic`, durable)
* **Routing Keys:**
  * **Telemetrie (Publisher):** `sim.telemetry.<device-type>.<device-id>`  
    *Beispiele:* `sim.telemetry.charger.1`, `sim.telemetry.battery.1`, `sim.telemetry.pv.1`, `sim.telemetry.wind.1`
  * **Steuerbefehle (Consumer):** `sim.commands.<device-type>.<device-id>`  
    *Beispiel:* `sim.commands.charger.1`, `sim.commands.battery.1`
* **Nachrichtenformate (JSON DTOs in `TeamRabbitMQShared`):**
  * **Telemetrie:** `DeviceTelemetryMessage`  
    Enthält: `DeviceId`, `Type`, `Timestamp`, `VirtualTime`, `OperatingState`, `ErrorCode`, `ActivePowerWatts`, `CumulativeEnergyKwh`, `DeviceSpecificData` (Dictionary für gerätespezifische Metriken).
  * **Befehle:** `DeviceCommandMessage`  
    Enthält: `DeviceId`, `CommandType` (z.B. `SetPowerLimit`, `ResetFault`, `ToggleCar`), `Value`, `Timestamp`.
* **Verbindungsresilienz:** Simulatoren dürfen bei vorübergehend nicht erreichbarem Broker niemals blockieren oder abstürzen. Reconnects laufen asynchron und im Hintergrund.

---

## 6. Build-, Test- und Ausführungsvorschriften

1. **SDK:** .NET 10.0 (`/usr/local/share/dotnet/dotnet` bzw. im System-`PATH`).
2. **MSBuild Parallelism unter macOS / Sandbox:**  
   In sandboxed Umgebungen oder bei eingeschränktem IPC **immer `-m:1`** und `DOTNET_CLI_HOME=/tmp/dotnet_home` verwenden:
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
   ```
3. **Unit Tests ausführen:**
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet test TeamRabbitMQShared.Tests/TeamRabbitMQShared.Tests.csproj -m:1
   ```
4. **NuGet-Konfiguration:** Das Repository enthält eine vorkonfigurierte `nuget.config`, die lokale Caches berücksichtigt und deterministische Restores gewährleistet.

---

## 7. Verfügbare Agent-Skills (`.agents/skills/`)

Im Verzeichnis `.agents/skills/` (sowie als Alias unter `skills/`) stehen 6 spezialisierte Workflows bereit. KI-Agenten und Entwickler sollten diese Workflows für wiederkehrende Aufgaben stets heranziehen:

```
.agents/skills/
├── build-and-test/            # Kompilieren & Validieren der Solution
├── development-journal/       # Führen der studentischen Entwicklungsjournale
├── modbus-rabbitmq-gateway/   # Konfiguration & Betrieb der Modbus-RabbitMQ-Bridge
├── rabbitmq-messaging/        # Vorlagen für Telemetrie-Pub/Sub & Commands im Code
├── rabbitmq-viewer/           # Live-Telemetrie-Monitor starten & konfigurieren
└── run-simulators/            # Geordnetes Starten der Simulationsknoten
```

### Detailübersicht der Skills:

### 🛠️ 1. `build-and-test`
* **Zweck:** Bauen und Validieren der gesamten .NET 10 Solution `dotNET_Programmierung_2026_Aufgabe_02.slnx`.
* **Wann nutzen:** Vor jedem Commit, nach Refactorings und vor dem Eintragen ins Journal.
* **Befehle:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet test TeamRabbitMQShared.Tests/TeamRabbitMQShared.Tests.csproj -m:1
  ```

---

### 📝 2. `development-journal`
* **Zweck:** Führen und Aktualisieren der individuellen studentischen Entwicklungsjournale in `docs/development-journal/`.
* **Wann nutzen:** Nach jedem abgeschlossenen Arbeitspaket, Feature, Bugfix oder Refactoring.
* **Pflicht-Tags (Format-Vorschrift):**
  * `- **Done:**` Was wurde konkret umgesetzt?
  * `- **KI:**` Welches Modell, welche Prompting-/Einsatzform, kritische Prüfung (oder `keine`).
  * `- **Artefact:**` Exakte Dateipfade aller neuen/geänderten Dateien.
  * `- **Test:**` (Optional) Testergebnis (automatisiert oder manuell).
  * `- **Comment:**` (Optional) Architekturentscheidungen, Herausforderungen.
* **Zugehörige Dateien:**
  * `docs/development-journal/Alexander-Graf.md`
  * `docs/development-journal/Nikolas-Giesen.md`
  * `docs/development-journal/Nazar-Burdeinyi.md`

---

### 🚀 3. `run-simulators`
* **Zweck:** Geordnetes Hochfahren des verteilten Gesamtsystems in separaten Terminal-Instanzen.
* **Wann nutzen:** Wenn die Simulation live ausgeführt, integriert getestet oder vorgeführt werden soll.
* **Verbindliche Start-Sequenz (4 Phasen):**
  1. **Phase 1 (Infrastruktur & Takt):** Docker RabbitMQ (`docker compose up -d`) & `Simulations-Master` (Port 1510)
  2. **Phase 2 (Wetterdienst):** `WeatherSimulator` (Port 5005)
  3. **Phase 3 (Feldgeräte):** `Dual-Port-Charger` (1502), `Battery` (1503), `PV_Simulator` (1504), `WindTurbine_Simulator` (1505)
  4. **Phase 4 (Bridge & Viewer):** `ModbusRabbitMqGateway` & `RabbitMqViewer`
* **Bequeme Start-Skripte:**
  * macOS / Linux: `scripts/start-all.sh`, `scripts/stop-all.sh`
  * Windows: `scripts/start-all.bat`, `scripts/start-all.ps1`

---

### 🌉 4. `modbus-rabbitmq-gateway`
* **Zweck:** Dokumentiert Konfiguration und Betrieb des Gateways zwischen Modbus TCP und RabbitMQ.
* **Wann nutzen:** Wenn Feldgeräte ohne direkte RabbitMQ-Anbindung integriert oder Modbus-Register über RabbitMQ ferngesteuert werden sollen.
* **Befehl:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj
  ```

---

### 📨 5. `rabbitmq-messaging`
* **Zweck:** Bereitstellung von Best Practices und Code-Snippets für `TeamRabbitMQShared`.
* **Wann nutzen:** Wenn ein Feldgerät Telemetriedaten publizieren oder Steuerbefehle empfangen soll.
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

## 8. Verbindliche Entwicklungs- und Arbeitsregeln für KI-Agenten

Als KI-Agent in diesem Projekt musst du folgende Regeln strikt beachten:

1. **Build-Umgebung unter macOS / Sandbox:**
   * Verwende immer `DOTNET_CLI_HOME=/tmp/dotnet_home`.
   * Ergänze bei `dotnet build`, `dotnet restore` und `dotnet test` **immer `-m:1`** (deaktiviert MSBuild Multi-Process Node Worker).
2. **Keine Reconnect-Blockaden:**
   * Alle Netzwerk-Clients (TCP-Master, Modbus, RabbitMQ, HTTP) müssen asynchron und resilient sein. Fällt ein Server aus, darf die Anwendung niemals einfrieren oder abstürzen.
3. **Register-Treue (LB02):**
   * Register 1–12 sind heilig. Änderungen an Holding-Registern müssen dem LB02-Standard entsprechen.
4. **Wetter-Push & Master-Synchronisation:**
   * Erst lokale Berechnungen für den Zeitschritt abschließen, dann das `Acknowledgement` an den Simulations-Master senden.
5. **Entwicklungsjournal pflegen:**
   * Nach relevanten Code-Änderungen das jeweilige studentische Entwicklungsjournal nach dem Schema aus dem `development-journal`-Skill fortschreiben.

---

## 9. Schnelle Orientierung (Dateipfade & Dokumente)

* **Praxis-Tutorials:**
  * [Ausführen der .NET Applikationen](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-dotnet-applications.md)
  * [RabbitMQ mit Docker und .env einrichten](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-rabbitmq-docker.md)
* **Architektur & Spezifikation:**
  * [Gesamt-Projektkontext (PROJECT_CONTEXT.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/PROJECT_CONTEXT.md)
  * [Master-Client-Protokoll (SimulationMaster-Client-Protocol.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/TeamShared/SimulationMaster-Client-Protocol.md)
  * [Zentrale Dokumentationsübersicht (docs/README.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/README.md)
* **Startskripte:**
  * macOS / Linux: `scripts/start-all.sh`, `scripts/stop-all.sh`
  * Windows: `scripts/start-all.bat`, `scripts/start-all.ps1`
