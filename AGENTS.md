# Agent Guidelines & Development Standards: Energy Twin 2026 (Team Echo)

Dieses Dokument definiert verbindliche Richtlinien, Architekturstandards und Vorgehensweisen für Entwickler und autonome KI-Agenten im Repository `dotnet-simulator-team-echo`.

---

## 1. Projektübersicht & Kontext

Das Projekt **Energy Twin 2026** ist ein verteiltes Simulationssystem zur Echtzeitsimulation erneuerbarer Energien, Speichersysteme und Ladeinfrastrukturen.
* **Architektur:** Verteilte .NET 10 Konsolenapplikationen, die als isolierte Prozesse laufen.
* **Kommunikationsschichten:**
  1. **Virtuelle Zeit & Takt:** Zentral gesteuert über den `Simulations-Master`.
  2. **Feldbusebene (Modbus TCP):** Jeder Feldgeräte-Simulator betreibt einen Modbus TCP-Server auf einem unprivilegierten Port (1502–1510) mit einheitlichem Register-Layout.
  3. **Messagingebene (RabbitMQ):** Asynchrone Verteilung von Sensordaten und Telemetrie über Topic-Exchanges (`sim.exchange`) sowie Empfang von Steuerbefehlen/Sollwerten.

---

## 2. Projekt- und Komponentenstruktur

| Projekt | Typ | Beschreibung & Port |
| :--- | :--- | :--- |
| `TeamShared` | Class Library | Modbus-Wrapper (`FieldDeviceModbusServer`), Register-Mapping (`CommonRegisterMap`), Simulationsprotokoll |
| `TeamConsoleShared` | Class Library | Spectre.Console UI-Komponenten (`SimulationDashboard`, `StatusBadge`, `GaugeBar`, `LiveSimulationView`) |
| `TeamRabbitMQShared` | Class Library | Wiederverwendbare Bibliothek für RabbitMQ Publisher, Consumer, Telemetrie- und Command-DTOs |
| `Dual-Port-Charger` | Console Executable | Feldgerät: DC-Schnellladesäule mit 2 Ladepunkten (Modbus Port 1502, RabbitMQ Publisher & Consumer) |
| `Battery` | Console Executable | Feldgerät: Batteriespeicher-Simulator (Modbus Port 1503) |
| `PV_Simulator` | Console Executable | Feldgerät: Photovoltaik-Simulator (Modbus Port 1504) |
| `WindTurbine_Simulator` | Console Executable | Feldgerät: Windkraftanlage (Modbus Port 1505) |
| `Simulations-Master` | Console Executable | Zentraler Taktgeber & Zeitsynchronisation |
| `ModbusDiagnoseClient` | Console Executable | Interaktives Modbus-Testwerkzeug zum Lesen und Schreiben von Registern |
| `ModbusRabbitMqGateway`| Console Executable | Gateway: Liest Modbus-Register aus und publiziert Sensordaten via RabbitMQ; leitet Steuerbefehle zurück |
| `RabbitMqViewer` | Console Executable | Interaktiver Live-Monitor für empfangene Sensordaten und RabbitMQ-Nachrichten |

---

## 3. Modbus Registerbelegung (LB02 Standard)

Alle Feldgeräte implementieren im Registerkopf (Holding Registers 1–12) den einheitlichen Standard:
* **Reg 1:** OperatingState (Int16: 0=Off, 1=Booting, 2=Running, 3=Error)
* **Reg 2:** ErrorCode (Int16: 0=None, >0=Error-Code)
* **Reg 3–4:** ActivePower (Float32 in Watt, High Word / Low Word)
* **Reg 5–6:** CumulativeEnergy (Float32 in kWh, High Word / Low Word)
* **Reg 7–8:** VirtualTime (Int32 Unix-Timestamp, Low Word / High Word)
* **Reg 9:** OnOffState (Int16: 0=Off, 1=On)
* **Reg 10–11:** Setpoint (Float32, High Word / Low Word, z.B. Leistungssollwert)
* **Reg 12:** ResetError (Int16: Schreiben von 1 setzt Fehler zurück)
* **Reg 13+:** Gerätespezifische Register (z.B. SOC Port 1 & 2 bei Dual-Port Charger; SOC, Spannung, Strom bei Battery)

---

## 4. RabbitMQ Messaging Konventionen

* **Exchange:** `sim.exchange` (Typ: `topic`, durable)
* **Routing Keys:**
  * Telemetrie (Publisher): `sim.telemetry.<device-type>.<device-id>`  
    *Beispiel:* `sim.telemetry.charger.1`, `sim.telemetry.battery.1`, `sim.telemetry.pv.1`
  * Steuerbefehle (Consumer): `sim.commands.<device-type>.<device-id>`  
    *Beispiel:* `sim.commands.charger.1`
* **Nachrichtenformate:**
  * Telemetrie: `DeviceTelemetryMessage` (JSON mit DeviceId, Type, Timestamp, VirtualTime, OperatingState, ErrorCode, ActivePowerWatts, CumulativeEnergyKwh, DeviceSpecificData).
  * Befehle: `DeviceCommandMessage` (JSON mit DeviceId, CommandType [SetPowerLimit, ResetFault, ToggleCar], Value, Timestamp).
* **Verbindungsresilienz:** Simulatoren dürfen bei vorübergehend nicht erreichbarem Broker niemals blockieren oder abstürzen. Reconnects laufen asynchron im Hintergrund.

---

## 5. Build- und Ausführungsvorschriften

1. **SDK:** .NET 10.0 (`/usr/local/share/dotnet/dotnet`).
2. **MSBuild Parallelism:** Auf macOS in Sandboxes oder Umgebungen mit eingeschränktem IPC **immer `-m:1`** verwenden:
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
   ```
3. **NuGet Konfiguration:** Das Repository enthält eine vorkonfigurierte `nuget.config`, die lokale Caches berücksichtigt und deterministische Restores gewährleistet.

---

## 6. Wiederkehrende Entwicklungs-Workflows (Skills)

Für automatisierte und wiederkehrende Aufgaben existieren dedizierte Skills unter `skills/`:
* `skills/build-and-test`: Bauen und Verifizieren der gesamten Solution.
* `skills/run-simulators`: Geordnetes Starten der Simulationsknoten.
* `skills/modbus-rabbitmq-gateway`: Konfigurieren und Betreiben des Modbus-RabbitMQ Gateways.
* `skills/rabbitmq-messaging`: Vorlage für RabbitMQ Publisher/Consumer Integration in Feldgeräte.
* `skills/rabbitmq-viewer`: Starten und Bedienen des RabbitMQ Live-Viewers.
