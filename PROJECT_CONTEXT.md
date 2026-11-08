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
| **SimulationsMaster** | (In Konzept) | REST / TCP | Zukünftiger zentraler Taktgeber für die virtuelle Simulationszeit. (*Nazar Burdeinyi*) |
| **WeatherSimulator** | Web / Minimal API | HTTP REST `localhost:5005` | Zentraler Offline-Wetterprovider. Liefert standort- und zeitaufgelöste Daten sowie berechneten Sonnenstand. (*Nikolas Giesen*) |

### 1.2 Interaktions- & Kommunikationsmuster
- **Modbus TCP:** Feldgeräte (`PV_Simulator`, `WindTurbine_Simulator`, `Battery`, `Dual-Port-Charger`) stellen ihre aktuellen Messwerte und Betriebszustände über Holding-Register (Portbereich ab 1502 aufwärts) bereit.
- **REST Minimal API (WeatherProvider):** Simulatoren rufen Umgebungs- und Wetterdaten über den zentralen `WeatherSimulator` via HTTP GET ab.
  - Endpunkt: `GET /api/weather?lat={latitude}&lon={longitude}&time={timestamp}`
  - Offline-Fähigkeit: Vollständig autark auf `localhost` ohne externe Web-Abhängigkeiten.

### 1.3 Zentrale Einstiegspunkte
- Solution: `dotNET_Programmierung_2026_Aufgabe_02.slnx`
- Registrierung und Shared Libs: `TeamShared/TeamSharedModbus.cs`, `TeamConsoleShared/`
- Wetter-Datengrundlage: `WeatherSimulator/Data/wetterdaten_salzburg.csv`

---

## 2. Getroffene Konventionen & Vereinbarungen

### 2.1 Technologie-Stack
- **Laufzeitumgebung & SDK:** .NET 10.0 (`net10.0`), C# 13 / C# Latest.
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
   - **Jahres-Wrapping (Modulo):** Liegt der angefragte Zeitstempel in einem anderen Jahr als die CSV-Referenzdaten (z. B. Simulation in 2026, Daten aus 2024), wird das Datum automatisch auf das Referenzjahr gemappt (Tag des Jahres / Uhrzeit), um robuste Simulationsläufe ohne Out-of-Bounds-Fehler zu garantieren.
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
- `WeatherSimulator` ist vollständig implementiert und getestet:
  - Minimal API auf Port 5005 (`http://localhost:5005`).
  - Endpunkt `GET /api/weather?lat={latitude}&lon={longitude}&time={timestamp}` liefert flaches JSON mit interpolierten Wetterdaten und berechnetem Sonnenstand.
  - CSV-Parser lädt `WeatherSimulator/Data/wetterdaten_salzburg.csv` (8.784 Stundenzeilen) in den Speicher.
  - Multi-Location-Auflösung via Haversine-Großkreisdistanz.
  - Jahres-Wrapping & lineare Stützstellen-Interpolation.
  - Astronomische Sonnenstandsberechnung (Azimut 0°=Nord, Elevation 0°=Horizont).
  - Validation und Bad-Request-Handling für ungültige oder fehlende Parameter.
  - Projekt in `dotNET_Programmierung_2026_Aufgabe_02.slnx` eingebunden.
  - Entwicklungsjournal in `Nikolas-Giesen.md` aktualisiert.
- Alle weiteren Simulatoren (`PV_Simulator`, `WindTurbine_Simulator`, `Battery`, `Dual-Port-Charger`) und Shared-Libraries liegen im Workspace vor.

### 3.2 In Bearbeitung (Work in Progress)
- Aktuell keine offenen Implementierungsbaustellen im WeatherSimulator.

### 3.3 Nächste konkrete Schritte
1. Feldgeräte (`PV_Simulator`, `WindTurbine_Simulator`) an den neuen REST-Endpunkt `/api/weather` anbinden, sodass sie ihre Wetter- und Einstrahlungswerte live vom `WeatherSimulator` beziehen.
2. Konsolenausgabe des `WeatherSimulator` bei Bedarf auf Spectre.Console erweitern.
3. Integrationstest über das Gesamtsystem (Master + Wetter + Feldgeräte).

### 3.4 Wiederaufnahme-Hinweis für Agenten
Wenn eine Sitzung unterbrochen wird: Dieser Kontext fasst alle Vereinbarungen zusammen. Es sind keine weiteren Nachfragen zur Architektur nötig; die Schritte 1–8 in Abschnitt 3.3 können direkt sequentiell abgearbeitet werden.
