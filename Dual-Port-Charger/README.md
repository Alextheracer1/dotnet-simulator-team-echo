# ⚡ E-Auto-Ladesäule Simulator (Dual-Port CCS)

Digitaler Zwilling einer modernen 300-kW-DC-Schnellladestation mit zwei unabhängigen Ladepunkten, dynamischem Lastmanagement (Load Balancing), realistischen Fahrzeugladekurven, Modbus-TCP-Feldbus (Port 1502), nativer RabbitMQ-Telemetrie/Befehlsanbindung und Master-Zeitsynchronisation (Port 1510).

Entwickelt von **Alexander Graf** im Rahmen der Lehrveranstaltung *.NET Programmierung für industrienahe Anwendungen* (Leistungsbeurteilungen LB01–LB04, Team Echo).

---

## 📑 Inhaltsverzeichnis

- [Architektur & Schnittstellen](#-architektur--schnittstellen)
- [Docker-Containerisierung (Aufgabe 04 / Schritt 5)](#-docker-containerisierung-aufgabe-04--schritt-5)
  - [Build-Anleitung](#build-anleitung)
  - [Starten mit Docker Compose (Empfohlen)](#starten-mit-docker-compose-empfohlen)
  - [Starten als Standalone-Container (`docker run`)](#starten-als-standalone-container-docker-run)
  - [Umgebungsvariablen](#umgebungsvariablen)
  - [Status, Logs & Überwachung](#status-logs--überwachung)
  - [Kontrolliertes Beenden](#kontrolliertes-beenden)
- [Bedienung & Betriebsmodi](#-bedienung--betriebsmodi)
  - [Auto-Modus (Container-Standard)](#auto-modus-container-standard)
  - [Fernsteuerung über RabbitMQ (Commands)](#fernsteuerung-über-rabbitmq-commands)
  - [Fernsteuerung über Modbus TCP](#fernsteuerung-über-modbus-tcp)
  - [Lokale Tastatursteuerung](#lokale-tastatursteuerung)
- [Modbus Registerbelegung (LB02 Standard)](#-modbus-registerbelegung-lb02-standard)
- [Modellbeschreibung & Physik](#-modellbeschreibung--physik)
- [Automatisierte Tests (xUnit)](#-automatisierte-tests-xunit)

---

## 🏛️ Architektur & Schnittstellen

Der `Dual-Port-Charger` verbindet sich nahtlos mit allen Kommunikationsschichten des verteilten Gesamtsystems:

| Schnittstelle | Protokoll / Port | Richtung | Funktion |
| :--- | :--- | :--- | :--- |
| **Modbus TCP Server** | Port `1502` (Unit ID 1) | Eingehend (Server) | Bereitstellung der LB02-Register (1–12) sowie der gerätespezifischen Register (13–16: SOC Ladepunkt 1 & 2) für Polling-Clients und Gateways. |
| **Simulations-Master** | TCP `1510` (JSON Lines) | Ausgehend (Client) | Registrierung als `charger-1`, Empfang virtueller Zeitschritte (`TimeStep`), Zeitschritt-Barriere via `Acknowledgement` und nahtloser lokaler Fallback bei Verbindungsverlust. |
| **RabbitMQ Publisher** | AMQP `5672` (`sim.exchange`) | Ausgehend (Publisher) | Publiziert periodisch Zustands- und Telemetriedaten auf `sim.telemetry.charger.1`. |
| **RabbitMQ Consumer** | AMQP `5672` (`sim.exchange`) | Eingehend (Consumer) | Abonniert Steuerbefehle auf `sim.commands.charger.#` (`SetPowerLimit`, `ResetFault`, `ToggleCar`, `SetMode`). |

---

## 🐳 Docker-Containerisierung (Aufgabe 04 / Schritt 5)

Das Projekt verfügt über ein versioniertes Multi-Stage Dockerfile ([Dual-Port-Charger/Dockerfile](file:///Users/alex/Github/dotnet-simulator-team-echo/Dual-Port-Charger/Dockerfile)) basierend auf offiziellen Microsoft .NET 10 Images:
* **Build-Stage:** `mcr.microsoft.com/dotnet/sdk:10.0` (kompiliert `Dual-Port-Charger` und die Bibliotheken `TeamShared`, `TeamConsoleShared`, `TeamRabbitMQShared`).
* **Runtime-Stage:** `mcr.microsoft.com/dotnet/runtime:10.0` (schlankes, gehärtetes Runtime-Image).

### Build-Anleitung

Da das Projekt referenzierte Team-Bibliotheken nutzt, wird der Build aus dem Hauptverzeichnis des Repositories ausgeführt:

```bash
# Über Docker Compose:
docker compose build dual-port-charger

# Oder direkt via Docker CLI:
docker build -f Dual-Port-Charger/Dockerfile -t dual-port-charger:latest .
```

### Starten mit Docker Compose (Empfohlen)

In der zentralen [docker-compose.yml](file:///Users/alex/Github/dotnet-simulator-team-echo/docker-compose.yml) ist der Dienst `dual-port-charger` vorkonfiguriert und startet automatisch zusammen mit dem RabbitMQ-Broker:

```bash
# Gesamtes System inklusive Broker und Charger im Hintergrund starten:
docker compose up -d

# Nur den Dual-Port-Charger starten (RabbitMQ startet als Abhängigkeit mit):
docker compose up -d dual-port-charger
```

### Starten als Standalone-Container (`docker run`)

```bash
docker run -d \
  --name energy-twin-dual-port-charger \
  -p 1502:1502 \
  -e RABBITMQ_HOST=host.docker.internal \
  -e RABBITMQ_PORT=5672 \
  -e SIMULATION_MASTER_HOST=host.docker.internal \
  -e SIMULATION_MASTER_PORT=1510 \
  -v "$(pwd)/logs/charger:/app/logs" \
  dual-port-charger:latest
```

### Umgebungsvariablen

| Variable | Standardwert | Beschreibung |
| :--- | :--- | :--- |
| `RABBITMQ_HOST` | `rabbitmq` (Compose) / `localhost` | Hostname oder IP des RabbitMQ-Brokers |
| `RABBITMQ_PORT` | `5672` | AMQP-Port des Brokers |
| `RABBITMQ_USER` | `guest` | Benutzername für RabbitMQ |
| `RABBITMQ_PASS` | `guest` | Passwort für RabbitMQ |
| `SIMULATION_MASTER_HOST` | `host.docker.internal` | Hostname/IP des zentralen `Simulations-Master` |
| `SIMULATION_MASTER_PORT` | `1510` | TCP-Port des `Simulations-Master` |

### Status, Logs & Überwachung

1. **Konsolenausgabe des Containers verfolgen:**
   ```bash
   docker logs -f energy-twin-dual-port-charger
   ```
2. **Dateibasierte CSV-Logs:**
   Über das gemountete Volume `./logs/charger:/app/logs` wird die CSV-Datei `simulation.csv` auf dem Host-System abgelegt:
   ```bash
   head -n 20 logs/charger/simulation.csv
   ```
3. **Live-Telemetrie im Viewer:**
   Der `RabbitMqViewer` empfängt die Telemetrie des Containers live über den RabbitMQ-Broker:
   ```bash
   dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
   ```
4. **Modbus TCP Abfrage:**
   Mit dem `ModbusDiagnoseClient` kann der Modbus-Server des laufenden Containers auf Port 1502 abgefragt werden:
   ```bash
   dotnet run --project ModbusDiagnoseClient/ModbusDiagnoseClient.csproj
   ```

### Kontrolliertes Beenden

```bash
# Container stoppen:
docker compose stop dual-port-charger

# Container und Netzwerke sauber herunterfahren:
docker compose down
```

---

## 🎮 Bedienung & Betriebsmodi

### Auto-Modus (Container-Standard)
Beim Start im Container befindet sich der Simulator standardmäßig im **Auto-Modus** (`AutoMode = true`). 
* E-Autos verschiedener Fahrzeugtypen kommen mit zufälligem Akkustand (5–20% SOC) an, fordern Leistung gemäß ihrer fahrzeugspezifischen Ladekurve an und fahren nach Erreichen des Ziel-SOC wieder ab.
* Das dynamische Lastmanagement regelt die Leistungsaufteilung autonom.

### Fernsteuerung über RabbitMQ (Commands)
Steuerbefehle können asynchron auf das Topic `sim.commands.charger.1` gesendet werden:
* **`SetPowerLimit` / `Setpoint`:** Drosselung des Netzanschlusslimits der Ladesäule (z. B. auf 150.000 Watt).
* **`ToggleCar`:** Simuliert das An-/Abstecken an Ladepunkt 1 oder 2 (Wert: `1` oder `2`).
* **`TriggerFault`:** Versetzt die Station in Störung (Register 2 = 1, Wirkleistung = 0 kW).
* **`ResetFault`:** Quittiert eine Störung und nimmt den Normalbetrieb wieder auf.
* **`SetMode`:** Wechsel zwischen Auto-Modus (`1`) und manuellem Modus (`0`).

### Fernsteuerung über Modbus TCP
Über einen Modbus-Client (z. B. `ModbusDiagnoseClient`) können folgende Steuerregister beschrieben werden:
* **Register 10–11 (Setpoint, Float32):** Begrenzung der Stationsleistung in Watt.
* **Register 12 (ResetError, Int16):** Schreiben von `1` setzt einen aktiven Fehlerzustand zurück.

### Lokale Tastatursteuerung (bei interaktiver Ausführung)
Wird der Simulator direkt auf der Konsole oder mit `docker run -it` betrieben, stehen folgende Tasten bereit:
* `[A]` / `[M]` – Auto-Modus bzw. Manuell-Modus aktivieren.
* `[1]` / `[2]` – Fahrzeug an Ladepunkt 1 bzw. 2 manuell verbinden/trennen.
* `[↑]` / `[↓]` – Manuelle Stationsleistung in 5-kW-Schritten anpassen.
* `[F]` / `[R]` – Störung auslösen bzw. zurücksetzen.
* `[+]` / `[-]` – Simulationsgeschwindigkeit im Fallback-Modus anpassen.
* `[P]` – Pause / Fortsetzen (im Fallback-Modus; bei Master-Verbindung steuert der Master den Takt).
* `[X]` – Simulator beenden.

---

## 📋 Modbus Registerbelegung (LB02 Standard)

| Register | Name | Typ | Einheit / Werte | Beschreibung |
| :---: | :--- | :--- | :--- | :--- |
| **1** | `OperatingState` | Int16 | 0=Off, 1=Boot, 2=Run, 3=Error | Betriebszustand |
| **2** | `ErrorCode` | Int16 | 0=Kein Fehler, 1=Störung | Fehlercode |
| **3–4** | `ActivePower` | Float32 | Watt (High/Low Word) | Aktuelle Gesamtwirkleistung |
| **5–6** | `CumulativeEnergy` | Float32 | kWh (High/Low Word) | Bisher abgegebene Gesamtenergie |
| **7–8** | `VirtualTime` | Int32 | Unix-Timestamp (Low/High Word) | Aktuelle virtuelle Zeit |
| **9** | `OnOffState` | Int16 | 1=Ein, 0=Aus | Schalterzustand |
| **10–11** | `Setpoint` | Float32 | Watt (High/Low Word) | Leistungssollwert / Begrenzung |
| **12** | `ResetError` | Int16 | 1=Reset ausführen | Fehlerquittierung |
| **13–14** | `SOC_Port1` | Float32 | 0.0–1.0 (High/Low Word) | State of Charge Ladepunkt 1 |
| **15–16** | `SOC_Port2` | Float32 | 0.0–1.0 (High/Low Word) | State of Charge Ladepunkt 2 |

---

## 🚗 Modellbeschreibung & Physik

* **Maximale Anschlussleistung:** 300 kW.
* **Dynamisches Lastmanagement:** Bei zwei gleichzeitig ladenden Fahrzeugen teilt die Station die Leistung fair auf (bis zu 150 kW pro Ladepunkt). Benötigt ein Fahrzeug weniger Leistung, steht der Überschuss dem anderen Punkt zur Verfügung.
* **Fahrzeugkatalog:**
  * **Porsche Taycan:** 93,4 kWh, 800V-System, langes 270 kW Plateau.
  * **Tesla Model Y LR:** 78 kWh, bis zu 250 kW Peak.
  * **Tesla Model S 100D:** 100 kWh, bis zu 142 kW.
  * **VW ID.4 Pro:** 77 kWh, bis zu 135 kW Peak.

---

## 🧪 Automatisierte Tests (xUnit)

Die Testsuite [Dual-Port-Charger.Tests](file:///Users/alex/Github/dotnet-simulator-team-echo/Dual-Port-Charger.Tests/) sichert das gesamte physikalische und mathematische Verhalten ab (27 automatisierte Tests mit 100% Methodenabdeckung):

```bash
# Tests ausführen:
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Dual-Port-Charger.Tests/Dual-Port-Charger.Tests.csproj --no-build
```
