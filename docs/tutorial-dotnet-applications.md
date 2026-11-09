# 🚀 Tutorial: Ausführen der .NET Applikationen (Energy Twin 2026)

Dieses Tutorial führt Sie durch die Vorbereitung, geordnete Inbetriebnahme und Steuerung der verteilten .NET 10 Applikationen des Systems **Energy Twin 2026**.

---

## 1. Systemübersicht & Komponenten

Das Gesamtsystem besteht aus mehreren eigenständigen Konsolen- und Webanwendungen, die über **Master-TCP (1510)**, **Modbus TCP (1502–1505)**, **HTTP REST / Callbacks (5005, 6000+)** und **RabbitMQ (5672)** miteinander interagieren:

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

### Module im Überblick:

| Modul | Typ | Ports / Schnittstellen | Funktion |
| :--- | :--- | :--- | :--- |
| [`Simulations-Master`](file:///Users/alex/Github/dotnet-simulator-team-echo/Simulations-Master/Program.cs) | Console | TCP `1510` | Zentraler Taktgeber; koordiniert virtuelle Zeitschritte über TCP-JSON-Protokoll mit ACK-Barriere. |
| [`WeatherSimulator`](file:///Users/alex/Github/dotnet-simulator-team-echo/WeatherSimulator/Program.cs) | Web Minimal API | HTTP `5005`, Master-TCP `1510` | Liest reale Salzburger Wetterdaten aus CSV; pusht Wetter-Snapshots an PV und Wind. |
| [`Dual-Port-Charger`](file:///Users/alex/Github/dotnet-simulator-team-echo/Dual-Port-Charger/Program.cs) | Console | Modbus TCP `1502`, Master-TCP `1510` | 2-Port DC-Schnellladestation für Elektroautos mit Lastmanagement und Störungslogik. |
| [`Battery`](file:///Users/alex/Github/dotnet-simulator-team-echo/Battery/Program.cs) | Console | Modbus TCP `1503`, Master-TCP `1510` | Großbatteriespeicher (Lade-/Entladezyklen, SOC-Tracking). |
| [`PV_Simulator`](file:///Users/alex/Github/dotnet-simulator-team-echo/PV_Simulator/PV_Simulator/Program.cs) | Console | Modbus TCP `1504`, Master-TCP `1510`, HTTP ab `6000` | Photovoltaikanlage; berechnet Sonnenstand, Strahlung und Solarstrom. |
| [`WindTurbine_Simulator`](file:///Users/alex/Github/dotnet-simulator-team-echo/WindTurbine_Simulator/Program.cs) | Console | Modbus TCP `1505`, Master-TCP `1510`, HTTP ab `6100` | Windkraftanlage (Vestas V90 Modell) mit Turbulenzberechnung. |
| [`ModbusRabbitMqGateway`](file:///Users/alex/Github/dotnet-simulator-team-echo/ModbusRabbitMqGateway/Program.cs) | Console | Modbus Client, RabbitMQ Publisher & Consumer | Pollt Modbus-Register (1502–1505) periodisch und publiziert standardisierte JSON-Telemetrie. |
| [`RabbitMqViewer`](file:///Users/alex/Github/dotnet-simulator-team-echo/RabbitMqViewer/Program.cs) | Console | RabbitMQ Consumer | Interaktives Live-Dashboard zur Visualisierung aller Telemetrieströme. |
| [`ModbusDiagnoseClient`](file:///Users/alex/Github/dotnet-simulator-team-echo/ModbusDiagnoseClient/Program.cs) | Console | Modbus TCP Client | Interaktives Tool zur manuellen Inspektion und Manipulation von Modbus-Registern. |

---

## 2. Voraussetzungen & Vorbereitung

### 2.1 Software-Voraussetzungen
* **.NET 10.0 SDK:** Überprüfen mit:
  ```bash
  dotnet --version
  ```
  *(Hinweis auf macOS: Befindet sich standardmäßig unter `/usr/local/share/dotnet/dotnet`)*.
* **RabbitMQ Broker:** Stellen Sie sicher, dass RabbitMQ via Docker läuft (siehe [RabbitMQ Docker Tutorial](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-rabbitmq-docker.md)):
  ```bash
  docker compose up -d
  ```

### 2.2 Kompilieren der gesamten Solution
Vor dem ersten Start sollten alle Projekte gebaut werden. 

> [!IMPORTANT]
> **macOS IPC-Vorschrift:** Verwenden Sie unter macOS in Sandboxes oder Umgebungen mit Dateibeschränkungen immer `-m:1` (Parallelität deaktivieren) und setzen Sie `DOTNET_CLI_HOME`:

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
```

---

## 3. Die verbindliche Startreihenfolge

Da die Simulatoren voneinander abhängen (z. B. müssen Taktgeber und Wetterdienst vor den Feldgeräten laufen), empfiehlt sich die folgende **4-Phasen-Startreihenfolge**:

```
[Phase 1: Infrastruktur & Takt]  -->  RabbitMQ (Docker) + Simulations-Master (TCP 1510)
                                            │
[Phase 2: Wetterprovider]        -->  WeatherSimulator (HTTP 5005)
                                            │
[Phase 3: Feldgeräte]            -->  Charger (1502), Battery (1503), PV (1504), Wind (1505)
                                            │
[Phase 4: Gateway & Monitoring]  -->  ModbusRabbitMqGateway + RabbitMqViewer
```

---

## 4. Schritt-für-Schritt Startanleitung

Öffnen Sie separate Terminal-Fenster bzw. -Reiter für jede Applikation und führen Sie die Befehle im Hauptverzeichnis des Repositories aus.

### Schritt 1: Simulations-Master starten (Terminal 1)

Der Master steuert die gemeinsame virtuelle Zeit und den Schritt-Takt.

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Simulations-Master/Simulations-Master.csproj
```

* **Standard-Port:** `1510`
* **Zustand nach Start:** Simulation startet zunächst im Zustand **PAUSED** (Pausiert).
* **Wichtige Hotkeys im Master-Fenster:**
  * `S`: Simulation starten
  * `P`: Simulation pausieren
  * `R`: Simulation fortsetzen (Resume)
  * `+` / `-`: Simulations-Schrittweite erhöhen/verringern (`1s`, `10s`, `1m`, `15m`, `1h`, `2h`, `12h`, `24h`, `7d`)
  * `Q`: Master beenden

---

### Schritt 2: WeatherSimulator starten (Terminal 2)

Der Wetterdienst verbindet sich mit dem Master und stellt eine HTTP-API sowie Push-Benachrichtigungen bereit.

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project WeatherSimulator/WeatherSimulator.csproj
```

* **HTTP-Endpunkt:** `http://localhost:5005`
* **Verhalten:** Wartet kurz auf die erste Zeitsynchronisation mit dem Master und schaltet danach die Minimal API frei.

---

### Schritt 3: Feldgeräte starten (Terminals 3 bis 6)

#### Terminal 3: Dual-Port Charger (E-Auto Ladestation)
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Dual-Port-Charger/Dual-Port-Charger.csproj
```
* **Modbus TCP:** Port `1502`, Unit ID `1`
* **Interaktive Tastenbelegung:**
  * `A`: Automatikmodus aktivieren
  * `M`: Manuellen Modus aktivieren
  * `1` / `2`: Fahrzeug an Ladepunkt 1 bzw. 2 an-/abstecken
  * `Pfeil nach oben / unten`: Ladeleistung im manuellen Modus anpassen
  * `F`: Störung simulieren (Trigger Fault)
  * `R`: Störung zurücksetzen (Reset Fault)

#### Terminal 4: Battery Simulator (Großspeicher)
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Battery/Battery.csproj
```
* **Modbus TCP:** Port `1503`, Unit ID `1`
* Startet direkt und zeigt den Lade-/Entladestatus sowie SOC in einer interaktiven Spectre.Console-Tabelle an.

#### Terminal 5: PV Simulator (Photovoltaik)
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj
```
* **Modbus TCP:** Port `1504`, Unit ID `1`
* **Callback-Port:** Ab `6000`
* **Interaktive Startabfrage:**
  * Drücken Sie `1` (oder einfach `Enter`), um die Standard-Konfiguration zu übernehmen.
  * Bestätigen Sie die abgefragten Nennleistungen (oder tippen Sie z. B. `10` für 10 kWp ein).

#### Terminal 6: WindTurbine Simulator (Windkraftanlage)
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project WindTurbine_Simulator/WindTurbine_Simulator.csproj
```
* **Modbus TCP:** Port `1505`, Unit ID `1`
* **Callback-Port:** Ab `6100`
* **Interaktive Startabfrage:**
  * Es erscheint: `Standardwerte für Physik und Zeit verwenden (j/n)? [j]:`
  * Bestätigen Sie mit `Enter` (bzw. `j`), um Standardwerte für Rotor und Nennleistung (3 MW) zu nutzen.
  * Bestätigen Sie Port `1505` und Unit ID `1` mit `Enter`.

---

### Schritt 4: Modbus-RabbitMQ Gateway starten (Terminal 7)

Das Gateway verbindet die Feldbusebene mit RabbitMQ. Es pollt alle vier Simulatoren und publiziert deren Zustand als JSON auf `sim.exchange`.

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj
```

* **Tastatursteuerung:**
  * `P`: Polling pausieren / fortsetzen
  * `Q` oder `X`: Gateway beenden

---

### Schritt 5: RabbitMQ Live-Viewer starten (Terminal 8)

Der Viewer abonniert alle Telemetrie-Ereignisse (`sim.telemetry.#`) und visualisiert die Leistungskurven aller Feldgeräte in Echtzeit.

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
```

* **Tastatursteuerung:**
  * `C`: Ereignis-Log leeren
  * `Q`: Viewer beenden

---

### Schritt 6: Simulation im Master starten!

Wechseln Sie zurück in **Terminal 1** ([`Simulations-Master`](file:///Users/alex/Github/dotnet-simulator-team-echo/Simulations-Master/Program.cs)) und drücken Sie die Taste **`S`**.
* Die Simulation beginnt zu laufen.
* Alle angemeldeten Simulatoren empfangen Zeitschritte, berechnen Physik und Wetterwerte, aktualisieren ihre Modbus-Register und quittieren (ACK).
* Das Gateway pollt die Register und übermittelt Daten an RabbitMQ.
* Der Viewer zeigt die Live-Werte grafisch an!

---

## 5. Optional: Modbus Diagnose Client verwenden

Zum Testen oder gezielten Manipulieren einzelner Modbus-Register (z. B. Setpoints schreiben oder Störungen über Register 12 zurücksetzen):

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusDiagnoseClient/ModbusDiagnoseClient.csproj
```

Sie können interaktiv wählen, welches Gerät angesprochen werden soll:
1. `1` = Dual-Port Charger (`localhost:1502`)
2. `2` = Battery (`localhost:1503`)
3. `3` = PV Simulator (`localhost:1504`)
4. `4` = WindTurbine Simulator (`localhost:1505`)

---

## 6. Komfort-Option: Automatisierter Start aller Komponenten

Um nicht jedes Mal 8 Konsolenfenster von Hand zu öffnen, können Sie das bereitgestellte Startskript [`scripts/start-all.sh`](file:///Users/alex/Github/dotnet-simulator-team-echo/scripts/start-all.sh) verwenden:

```bash
chmod +x scripts/start-all.sh
./scripts/start-all.sh
```

Auf **macOS** öffnet dieses Skript automatisch separate Tabs in der Terminal-App für jeden Simulator.

---

## 7. Fehlerbehebung & Häufige Fragen (FAQ)

### Q: Ein Simulator meldet "Connection refused" zum Master (Port 1510)
* **Antwort:** Der [`Simulations-Master`](file:///Users/alex/Github/dotnet-simulator-team-echo/Simulations-Master/Program.cs) muss vor den Simulatoren gestartet werden. Alle Simulatoren verfügen jedoch über eine automatische Reconnect-Schleife und verbinden sich selbstständig, sobald der Master online geht.

### Q: Port-Konflikt: "Address already in use"
* **Antwort:** Ein früherer Prozess läuft möglicherweise noch im Hintergrund.
* Prüfen und beenden Sie verwaiste Prozesse (z. B. auf Port 1502 oder 1510):
  ```bash
  lsof -i :1510
  kill -9 <PID>
  ```

### Q: PV oder Wind melden keine neuen Wetterdaten
* **Antwort:** Prüfen Sie, ob der [`WeatherSimulator`](file:///Users/alex/Github/dotnet-simulator-team-echo/WeatherSimulator/Program.cs) läuft und im Master-Fenster registriert ist. Sollte der Wetterdienst offline sein, greifen PV und Wind automatisch auf interne Modell-Berechnungen (Offline-Fallback) zurück.
