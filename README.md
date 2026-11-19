# ⚡ Energy Twin 2026 – Verteiltes Simulationssystem (Team Echo / Team 01)

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13/14](https://img.shields.io/badge/C%23-13%2F14-239120?style=flat&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![RabbitMQ AMQP](https://img.shields.io/badge/RabbitMQ-AMQP%200--9--1-FF6600?style=flat&logo=rabbitmq)](https://www.rabbitmq.com/)
[![Modbus TCP](https://img.shields.io/badge/Modbus-TCP%20(Ports%201502--1505)-00599C?style=flat)](https://modbus.org/)
[![Spectre.Console](https://img.shields.io/badge/UI-Spectre.Console-blue?style=flat)](https://spectreconsole.net/)
[![Build Status](https://img.shields.io/badge/Build-0%20Errors%20%7C%20Passing-brightgreen?style=flat)](dotNET_Programmierung_2026_Aufgabe_02.slnx)
[![Tests Passing](https://img.shields.io/badge/Tests-36%2F36%20Passing-success?style=flat)](TeamRabbitMQShared.Tests/)

**Energy Twin 2026** ist ein industrienahes, verteiltes Simulationssystem und ein digitaler Zwilling eines modernen Microgrids mit erneuerbaren Erzeugern, Großspeichern und DC-Ladeinfrastruktur. Entwickelt als verteilte .NET 10 Multi-Prozess-Architektur für die Lehrveranstaltung *.NET Programmierung für industrienahe Anwendungen* (Leistungsbeurteilungen **LB02** & **LB03**).

Das System simuliert physikalische Prozesse in Echtzeit über eine gemeinsame virtuelle Simulationszeit, unterstützt standortgenaue Wetterdaten aus Realdatensätzen, bietet standardisierte Feldbusse (Modbus TCP) sowie skalierbares Event-Messaging (RabbitMQ Topic Pub/Sub) und wird über reaktive Spectre.Console Dashboards visualisiert.

---

## 📑 Inhaltsverzeichnis

- [Systemarchitektur & Kommunikationsschichten](#-systemarchitektur--kommunikationsschichten)
- [Module & Verantwortlichkeiten](#-module--verantwortlichkeiten)
- [Netzwerk-, Port- & Register-Matrix](#-netzwerk--port---register-matrix)
  - [Port-Übersicht](#port-übersicht)
  - [Modbus LB02 Registerbelegung (Holding Register 1–12)](#modbus-lb02-registerbelegung-holding-register-112)
  - [Gerätespezifische Register (13+)](#gerätespezifische-register-13)
  - [RabbitMQ Routing-Key Konventionen](#rabbitmq-routing-key-konventionen)
- [Voraussetzungen & Vorbereitung](#-voraussetzungen--vorbereitung)
- [Schnellstart & Ausführung](#-schnellstart--ausführung)
  - [Option A: Automatisierter Start aller Komponenten (Empfohlen)](#option-a-automatisierter-start-aller-komponenten-empfohlen)
  - [Option B: Manuelle 4-Phasen-Inbetriebnahme](#option-b-manuelle-4-phasen-inbetriebnahme)
- [Tastatursteuerung & Interaktive Hotkeys](#-tastatursteuerung--interaktive-hotkeys)
- [Automatisierte Tests & Qualitätssicherung](#-automatisierte-tests--qualitätssicherung)
- [Projektstruktur & Weiterführende Dokumentation](#-projektstruktur--weiterführende-dokumentation)

---

## 🏛️ Systemarchitektur & Kommunikationsschichten

Das System besteht aus voneinander isolierten, autonomen .NET 10 Prozessen, die über vier orthogonale Protokolle miteinander interagieren:

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
   * Der `Simulations-Master` koordiniert die globale Simulationszeit und Schrittweite (`1s` bis `7d`).
   * Simulatoren melden sich mit stabiler Station-ID an (`ITimeMasterClient`), empfangen `TimeStep`-Nachrichten (UTF-8-JSON, zeilengetrennt) und bestätigen mit `Acknowledgement` (**Zeitschritt-Barriere**). Erst nach Eingang aller Bestätigungen schaltet der Master den Zeitschritt weiter.
   * Fällt der Master temporär aus, rechnen die Geräte mit dem letzten Zustand autark weiter und resynchronisieren sich nahtlos beim Wiederverbinden.

2. **Realdaten-Wetterdienst & HTTP-Push (`HTTP 5005` & Callbacks ab `6000` / `6100`):**
   * Der `WeatherSimulator` liest 8.784 Stunden realer Salzburger Wetterdaten (CSV) ein, wählt per Haversine-Formel den nächsten Standort, interpoliert linear zwischen Stützstellen und berechnet astronomische Sonnenstände (Azimut & Elevation).
   * PV und Wind melden sich dynamisch per Callback (`POST /api/weather/register`) an und erhalten pro Zeitschritt Wetter-Snapshots (mit automatischem lokalem Offline-Fallback bei Verbindungsverlust).

3. **Feldbusebene (Modbus TCP Ports `1502–1505`):**
   * Jeder Feldgeräte-Simulator betreibt einen Modbus TCP-Server auf einem unprivilegierten Port (1502–1505) mit standardisiertem **LB02 Holding-Register-Layout (1–12)** (Status, Fehlercode, Wirkleistung in Watt, kumulierte Energie in kWh, Zeitstempel, Sollwerte) sowie gerätespezifischen Registern (13+).

4. **Messaging & Telemetrie (RabbitMQ `5672` / Exchange `sim.exchange`):**
   * **Duale Telemetrie-Pipeline:**
     * **Nativ:** Alle Feldgeräte (`Dual-Port-Charger`, `Battery`, `PV_Simulator`, `WindTurbine_Simulator`) publizieren als native Publisher direkt auf den Topic-Exchange `sim.exchange` und abonnieren Steuerbefehle zur Fernsteuerung.
     * **Gateway:** `ModbusRabbitMqGateway` pollt parallel die Modbus-Register (1502–1505) als universelle Feldbus-Bridge.
   * `RabbitMqViewer` abonniert `sim.telemetry.#` und visualisiert alle Telemetrieströme in einem einheitlichen Live-Dashboard.

---

## 👥 Module & Verantwortlichkeiten

Die Systementwicklung wurde im Team partnerschaftlich nach klaren Modulgrenzen aufgeteilt:

| Modul / Projekt | Typ | Port / Endpunkt | Kurzbeschreibung & Kernfunktionen | Hauptverantwortung |
| :--- | :--- | :--- | :--- | :--- |
| **`TeamShared`** | Class Library | – | Gemeinsame Modbus-Wrapper (`FieldDeviceModbusServer`), `CommonRegisterMap` (1–12), Master-Protokoll (`ITimeMasterClient`) und Weather-Push-Infrastruktur (`WeatherCallbackReceiver`). | *Alexander Graf* |
| **`TeamConsoleShared`** | Class Library | – | Wiederverwendbares UI-Framework mit Spectre.Console (`SimulationTheme`, `SimulationDashboard`, `StatusBadge`, `GaugeBar`, `LiveSimulationView`). | *Alexander Graf* |
| **`TeamRabbitMQShared`** | Class Library | – | Robuste Messaging-Infrastruktur mit brokerresilientem `RabbitMqPublisher` & `RabbitMqConsumer`, DTOs (`DeviceTelemetryMessage`, `DeviceCommandMessage`) und `ModbusTelemetryMapper`. | *Alexander Graf* |
| **`Dual-Port-Charger`** | Console App | Modbus `1502`, Master-TCP `1510`, RabbitMQ | 2-Port DC-Schnellladestation (300 kW, Dynamic Load Balancing, realistische Ladekurven für Porsche Taycan, Tesla Model S/Y, VW ID.4, Störungslogik, native RabbitMQ Pub/Sub). | *Alexander Graf* |
| **`ModbusRabbitMqGateway`**| Console App | Modbus Client, RabbitMQ Pub/Sub | Modbus-Polling-Bridge für alle 4 Feldgeräte (Ports 1502–1505) mit kontinuierlicher Übersetzung in JSON-Telemetrie und Befehlsweiterleitung. | *Alexander Graf* |
| **`RabbitMqViewer`** | Console App | RabbitMQ Consumer | Interaktiver Live-Monitor mit Spectre.Console-Dashboard für Leistungs-, Energie- und Statusverläufe aller Simulatoren in Echtzeit. | *Alexander Graf* |
| **`ModbusDiagnoseClient`** | Console App | Modbus TCP Client | Interaktives Diagnose- und Testwerkzeug zum Auslesen beliebiger Modbus-Register, Schreiben von Setpoints und Quittieren von Fehlern. | *Alexander Graf* |
| **`Simulations-Master`** | Console App | TCP `1510` | Zentraler Taktgeber & Synchronisations-Engine mit Zeitschritt-Barriere, flexibler Schrittweitensteuerung (`1s` bis `7d`) und Tastatur-Steuerung. | *Nazar Burdeinyi* |
| **`Battery`** | Console App | Modbus `1503`, Master-TCP `1510`, RabbitMQ | Großbatteriespeicher (10 kWh, 3 kW Laden / 18 kW Entladen, SOC-Tracking, automatisches Tagesprofil mit Solar- und Netzladung, manuelle Laststeuerung). | *Nazar Burdeinyi* |
| **`WeatherSimulator`** | Web Minimal API | HTTP `5005`, Master-TCP `1510` | Realdaten-Wetterdienst (8.784 Stunden Salzburg CSV), Haversine-Distanz, lineare Interpolation, astronomischer Sonnenstand, HTTP REST & Snapshot-Push an PV/Wind. | *Nikolas Giesen* |
| **`PV_Simulator`** | Console App | Modbus `1504`, Master-TCP `1510`, HTTP `6000+`, RabbitMQ | Photovoltaikanlage (Solarstrahlung, Neigung, Azimut, Modul-/Wechselrichtermodell, Schneebedeckung, Wetter-Push-Integration mit lokalem Fallback). | *Nikolas Giesen* |
| **`WindTurbine_Simulator`**| Console App | Modbus `1505`, Master-TCP `1510`, HTTP `6100+`, RabbitMQ | Windkraftanlage (Vestas V90 3 MW, aerodynamisches Modell, Rotordrehzahl, Pitch-Winkel, Turbulenzmodell, Wetter-Push-Integration mit lokalem Fallback). | *Nikolas Giesen* |
| **`TeamRabbitMQShared.Tests`**| Testprojekt | – | 9 automatisierte Unit- und Integrationstests für Telemetrie-Mapping, Command-Handling, Routing-Keys und Weather-Push. | *Alexander Graf* |
| **`Dual-Port-Charger.Tests`** | Testprojekt | – | 27 automatisierte Tests für Ladekurven-Interpolation, Port-Physik, Dynamic Load Balancing und Störungsmanagement. | *Alexander Graf* |

---

## 🔌 Netzwerk-, Port- & Register-Matrix

### Port-Übersicht

| Port | Protokoll | Komponente | Beschreibung |
| :---: | :---: | :--- | :--- |
| **`1510`** | TCP (JSON Lines) | `Simulations-Master` | Globaler Zeittakt, Registrierung von `ITimeMasterClient`, Zeitschritt-Barriere |
| **`5005`** | HTTP REST | `WeatherSimulator` | Wetterdaten-Minimal-API (`GET /api/weather`, `POST /api/weather/register`) |
| **`6000+`**| HTTP POST | `PV_Simulator` | Dynamischer Callback-Port für Wetterdaten-Push |
| **`6100+`**| HTTP POST | `WindTurbine_Simulator`| Dynamischer Callback-Port für Wetterdaten-Push |
| **`1502`** | Modbus TCP | `Dual-Port-Charger` | Schnellladestation (Unit ID 1) |
| **`1503`** | Modbus TCP | `Battery` | Batteriespeicher (Unit ID 1) |
| **`1504`** | Modbus TCP | `PV_Simulator` | PV-Anlage (Unit ID 1) |
| **`1505`** | Modbus TCP | `WindTurbine_Simulator`| Windkraftanlage (Unit ID 1) |
| **`5672`** | AMQP | `RabbitMQ Broker` | Messaging Broker für Telemetrie und Befehle |
| **`15672`**| HTTP Web UI | `RabbitMQ Management` | Web-Verwaltungskonsole (Standard-Login: `guest` / `guest`) |

---

### Modbus LB02 Registerbelegung (Holding Register 1–12)

Alle Feldgeräte implementieren im Registerkopf das einheitliche **LB02 Holding-Register-Layout**:

| Register | Name | Datentyp | Einheit / Wertebereich | Beschreibung |
| :---: | :--- | :---: | :--- | :--- |
| **1** | `OperatingState` | Int16 | `0`=Off, `1`=Booting, `2`=Running, `3`=Error | Aktueller Betriebszustand des Simulators |
| **2** | `ErrorCode` | Int16 | `0`=Kein Fehler, `>0`=Fehlercode | Spezifischer Fehlercode |
| **3–4** | `ActivePower` | Float32 | Watt (High Word / Low Word) | Aktuelle Wirkleistung (Erzeugung positiv, Bezug negativ) |
| **5–6** | `CumulativeEnergy`| Float32 | kWh (High Word / Low Word) | Kumulierte Gesamtenergie seit Simulationsbeginn |
| **7–8** | `VirtualTime` | Int32 | Unix-Timestamp (Low Word / High Word) | Aktuelle virtuelle Simulationszeit |
| **9** | `OnOffState` | Int16 | `0`=Off, `1`=On | Hauptschalter des Feldgeräts |
| **10–11**| `Setpoint` | Float32 | High Word / Low Word | Leistungssollwert bzw. maximale Ladeleistung |
| **12** | `ResetError` | Int16 | `1`=Reset ausführen | Schreiben von 1 quittiert den aktuellen Fehlerzustand |

---

### Gerätespezifische Register (13+)

* **Dual-Port Charger (Port 1502):**
  * Reg 13–14: `SOC Port 1` (Float32 in %)
  * Reg 15–16: `Power Port 1` (Float32 in Watt)
  * Reg 17–18: `SOC Port 2` (Float32 in %)
  * Reg 19–20: `Power Port 2` (Float32 in Watt)
* **Battery (Port 1503):**
  * Reg 13–14: `State of Charge` (Float32 in %)
  * Reg 15–16: `Batteriespannung` (Float32 in V)
  * Reg 17–18: `Batteriestrom` (Float32 in A)
* **PV Simulator (Port 1504):**
  * Reg 13–14: `Globalstrahlung` (Float32 in W/m²)
  * Reg 15–16: `Modultemperatur` (Float32 in °C)
  * Reg 17–18: `Bewölkung` (Float32 in %)
* **Wind Turbine (Port 1505):**
  * Reg 13–14: `Windgeschwindigkeit` (Float32 in m/s)
  * Reg 15–16: `Rotordrehzahl` (Float32 in RPM)
  * Reg 17–18: `Pitch-Winkel` (Float32 in Grad)

---

### RabbitMQ Routing-Key Konventionen

* **Exchange:** `sim.exchange` (Typ: `topic`, durable)
* **Telemetrie (Publish):** `sim.telemetry.<deviceType>.<deviceId>`
  * Beispiele: `sim.telemetry.charger.1`, `sim.telemetry.battery.1`, `sim.telemetry.pv.pv-1`, `sim.telemetry.wind.wind-1`
  * Format: `DeviceTelemetryMessage` (JSON mit allgemeinen LB02-Werten und typspezifischem `DeviceSpecificData`-Dictionary)
* **Steuerbefehle (Subscribe):** `sim.commands.<deviceType>.<deviceId>`
  * Beispiele: `sim.commands.charger.1`, `sim.commands.battery.1`, `sim.commands.pv.#`, `sim.commands.wind.#`
  * Unterstützte Befehle: `SetPowerLimit` / `Setpoint`, `ResetFault`, `TriggerFault`, `ToggleCar`

---

## 🛠️ Voraussetzungen & Vorbereitung

### 1. Software-Voraussetzungen
* **[.NET 10.0 SDK](https://dotnet.microsoft.com/)** (oder neuer)
* **[Docker](https://www.docker.com/) & Docker Compose** (für den RabbitMQ-Broker)
* Ein Terminal mit Unterstützung für **UTF-8 & ANSI-Farben** (Windows Terminal, Apple Terminal, iTerm2 oder VS Code Terminal)

### 2. RabbitMQ Broker starten
Der Broker wird über das vorbereitete `docker-compose.yml` gestartet:
```bash
docker compose up -d
```
* **Broker:** `localhost:5672`
* **Management Web-UI:** `http://localhost:15672` (Benutzer: `guest`, Passwort: `guest`)

### 3. Solution kompilieren
Kompilieren Sie alle 14 Projekte der Solution:

```bash
# Unter macOS / Linux (mit IPC-Schutz und festem CLI-Home):
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1

# Unter Windows:
dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx
```

---

## 🚀 Schnellstart & Ausführung

### Option A: Automatisierter Start aller Komponenten (Empfohlen)

Das Repository enthält maßgeschneiderte Startup-Skripte für macOS, Linux und Windows. Diese bieten ein interaktives Auswahlmenü und starten alle 8 Komponenten geordnet in separaten Fenstern oder Terminal-Tabs:

#### 🍏 macOS / 🐧 Linux
```bash
# Ausführungsrechte vergeben (einmalig)
chmod +x start-all.sh scripts/start-all.sh

# Starten (öffnet separate Tabs in Apple Terminal):
./start-all.sh
```

#### 🪟 Windows (PowerShell / Windows Terminal oder CMD)
```powershell
# In PowerShell (nutzt Windows Terminal wt.exe Tabs):
.\start-all.ps1

# Oder per Doppelklick / Eingabeaufforderung:
start-all.bat
```

> [!TIP]
> **Nach dem Start aller Fenster:** Wechseln Sie in das Terminal des **`Simulations-Master`** und drücken Sie die Taste **`S`**, um die Simulation zu starten!

---

### Option B: Manuelle 4-Phasen-Inbetriebnahme

Wenn Sie die Komponenten einzeln in separaten Terminal-Fenstern starten möchten, halten Sie sich an die verbindliche 4-Phasen-Reihenfolge:

```
[Phase 1: Infrastruktur & Takt]  -->  RabbitMQ (Docker) + Simulations-Master (TCP 1510)
                                             │
[Phase 2: Wetterprovider]        -->  WeatherSimulator (HTTP 5005)
                                             │
[Phase 3: Feldgeräte]            -->  Charger (1502), Battery (1503), PV (1504), Wind (1505)
                                             │
[Phase 4: Gateway & Monitoring]  -->  ModbusRabbitMqGateway + RabbitMqViewer
```

#### Phase 1: Infrastruktur & Zeitgeber
```bash
# Terminal 1: Simulations-Master
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Simulations-Master/Simulations-Master.csproj
```
*(Startet pausiert auf TCP-Port 1510)*

#### Phase 2: Wetterdienst
```bash
# Terminal 2: WeatherSimulator
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project WeatherSimulator/WeatherSimulator.csproj
```
*(Verbindet sich mit dem Master und aktiviert die Minimal API auf HTTP-Port 5005)*

#### Phase 3: Feldgeräte
```bash
# Terminal 3: Dual-Port-Charger (Modbus 1502)
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Dual-Port-Charger/Dual-Port-Charger.csproj

# Terminal 4: Battery-Simulator (Modbus 1503)
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Battery/Battery.csproj

# Terminal 5: PV-Simulator (Modbus 1504)
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj
# (Eingabeaufforderung mit '1' und Enter für Standardwerte bestätigen)

# Terminal 6: WindTurbine-Simulator (Modbus 1505)
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project WindTurbine_Simulator/WindTurbine_Simulator.csproj
# (Eingabeaufforderung mit Enter für Standard-Physik und Port 1505 bestätigen)
```

#### Phase 4: Gateway & Live-Monitoring
```bash
# Terminal 7: Modbus-RabbitMQ Gateway
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj

# Terminal 8: RabbitMQ Live-Viewer
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
```

#### Phase 5: Startschuss!
* Wechseln Sie in **Terminal 1 (`Simulations-Master`)** und drücken Sie **`S`**.
* Alle Simulatoren beginnen synchron zu laufen, pushen Telemetriedaten und der `RabbitMqViewer` zeigt die Live-Leistungskurven an.

---

## 🎮 Tastatursteuerung & Interaktive Hotkeys

Alle Konsolenanwendungen sind mit dem einheitlichen `TeamConsoleShared` Theme gestaltet und lassen sich interaktiv steuern:

| Modul | Hotkeys | Aktion |
| :--- | :--- | :--- |
| **Simulations-Master** | `S` | Simulation starten |
| | `P` | Simulation pausieren |
| | `R` | Simulation fortsetzen (Resume) |
| | `+` / `-` | Schrittweite anpassen (`1s`, `10s`, `1m`, `15m`, `1h`, `2h`, `12h`, `24h`, `7d`) |
| | `Q` | Master beenden |
| **Dual-Port-Charger** | `A` / `M` | Zwischen Automatik- und Manuell-Modus umschalten |
| | `1` / `2` | Auto an Ladepunkt 1 bzw. 2 verbinden / trennen |
| | `↑` / `↓` | Ladeleistung im Manuell-Modus in 5-kW-Schritten anpassen |
| | `F` / `R` | Störung simulieren (`Fault`) / Störung beheben (`Reset`) |
| **Battery** | `A` / `M` | Automatisches Tagesprofil / Manuelle Laststeuerung aktivieren |
| | `D` / `C` | Entlade-Verbraucher anstecken / Manuelle Ladevorgabe setzen |
| | `+` / `-` | Lade-/Entladeleistung um 1 kW erhöhen/verringern |
| | `F` / `R` | Batteriespeicher-Fehler simulieren / zurücksetzen |
| **PV Simulator** | `P` / `A` | Pause umschalten / Wetter-Automatik aktivieren |
| | `F` / `S` / `R`| Wechselrichter-Ausfall / Schnee-Bedeckung / Regen ein- bzw. ausschalten |
| | `C` / `T` | Wolkenbedeckung bzw. Temperatur auswählen (danach mit `+`/`-` justieren) |
| **ModbusRabbitMqGateway**| `P` | Modbus-Polling pausieren / fortsetzen |
| | `Q` / `X` | Gateway beenden |
| **RabbitMqViewer** | `C` | Ereignis- und Telemetrie-Log leeren |
| | `Q` | Viewer beenden |
| **WeatherSimulator** | `R` / `Q` | Dashboard manuell aktualisieren / Dienst beenden |

---

## 🧪 Automatisierte Tests & Qualitätssicherung

Das Repository verfügt über zwei dedizierte Test-Suites mit insgesamt **36 automatisierten Tests**:

### 1. Dual-Port-Charger Test-Suite (27 Tests)
Überprüft Ladekurven aller Fahrzeugmodelle, lineare Interpolation, Port-Verhalten, Störungsmanagement und Dynamic Load Balancing:
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Dual-Port-Charger.Tests/Dual-Port-Charger.Tests.csproj
```
```text
=================================================================
       Dual-Port-Charger xUnit Test Suite (Team Echo)            
=================================================================
[1] Fahrzeugmodelle & Ladekurven (EVModel / EVModelCatalog):      7/7  PASSED ✅
[2] Ladephysik & Port-Logik (ChargingPoint):                      9/9  PASSED ✅
[3] Dynamisches Lastmanagement & Station (Device):               11/11 PASSED ✅
=================================================================
  Ergebnis: Alle 27/27 Tests erfolgreich bestanden! 🎉
=================================================================
```

### 2. RabbitMQ & Weather Integrations-Tests (9 Tests)
Überprüft DTO-Serialisierung, Routing-Key-Generatoren, Register-Mappings von Modbus zu RabbitMQ für PV & Wind sowie HTTP-Callback-Wetter-Push:
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project TeamRabbitMQShared.Tests/TeamRabbitMQShared.Tests.csproj
```
```text
=== Running TeamRabbitMQShared & Weather Integration Tests ===
[TEST] DeviceTelemetryMessage_SerializationAndDeserialization... PASSED ✅
[TEST] DeviceCommandMessage_FactoryMethods...                   PASSED ✅
[TEST] RabbitMqConfiguration_RoutingKeyBuilders...              PASSED ✅
[TEST] ModbusTelemetryMapper_FromRegisters...                   PASSED ✅
[TEST] ModbusTelemetryMapper_FromRegisters_MapsPvSpecific...    PASSED ✅
[TEST] ModbusTelemetryMapper_FromRegisters_MapsWindSpecific...  PASSED ✅
[TEST] WeatherSnapshotDto_SerializationAndDeserialization...    PASSED ✅
[TEST] DeviceWeatherRegistration_SerializationAndDeserial...    PASSED ✅
[TEST] WeatherCallbackReceiver_ReceivesAndMatchesSnapshot...     PASSED ✅

All 9/9 Unit & Integration Tests Passed Successfully! 🎉
```

---

## 📚 Projektstruktur & Weiterführende Dokumentation

```text
.
├── dotNET_Programmierung_2026_Aufgabe_02.slnx  # .NET 10 Solution mit allen 14 Projekten
├── docker-compose.yml                          # RabbitMQ Broker Setup (Ports 5672, 15672)
├── nuget.config                                # Vorkonfigurierte deterministische Package-Quellen
├── AGENTS.md                                   # Onboarding- & Entwickler-Richtlinien für LLM/Entwickler
├── PROJECT_CONTEXT.md                          # Umfassender Architektur- & Protokoll-Kontext
│
├── start-all.sh / start-all.bat / start-all.ps1 # Schnellstarter im Root
├── scripts/                                    # Automatisierte Start- & Orchestrierungsskripte
│
├── Simulations-Master/                         # Zentraler Zeittaktgeber (TCP 1510)
├── WeatherSimulator/                           # Realdaten-Wetterdienst & HTTP-Push (HTTP 5005)
│
├── Dual-Port-Charger/                          # DC-Schnellladestation (Modbus 1502)
├── Dual-Port-Charger.Tests/                    # 27 Unit- & Integrationstests für den Charger
├── Battery/                                    # Großbatteriespeicher (Modbus 1503)
├── PV_Simulator/                               # Photovoltaikanlage (Modbus 1504)
├── WindTurbine_Simulator/                      # Windkraftanlage Vestas V90 (Modbus 1505)
│
├── ModbusRabbitMqGateway/                      # Feldbus-Bridge (Modbus Polling <-> RabbitMQ)
├── RabbitMqViewer/                             # Spectre.Console Live-Monitoring Dashboard
├── ModbusDiagnoseClient/                       # Interaktives Modbus-Inspektions-Tool
│
├── TeamShared/                                 # Gemeinsame Modbus- & Master-Protokoll-Bibliothek
├── TeamConsoleShared/                          # Spectre.Console UI-Komponenten & Themes
├── TeamRabbitMQShared/                         # RabbitMQ Publisher, Consumer, DTOs & Mappings
├── TeamRabbitMQShared.Tests/                   # 9 Tests für Messaging & Wetter-Integration
│
└── docs/                                       # Detaillierte Dokumentation & Tutorials
    ├── README.md                               # Zentrale Dokumentationsübersicht
    ├── tutorial-dotnet-applications.md         # Schritt-für-Schritt Ausführungsanleitung
    ├── tutorial-rabbitmq-docker.md             # RabbitMQ Docker & .env Einrichtungsanleitung
    └── development-journal/                    # Studentische Entwicklungsjournale
        ├── Alexander-Graf.md
        ├── Nikolas-Giesen.md
        └── Nazar-Burdeinyi.md
```

### Praxis-Tutorials & Spezifikationen:
* 📖 **[Schritt-für-Schritt Anleitung zur Ausführung](docs/tutorial-dotnet-applications.md)**: Vollständiger Leitfaden zur Multi-Terminal-Ausführung, Fehlerbehebung und Portmatrix.
* 🐳 **[RabbitMQ mit Docker einrichten](docs/tutorial-rabbitmq-docker.md)**: Konfiguration von RabbitMQ über Docker Compose, `.env` und Management-UI.
* 🤖 **[Entwickler- & Agentenrichtlinien (AGENTS.md)](AGENTS.md)**: Architekturstandards, LB02-Holding-Register, Skills und Coding-Vorschriften.
* 📐 **[Master-Client Protokollspezifikation](TeamShared/SimulationMaster-Client-Protocol.md)**: Detaillierte JSON-Nachrichtendefinition für Zeitschritt-Barriere und Synchronisation.

---

*Energy Twin 2026 – Team Echo / Team 01 – FH Salzburg 2026*
