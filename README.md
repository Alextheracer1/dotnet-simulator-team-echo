# Energy Twin 2026 - Verteiltes Simulationssystem (Team 01)

Dieses Repository enthält die Lösung für die **LB02: Vom einzelnen Simulator zum verteilten Teamsystem**. Es handelt sich um ein verteiltes System aus mehreren eigenständigen .NET 10 Applikationen, die über eine gemeinsame virtuelle Zeit synchronisiert werden und ihre Gerätedaten via Modbus TCP bereitstellen.

## Voraussetzungen
*   .NET 10.0 SDK
*   Lokales Netzwerk/Localhost für Modbus TCP (Ports ab 1502 aufwärts)
*   Keine aktive Internetverbindung zur Laufzeit erforderlich.

## Gesamtarchitektur und Zeitsynchronisation

Das System besteht aus voneinander isolierten Prozessen, die über das Netzwerk kommunizieren. Jeder Feldgeräte-Simulator hostet einen eigenen Modbus TCP-Server auf einem unprivilegierten Port (z.B. 1502, 1503), um seinen aktuellen Zustand bereitzustellen und Sollwerte zu empfangen.

**Zeitsynchronisation (Konzept):**
*Hinweis: Bitte passen Sie diesen Abschnitt nach Ihrer finalen Architekturentscheidung an.*
Der `SimulationsMaster` fungiert als zentraler Taktgeber. Die Feldgeräte-Simulatoren synchronisieren sich mit diesem Master (z.B. über eine REST-API, gRPC oder einen TCP-Socket), um die aktuelle virtuelle Zeit und die Simulationsgeschwindigkeit zu erhalten. Durch definierte Toleranzgrenzen wird sichergestellt, dass die lokale Simulationszeit der Feldgeräte nicht von der Master-Zeit abweicht.

## Module und Verantwortlichkeiten

Die Systementwicklung wurde nach Vorgabe gleichmäßig im Team aufgeteilt. Jedes Modul verfügt über eine eigene `README.md` im jeweiligen Projektverzeichnis mit spezifischen Details und Registerplänen.

| Modul / Projekt | Kurzbeschreibung | Hauptverantwortliche Person |
| :--- | :--- | :--- |
| **TeamShared** | Gemeinsame Class Library mit Modbus-Wrappern, Register-Mapping (01-12) und DTOs. | *[Name 1]* |
| **SimulationsMaster** | Zentrale Steuerung (Start, Pause, Geschwindigkeit) und Verteilung der virtuellen Zeit. | *[Name 2]* |
| **WeatherSimulator** | Liefert orts- und zeitabhängige Wetterdaten (Sonne, Temperatur) für alle Geräte. | *[Name 3]* |
| **PvSimulator** | Feldgerät: Simuliert eine PV-Anlage und stellt Daten per Modbus TCP bereit. | *[Name 1]* |
| **BatterySimulator** | Feldgerät: Simuliert einen Batteriespeicher (Pflichtgerät). | *[Name 2]* |
| **DualPortCharger** | Feldgerät: Simuliert eine Ladesäule für E-Autos (Pflichtgerät). | *[Name 3]* |
| **ModbusDiagnoseClient**| Konsolen-Tool zum Auslesen der Register und Schreiben von Setpoints zu Testzwecken. | *[Name 1]* |

*(Hinweis: Passen Sie die Namenszuordnungen so an, dass die Last gleichmäßig auf Ihr 2er- oder 3er-Team verteilt ist.)*

## Startreihenfolge und Bedienhinweise

Da es sich um ein verteiltes System handelt, müssen die Applikationen als getrennte Prozesse in einer strikten Reihenfolge gestartet werden. Öffnen Sie für jedes Programm ein eigenes Terminal im Stammverzeichnis des Repositories.

**1. Zentralen Master starten**
```bash
dotnet run --project SimulationsMaster/SimulationsMaster.csproj
```
*Bedienung:* Im Master kann die Simulation zunächst auf "Pause" gestellt oder mit einer bestimmten Startzeit initialisiert werden.

**2. Wettersimulator starten**
```bash
dotnet run --project WeatherSimulator/WeatherSimulator.csproj
```
*Bedienung:* Läuft im Hintergrund und wartet auf Anfragen der Feldgeräte.

**3. Feldgeräte-Simulatoren starten (Reihenfolge beliebig)**
```bash
dotnet run --project PvSimulator/PvSimulator.csproj
# In weiteren Terminals:
dotnet run --project BatterySimulator/BatterySimulator.csproj
dotnet run --project DualPortCharger/DualPortCharger.csproj
```
*Bedienung:* Die Geräte verbinden sich mit dem Master und dem Wetter-Dienst. Die Modbus-Server starten auf den konfigurierten Ports (z.B. 1502, 1503, 1504).

**4. Modbus-Diagnose-Client starten (Optional, für Tests)**
```bash
dotnet run --project ModbusDiagnoseClient/ModbusDiagnoseClient.csproj
```
*Bedienung:* Folgen Sie dem interaktiven Konsolenmenü, um sich mit einem spezifischen Feldgerät (IP + Port) zu verbinden. Sie können den Registerkopf auslesen oder Fehler zurücksetzen.

## Kontrolliertes Beenden
Um das System sauber herunterzufahren, beenden Sie zunächst die Feldgeräte-Simulatoren (in der Regel durch Eingabe von `q`, `exit` oder `Strg+C`, je nach Konsolen-Implementierung), gefolgt vom Wettersimulator und abschließend dem Simulations-Master.