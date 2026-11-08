# Projekt-Kontext: Energy Twin 2026 – Verteiltes Simulationssystem (Team Echo / Team 01)

Dieses Dokument dient als zentrale Wissensbasis, Architekturübersicht, Konventionsdokumentation und Handover-Status für das gesamte Repository und alle beteiligten Simulatoren.

---

## 1. Projektstruktur & Architektur

### 1.1 Projektübersicht & Modulverantwortlichkeiten
Das Gesamtsystem bildet ein verteiltes Digital-Twin-System zur Energiesimulation (.NET 10.0), bei dem Geräte als eigenständige Prozesse laufen. Je nach Aufgabe kommunizieren sie über Master-TCP mit zeilengetrenntem JSON, HTTP, Modbus TCP oder RabbitMQ.

| Modul / Projekt | Typ | Port / Endpunkt | Kurzbeschreibung & Verantwortlichkeit |
| :--- | :--- | :--- | :--- |
| **TeamShared** | Class Library | – | Gemeinsame Modbus-Logik und Register-Definitionen sowie Simulation-Master-Protokoll und `ITimeMasterClient`. |
| **TeamConsoleShared** | Class Library | – | Wiederverwendbare Konsolen-UI-Komponenten (Boxen, Power-Bars, Gauge-Bars, Metric-Grids). |
| **PV_Simulator** | Console App | Master-TCP `1510`, Modbus TCP `1504`, HTTP `6000+` | Digitaler Zwilling einer Photovoltaikanlage; empfängt Wetter-Push, stellt Daten via Modbus TCP bereit. |
| **WindTurbine_Simulator** | Console App | Master-TCP `1510`, Modbus TCP `1505`, HTTP `6100+` | Digitaler Zwilling einer Windkraftanlage mit Wetter-Push, Turbulenzmodell und Modbus-Bereitstellung. |
| **Battery** | Console App | Modbus TCP | Batteriespeicher-Simulator (Lade-/Entladezustand, Kapazität). (*Nazar Burdeinyi*) |
| **Dual-Port-Charger** | Console App | Modbus TCP | Zwei-Port-Ladesäulen-Simulator für Elektrofahrzeuge. (*Alexander Graf*) |
| **ModbusDiagnoseClient** | Console App | Client | Diagnose- und Test-Client zum Lesen/Schreiben von Modbus-Registern der Feldgeräte. (*Alexander Graf*) |
| **Simulations-Master** | Console App | TCP `1510` (Standard) | Implementierter zentraler Taktgeber; verteilt virtuelle Zeitschritte an registrierte Simulatoren und wartet auf deren Bestätigung. |
| **WeatherSimulator** | Web / Minimal API | HTTP `localhost:5005`, Master-TCP `1510` | CSV-basierter Wetterprovider; liefert standort-/zeitbezogene Wetterwerte und Sonnenstand. |
| **ModbusRabbitMqGateway** | Console App | Modbus TCP / RabbitMQ | Brücke für Feldgeräte-Telemetrie und Befehle; nicht Wettertransport. |
| **TeamRabbitMQShared** | Class Library | RabbitMQ | Gemeinsame Publisher-/Consumer-Infrastruktur und Telemetrie-Mapping. |
| **RabbitMqViewer** | Console App | RabbitMQ | Zeigt Live-Telemetrie an. |
| **TeamRabbitMQShared.Tests** | Testprojekt | – | Tests der RabbitMQ-Shared-Bibliothek. |

### 1.2 Interaktions- & Kommunikationsmuster
- **Modbus TCP:** Alle Feldgeräte (`Dual-Port-Charger` auf Port 1502, `Battery` auf 1503, `PV_Simulator` auf 1504, `WindTurbine_Simulator` auf 1505) stellen ihre Messwerte/Betriebszustände über Modbus TCP gemäß LB02-Registerstandard (Holding Register 1–12) sowie gerätespezifischen Registern (13+) bereit.
- **Simulation Master:** Der Master verteilt die gemeinsame virtuelle Zeit über TCP und zeilengetrennte JSON-Nachrichten. PV, Wind, WeatherSimulator und Battery registrieren jeweils eigene Clients auf Basis des Vertrags in `TeamShared`; registrierte Clients nehmen an der Zeitschritt-Barriere teil. Der Master berechnet oder verteilt selbst keine Wetterwerte.
- **REST Minimal API (WeatherProvider) & Push:** `WeatherSimulator` stellt standortgenaue Wetterdaten über HTTP Minimal API (Port 5005) und pro Zeitschritt per HTTP POST Push an registrierte Feldgeräte (`PV_Simulator` Port 6000+, `WindTurbine_Simulator` Port 6100+) bereit. Beide Simulatoren verfügen über einen robusten Offline-Fallback auf interne Modelle.
- **RabbitMQ (Weg A – Gateway-Architektur):** `ModbusRabbitMqGateway` pollt periodisch die Modbus-Server aller Feldgeräte (Charger 1502, Battery 1503, PV 1504, Wind 1505) und publiziert standardisierte Telemetriedaten (`DeviceTelemetryMessage`) auf den Topic-Exchange `sim.exchange` (`sim.telemetry.<deviceType>.<id>`). Der `RabbitMqViewer` abonniert `#` und visualisiert alle Simulatoren im einheitlichen Dashboard.
- **Offline-Zeitverhalten:** Wenn der Master nach erfolgreicher Synchronisierung ausfällt, rechnen PV, Wind und WeatherSimulator mit dem zuletzt bekannten Masterstatus und Zeitschritt weiter. Nach Wiederverbindung springen sie auf die aktuelle Masterzeit; ausgefallene Schritte werden nicht nachträglich abgespielt.

### 1.3 Zentrale Einstiegspunkte
- Solution: `dotNET_Programmierung_2026_Aufgabe_02.slnx`
- Registrierung und Shared Libs: `TeamShared/TeamSharedModbus.cs`, `TeamConsoleShared/`
- Master-Start und Taktgeber: `Simulations-Master/Program.cs`, `Simulations-Master/SimulationEngine.cs`, `Simulations-Master/TcpSimulationServer.cs`
- Master-Protokoll und Client-Vertrag: `TeamShared/SimulationProtocol.cs`, `TeamShared/SimulationMaster-Client-Protocol.md`
- Wetter-Datengrundlage: `WeatherSimulator/Data/wetterdaten_salzburg.csv`

### 1.4 WeatherSimulator – Aufbau und Verhalten
- `WeatherSimulator/Program.cs`: Minimal-API-Start, Singleton-Registrierung, CSV-Laden und Routen `GET /`, `GET /api/weather/health` sowie `GET /api/weather?lat={lat}&lon={lon}&time={timestamp}`.
- `WeatherSimulator/Services/WeatherSimulationMasterWorker.cs`: blockiert den ersten HTTP-Start bis eine gültige virtuelle Zeit vom Master empfangen wurde; nach späterem Master-Ausfall bleibt der HTTP-Dienst verfügbar und die Verbindung wird erneut aufgebaut.
- `WeatherSimulator/Services/WeatherCsvParser.cs`: erkennt Komma oder Semikolon, liest die benötigten Spalten `Latitude`, `Longitude`, `Timestamp`, `TemperatureC`, `PrecipitationMm`, `CloudCoverPercent` und `WindSpeedMps`; Zeit und Zahlen werden invariant eingelesen. Fehlerhafte Zeilen werden übersprungen; bei fehlenden Pflichtspalten oder ohne gültige Datensätze schlägt das Laden fehl.
- `WeatherSimulator/Services/WeatherService.cs`: wählt per Haversine den nächstgelegenen Standort, bildet das Datum auf das Referenzjahr ab, begrenzt es auf die Datenreihe und interpoliert die vier Wetterwerte linear. Sonnenstand wird für die originale Anfragezeit und Koordinaten berechnet.
- `WeatherSimulator/Services/SolarPositionCalculator.cs`: berechnet Sonnenazimut und -elevation.
- `WeatherSimulator/Models/`: enthält Wetterdatensatz, Standort-Zeitreihe, CSV-Datensatz, Antwortmodell und Sonnenstand.
- CSV-Bestand bei Analyse: 8.784 Stundenwerte, ein Standort (47.77, 13.09), Zeitraum 2024-01-01 bis 2024-12-31. Die Standortauswahl ist implementiert, kann mit nur einem Standort aber nicht räumlich zwischen Standorten unterscheiden.
- Konfiguration: `WeatherSimulator/appsettings.json` setzt Standardport 5005 und `Data/wetterdaten_salzburg.csv`; `WeatherSimulator/WeatherSimulator.csproj` kopiert die CSV ins Ausgabeverzeichnis.

### 1.5 Simulations-Master – virtuelle Zeit und Client-Kommunikation
- **Einstiegspunkte:** `Simulations-Master/Program.cs` startet `SimulationEngine` und `TcpSimulationServer`. Die virtuelle Startzeit ist standardmäßig `DateTime.UtcNow`; die Simulation startet pausiert.
- **Konfiguration:** TCP-Port über `SIMULATION_MASTER_PORT` (Standard `1510`); anfänglicher Simulationsschritt über `SIMULATION_MASTER_TIME_STEP` (Standard `1s`). Die Konsole steuert Start (`S`), Pause (`P`), Fortsetzen (`R`), Schrittweite (`+`/`-`) und Beenden (`Q`). Verfügbare Schrittweiten: `1s`, `10s`, `1m`, `15m`, `1h`, `2h`, `12h`, `24h`, `7d`.
- **Vertrag:** `TeamShared/SimulationProtocol.cs` definiert `SimulationMessage`, `SimulationEnvironment`, Nachrichtentypen und `ITimeMasterClient`. Die Verbindung ist dauerhaftes TCP; Nachrichten sind UTF-8-JSON, eine Nachricht pro Zeile, JSON-Felder in camelCase. Das Protokoll ist in `TeamShared/SimulationMaster-Client-Protocol.md` beschrieben.
- **Verbindungsablauf:** Der Master sendet beim Verbindungsaufbau eine `Environment`-Nachricht. Der Client kann `EnvironmentRequest` senden und registriert sich mit `Register` und stabiler `StationId`. Nicht registrierte Verbindungen erhalten keine `TimeStep`-Nachrichten und nehmen nicht an der Barriere teil.
- **Zeitschritt-Barriere:** Während die Simulation läuft, sendet der Master ungefähr einmal pro realer Sekunde `TimeStep` mit `Sequence`, `PreviousVirtualTime`, Ziel-`VirtualTime` und `TimeStep`. Der Simulator verarbeitet genau diesen virtuellen Zeitraum und sendet danach `Acknowledgement` mit derselben Sequence und Zielzeit. Erst wenn alle registrierten Clients bestätigt haben, committet der Master den nächsten Zeitpunkt. Clients ohne ACK innerhalb von fünf Sekunden werden getrennt. Bei Pause werden periodisch `Environment`-Snapshots gesendet; sie sind informativ und erfordern keinen ACK.
- **State-Nachrichten:** Ein Client darf `State` mit Leistungs-/Gerätestatus melden. Der aktuelle `TcpSimulationServer` akzeptiert diese Nachricht, verwendet sie aber nicht zur Zeitsteuerung oder für weitere Berechnungen.
- **Vorhandene Clients:** `Battery/Simulation/SimulationMasterClient.cs`, `PV_Simulator/PV_Simulator/Simulation/PvSimulationMasterClient.cs`, `WindTurbine_Simulator/Simulation/WindSimulationMasterClient.cs` und `WeatherSimulator/Services/WeatherSimulationMasterClient.cs` implementieren jeweils `ITimeMasterClient`. Der Vertrag liegt in `TeamShared/SimulationProtocol.cs`; die konkreten TCP-Clients sind nicht als eine gemeinsame Implementierung in `TeamShared` zentralisiert.
- **Integrationsstatus:** PV und Wind verwenden Master-Zeitschritte, bekommen darüber aber nur die virtuelle Zeit und keinen Wetter-Snapshot. Wind berechnet und aktualisiert seine lokale Simulation vor dem ACK und setzt bei Verbindungsverlust mit letztem laufendem Zustand/Zeitschritt fort; beim Reconnect übernimmt es die aktuelle Masterzeit. PV sendet derzeit das ACK vor `AdvanceAndLogAsync` und damit vor dem lokalen Simulationsschritt (siehe Prüfhinweis in Abschnitt 3.1). Der Weather-Worker setzt seine virtuelle Uhr aus Master-Nachrichten; beim ersten Start wird der HTTP-Host erst nach einer gültigen virtuellen Zeit freigegeben. Bei späterem Ausfall bleibt der HTTP-Dienst verfügbar und die Wetteruhr läuft mit dem letzten laufenden Zustand und Zeitschritt weiter. Wetterdaten werden noch nicht an PV oder Wind übertragen.

---

## 2. Getroffene Konventionen & Vereinbarungen

### 2.1 Technologie-Stack
- **Laufzeitumgebung & SDK:** .NET 10.0 (`net10.0`). Eine konkrete C#-Sprachversion ist in `WeatherSimulator.csproj` nicht explizit festgelegt.
- **SDK-Typen:**
  - Simulatoren / Shared: `Microsoft.NET.Sdk`
  - WeatherSimulator: `Microsoft.NET.Sdk.Web` (Minimal API)
- **Projekt-Settings:** `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`.
- **Externe Bibliotheken:**
  - Modbus: `EasyModbusTCP.NETCore` (v1.0.0)
  - HTTP Server: ASP.NET Core Minimal APIs (integriert in .NET 10)
  - Konsolen-UI: `Spectre.Console` (v0.57.2)
  - RabbitMQ: `RabbitMQ.Client` (v7.0.0)
  - Tests: `xunit` (v2.9.3; Visual-Studio-Testadapter/`Microsoft.NET.Test.Sdk` ist im Testprojekt nicht referenziert)
- **Master-Port-Dokumentation:** Der Code und das Master-Protokolldokument verwenden Standardport `1510`. `.agents/skills/run-simulators/SKILL.md` enthält noch `5000` und listet Weather- und Wind-Simulator nicht auf; Runbook-Angabe daher vor einem Start mit dem Code abgleichen.

### 2.2 Spezifikationen für den WeatherSimulator (aus Grill-Me Abstimmung)
1. **JSON-Response-Schema:**
   - Flache, saubere Struktur im JSON-Response:
   ```json
   {
     "latitude": 47.77,
     "longitude": 13.09,
     "timestamp": "2026-10-29T12:00:00Z",
     "temperatureC": 7.5,
     "precipitationMm": 0.0,
     "cloudCoverPercent": 10.0,
     "windSpeedMps": 1.31,
     "azimuth": 178.4,
     "elevation": 24.1
   }
   ```
2. **Multi-Location & Distanzermittlung:**
   - Auswahl des nächstgelegenen Datenpunkt-Standorts aus der CSV über die geodätische **Haversine-Formel** (Großkreisdistanz).
3. **Zeit-Auflösung & Interpolation:**
   - **Lineare Interpolation** der Wetterparameter zwischen den beiden Stundenstützstellen der CSV für die exakt angefragte Minute/Sekunde.
   - **Abbildung auf das Referenzjahr:** Die Implementierung setzt das angefragte Kalenderdatum in das Daten-Referenzjahr um (kein Modulo über den Tag des Jahres); ein 29. Februar wird bei einem Nicht-Schaltjahr auf den 28. Februar gesetzt. Anschließend wird auf den Zeitbereich der Datenreihe begrenzt.
4. **Sonnenstandsberechnung (Astronomie):**
   - Berechnung zur Laufzeit für den *exakten* angefragten Zeitstempel und die Koordinaten (`lat`, `lon`).
   - Konvention:
     - **Azimut:** 0° = Nord, 90° = Ost, 180° = Süd, 270° = West (im Intervall $[0^\circ, 360^\circ)$).
     - **Elevation:** 0° am Horizont, 90° im Zenit (negative Werte bei Nacht/unterhalb Horizont).
5. **Netzwerk & Ports:**
   - HTTP Server läuft auf Port `5005` (`http://localhost:5005`).
6. **Dateipfade:**
   - Die CSV-Daten liegen unter `WeatherSimulator/Data/wetterdaten_salzburg.csv` und werden beim Build in das Ausgabeverzeichnis kopiert.
7. **Konsolenausgabe:**
   - Vorerst schlichte und übersichtliche Terminal-Ausgabe (Startup-Infos, Port, geladene Datenpunkte, schlanke Request-Logs); spätere Migration auf Spectre.Console vorbereitet.

### 2.3 Vereinbarungen zur Master-Zeit und Wetterdaten-Integration
- **Implementiert:** Der `WeatherSimulator` pusht Wetterdaten pro Simulationszeitschritt via HTTP POST an registrierte Feldgeräte (`PV_Simulator` und `WindTurbine_Simulator`).
- **Callback-Empfänger:** `WeatherCallbackReceiver` (in `TeamShared.Weather`) startet dynamisch ab Port `6000` (PV) bzw. `6100` (Wind) mit automatischer Suche nach freien Ports.
- **Auto-Registrierung:** Beim Start meldet jedes Feldgerät über `POST /api/weather/register` seine `DeviceId`, den `DeviceType`, die Callback-URL sowie die Koordinaten an den `WeatherSimulator` (`WeatherClientRegistry`).
- **Synchronisation & ACK-Reihenfolge:** Bei jedem Master-`TimeStep` pusht `WeatherSimulationMasterWorker` via `WeatherSnapshotPusher` den Wetter-Snapshot für die Ziel-`VirtualTime` an alle angemeldeten Empfänger und sendet anschließend sein ACK. PV und Wind warten auf den Snapshot für diese Zielzeit, berechnen ihren Zustand (PV: Einstrahlung & Leistung, Wind: Windgeschwindigkeit als Basis plus Rauschen) und senden erst danach ihr Master-ACK.
- **Offline-Fallback:** Sollte der Wetter-Push ausbleiben (z. B. Timeout nach 3s), nutzen PV und Wind ihr lokales Fallback-Modell (`WeatherEngine` bzw. Sinus-Windkurve) weiter.

---

## 3. Handover- & Fortsetzungsstrategie

### 3.1 Aktueller Stand (Status Quo)
- `Simulations-Master` ist implementiert: TCP-Server mit JSON-Zeilenprotokoll, virtueller Uhr, Start/Pause/Fortsetzen, konfigurierbarer Schrittweite und ACK-Barriere für registrierte Clients.
- Der gemeinsame Vertrag `ITimeMasterClient` liegt in `TeamShared/SimulationProtocol.cs`.
- **Wetterdaten-Push:** Vollständig implementiert und getestet:
  - `TeamShared/Weather/`: `DeviceWeatherRegistration`, `WeatherSnapshotDto`, `WeatherCallbackReceiver`
  - `WeatherSimulator/`: `WeatherClientRegistry`, `WeatherSnapshotPusher`, Endpunkt `POST /api/weather/register` und `GET /api/weather/clients`
  - `PV_Simulator/`: Dynamischer Callback-Empfänger ab Port 6000, ACK-Reihenfolge korrigiert (erst rechnen, dann ACK), Nutzung von realem Sonnenstand und Bewölkung mit lokalem Fallback.
  - `WindTurbine_Simulator/`: Dynamischer Callback-Empfänger ab Port 6100, konfigurierbare Koordinaten (Salzburg-Default: 47.77, 13.09), Windberechnung mit realer Windgeschwindigkeit plus Turbulenzrauschen mit lokalem Fallback.
- **PV Modbus-TCP-Server (Port 1504):**
  - Implementiert via `PvModbusService` mit LB02-Holding-Registern 1–12 (OperatingState, ErrorCode, ActivePower in Watt, CumulativeEnergy in kWh, VirtualTime Unix-Timestamp, OnOffState) sowie Registern 13–18 für PV-Metriken (Irradiance, Temperature, CloudCover).
- **RabbitMQ Telemetrie-Pipeline (Weg A – Gateway-Polling):**
  - `ModbusRabbitMqGateway`: Konfiguriert für alle 4 Feldgeräte: Charger (`1502`), Battery (`1503`), PV (`1504`), Wind (`1505`).
  - `ModbusTelemetryMapper`: Vollständige Extraktion der allgemeinen Register (1–12) sowie feldspezifischer Werte für PV (Einstrahlung, Temperatur, Bewölkung) und Wind (Windgeschwindigkeit, Rotor-RPM, Pitch-Winkel).
  - `RabbitMqViewer`: Visualisiert Live-Telemetrie aller Simulatoren (Charger, Battery, PV mit 100 kW Skala, Wind mit 3000 kW Skala).
- **Tests & Build:** Vollständiger Solution-Build (`dotNET_Programmierung_2026_Aufgabe_02.slnx`) baut fehlerfrei (0 Fehler, 0 Warnungen außer optionaler Assembly-Referenzwarnung). Alle 9/9 Unit- und Integrations-Tests in `TeamRabbitMQShared.Tests` bestehen erfolgreich.

### 3.2 Wartung & Ausblick
- Simulatoren und Gateway sind betriebsbereit und können nun mit der geordneten Startreihenfolge ausgeführt werden.
- Alle Feldgeräte erfüllen die LB02-Spezifikation und integrieren sich nahtlos in die RabbitMQ- und Master-Architektur.
