# Projekt-Kontext: Energy Twin 2026 – Verteiltes Simulationssystem (Team Echo / Team 01)

Dieses Dokument dient als zentrale Wissensbasis, Architekturübersicht, Konventionsdokumentation und Handover-Status für das gesamte Repository und alle beteiligten Simulatoren.

---

## 1. Projektstruktur & Architektur

### 1.1 Projektübersicht & Modulverantwortlichkeiten
Das Gesamtsystem bildet ein verteiltes Digital-Twin-System zur Energiesimulation (.NET 10.0), bei dem verschiedene Geräte als eigenständige Prozesse auf `localhost` laufen und über standardisierte Schnittstellen (Modbus TCP und REST-APIs) kommunizieren.

| Modul / Projekt | Typ | Port / Endpunkt | Kurzbeschreibung & Verantwortlichkeit |
| :--- | :--- | :--- | :--- |
| **TeamShared** | Class Library | – | Gemeinsame Modbus-Logik, Register-Definitionen (Holding-Register 01–12), Helper und DTOs. (*Alexander Graf*) |
| **TeamConsoleShared** | Class Library | – | Wiederverwendbare Konsolen-UI-Komponenten (Boxen, Power-Bars, Gauge-Bars, Metric-Grids). |
| **PV_Simulator** | Console App | Modbus TCP (z. B. 1502) | Digitaler Zwilling einer Photovoltaikanlage mit konfigurierbaren Modul- und Wechselrichterparametern. (*Nikolas Giesen*) |
| **WindTurbine_Simulator** | Console App | Modbus TCP (z. B. 1503) | Digitaler Zwilling einer Windkraftanlage mit Tagesprofil, Rauschen und Modbus-Bereitstellung. (*Nikolas Giesen*) |
| **Battery** | Console App | Modbus TCP | Batteriespeicher-Simulator (Lade-/Entladezustand, Kapazität). (*Nazar Burdeinyi*) |
| **Dual-Port-Charger** | Console App | Modbus TCP | Zwei-Port-Ladesäulen-Simulator für Elektrofahrzeuge. (*Alexander Graf*) |
| **ModbusDiagnoseClient** | Console App | Client | Diagnose- und Test-Client zum Lesen/Schreiben von Modbus-Registern der Feldgeräte. (*Alexander Graf*) |
| **Simulations-Master** | Console App | TCP `1510` (Standard) | Implementierter zentraler Taktgeber; verteilt virtuelle Zeitschritte an registrierte Simulatoren und wartet auf deren Bestätigung. |
| **WeatherSimulator** | Web / Minimal API | HTTP REST `localhost:5005` | Zentraler Offline-Wetterprovider. Liefert standort- und zeitaufgelöste Daten sowie berechneten Sonnenstand. (*Nikolas Giesen*) |

### 1.2 Interaktions- & Kommunikationsmuster
- **Modbus TCP:** Feldgeräte (`PV_Simulator`, `WindTurbine_Simulator`, `Battery`, `Dual-Port-Charger`) stellen ihre aktuellen Messwerte und Betriebszustände über Holding-Register (Portbereich ab 1502 aufwärts) bereit.
- **Simulation Master:** Der Master verteilt die gemeinsame virtuelle Zeit über TCP und zeilengetrennte JSON-Nachrichten. Nur registrierte Clients nehmen an der Zeitschritt-Barriere teil. Clients können Zustandsdaten melden, die der aktuelle Master jedoch nicht auswertet; Wetterwerte berechnet oder verteilt er nicht.
- **REST Minimal API (WeatherProvider):** `WeatherSimulator` stellt lokale Wetterdaten über HTTP GET bereit. Bei der Analyse wurde kein Simulator gefunden, der diese API bereits aufruft; eine Anbindung ist derzeit ein Integrationsschritt, keine bestätigte bestehende Kommunikation.
  - Endpunkt: `GET /api/weather?lat={latitude}&lon={longitude}&time={timestamp}`
  - Offline-Fähigkeit: Wetterwerte werden aus der lokalen CSV geladen; für die Datenbereitstellung selbst ist keine externe Wetter-Web-API erforderlich.

### 1.3 Zentrale Einstiegspunkte
- Solution: `dotNET_Programmierung_2026_Aufgabe_02.slnx`
- Registrierung und Shared Libs: `TeamShared/TeamSharedModbus.cs`, `TeamConsoleShared/`
- Master-Start und Taktgeber: `Simulations-Master/Program.cs`, `Simulations-Master/SimulationEngine.cs`, `Simulations-Master/TcpSimulationServer.cs`
- Master-Protokoll und Client-Vertrag: `TeamShared/SimulationProtocol.cs`, `TeamShared/SimulationMaster-Client-Protocol.md`
- Wetter-Datengrundlage: `WeatherSimulator/Data/wetterdaten_salzburg.csv`

### 1.4 WeatherSimulator – Aufbau und Verhalten
- `WeatherSimulator/Program.cs`: Minimal-API-Start, Singleton-Registrierung, CSV-Laden und Routen `GET /`, `GET /api/weather/health` sowie `GET /api/weather?lat={lat}&lon={lon}&time={timestamp}`.
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
- **Vorhandener Client:** `Battery/Simulation/SimulationMasterClient.cs` implementiert `ITimeMasterClient`; `Battery/Program.cs` verbindet ihn standardmäßig mit `127.0.0.1:1510`. Dieser Client ist im Battery-Projekt implementiert, nicht als wiederverwendbare Clientklasse in `TeamShared`.
- **Integrationsstatus:** PV, Windturbine und WeatherSimulator verbinden sich derzeit nicht mit dem Master. PV besitzt keine Referenz auf `TeamShared`; Windturbine referenziert `TeamShared`, implementiert aber keinen Master-Client. WeatherSimulator referenziert ebenfalls keinen Master-Vertrag. Der Master sendet keine Wetterdaten. Der Wetterdienst bietet aktuell nur eine GET-API, die ein Verbraucher aktiv aufrufen müsste.

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
  - Zukünftige UI: `Spectre.Console` (nach Abschluss der Basisfunktionalität)

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

---

## 3. Handover- & Fortsetzungsstrategie

### 3.1 Aktueller Stand (Status Quo)
- `Simulations-Master` ist implementiert: TCP-Server mit JSON-Zeilenprotokoll, virtueller Uhr, Start/Pause/Fortsetzen, konfigurierbarer Schrittweite und ACK-Barriere für registrierte Clients.
- Der gemeinsame Vertrag `ITimeMasterClient` liegt in `TeamShared/SimulationProtocol.cs`; als einzige gefundene Implementierung ist `Battery/Simulation/SimulationMasterClient.cs` in Verwendung.
- PV-, Windturbinen- und WeatherSimulator sind aktuell nicht mit dem Simulationsmaster verbunden und berechnen bzw. verwalten ihre Zeit lokal.
- Der Master verteilt nur Simulationszeit/-steuerung, keine Wetterwerte. Der WeatherSimulator bietet derzeit eine HTTP-GET-Abfrage; ein direkter Push an PV oder Wind ist nicht implementiert.
- `WeatherSimulator` ist als ASP.NET-Core-Minimal-API implementiert und in `dotNET_Programmierung_2026_Aufgabe_02.slnx` eingebunden.
- Verifikation in der Analyse am 2026-11-08: Projekt-Build erfolgreich; lokaler Start lud 8.784 Datensätze an einem Standort. `/api/weather/health` lieferte `Healthy`, eine gültige Anfrage an `/api/weather` lieferte Wetterdaten und Sonnenstand, ungültige Latitude ergab HTTP 400.
- Es wurden keine dem Projekt zugeordneten automatisierten Tests im Test Explorer gefunden. Der erfolgreiche Build und der manuelle HTTP-Smoke-Test belegen daher keinen vollständigen Test aller Funktionen.
- Der Haversine-Algorithmus und die lineare Interpolation sind im Code vorhanden; die Auswahl zwischen mehreren Standorten ist mit dem vorhandenen einzelnen CSV-Standort nicht nachweisbar.
- Zeitabbildung auf das Referenzjahr, Schaltjahr-Korrektur, Begrenzung auf den Datensatzbereich, Sonnenstandsberechnung und Parameterprüfung sind implementiert; über die obigen HTTP-Aufrufe hinaus wurden diese Fälle nicht einzeln automatisiert getestet.
- Bei der Suche im C#-Quellcode wurde kein Verbraucher der WeatherSimulator-HTTP-API außerhalb des Projekts gefunden. Insbesondere stellt die separate Wetterlogik im PV-Simulator keine belegte API-Integration dar.
- Alle weiteren Simulatoren (`PV_Simulator`, `WindTurbine_Simulator`, `Battery`, `Dual-Port-Charger`) und Shared-Libraries liegen im Workspace vor.

### 3.2 In Bearbeitung (Work in Progress)
- In der Master-Analyse wurden keine Simulator-Codeänderungen vorgenommen; dokumentiert wurde der vorhandene Protokoll- und Integrationsstand.
- Eine Master-Anbindung von PV, Windturbine und WeatherSimulator sowie die Übertragung von Wetterdaten an PV/Wind fehlen.
- Eine automatische Testabdeckung für den WeatherSimulator wurde nicht gefunden; der Master-Kommunikationspfad wurde in dieser Dokumentationsaufgabe nicht erneut ausgeführt.

### 3.3 Nächste konkrete Schritte
1. Master-Client-Verbindungen und ACK-Verarbeitung für WeatherSimulator, PV und Windturbine ergänzen, ohne deren fachliche Berechnungen in den Master zu verschieben.
2. Festlegen und implementieren, wie PV und Wind die Wetterdaten erhalten; aktuell existiert nur die vom Verbraucher aufzurufende HTTP-GET-API.
3. Synchronisationsabläufe (Registrierung, Pause, Zeitschritt, ACK, Verbindungsabbruch) automatisiert testen.
4. Bei Bedarf WeatherSimulator-Tests für CSV-Parsing, Interpolation, Zeitabbildung und Sonnenstand ergänzen.

### 3.4 Wiederaufnahme-Hinweis für Agenten
Wenn eine Sitzung unterbrochen wird: Für Master-Kommunikation zuerst `TeamShared/SimulationProtocol.cs` und `TeamShared/SimulationMaster-Client-Protocol.md` lesen, anschließend `Battery/Simulation/SimulationMasterClient.cs` als vorhandenes Client-Beispiel sowie `Simulations-Master/TcpSimulationServer.cs` für die Server-Barriere prüfen. Master-Port und Takt sind über `SIMULATION_MASTER_PORT` und `SIMULATION_MASTER_TIME_STEP` konfigurierbar. Für Wetterdaten gelten weiterhin `WeatherSimulator/Program.cs` und `WeatherSimulator/Services/`; die Wetter-GET-API ist derzeit nicht an PV oder Wind angeschlossen.
