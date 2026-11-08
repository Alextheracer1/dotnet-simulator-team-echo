
<!--
Verpflichtende Tags pro Eintrag:

- Done: Was wurde bearbeitet und welches Ergebnis liegt vor?
- KI: Werkzeug, Modell, Einsatzform und Umgang mit dem Ergebnis; bei keiner KI-Nutzung: keine.
- Artefact: Betroffene Dateien, CSV/Kurve, Screenshot, Dokumentation oder andere Ergebnisse.

Optionale Tags bei Relevanz:
- Comment: Entscheidung, Problem, Erkenntnis oder nächster Schritt.
- Test: Durchgeführter manueller oder automatisierter Test.
-->

# Entwicklungsjournal Nazar Burdeinyi

## 2026-10-05 – Grundaufbau vom Interaktiven Batteriespeicher-Simulator

### Done:
- 24-Stunden-Simulation mit 15-Minuten-Schritten, Ladezustand, Energiezählern und Fehlerzuständen umgesetzt.
- Interaktive Steuerung für Manuellbetrieb, Laden, Entladen, Pause/Fortsetzen sowie Fehler auslösen und zurücksetzen ergänzt.
- Geräte mit Leistungsbedarf können angeschlossen werden. 
- Konsolendashboard mit strukturierten Kennzahlen, Ladezustandsbalken, ausgerichteten Tastenhinweisen und CSV-Ausgabe.
### KI:
GitHub Copilot GPT-6 Luna als IDE-Agent für 100% der Implementierung. Implementierungen wurden händisch geprüft und verstanden.
### Artefact: 
`Program.cs`, `Battery/BatteryModel.cs`, `Simulation/BatterySimulator.cs`, `Simulation/SimulationOptions.cs`, `Simulation/DeviceLoad.cs`, `ConsoleUi/ConsoleDashboard.cs`, `Persistence/CsvSimulationLogger.cs`; CSV-Zieldatei `data/battery-run.csv`.
### Test: 
Erfolgreiche Ausführung der Simulation mit manueller Steuerung und CSV-Ausgabe; Kleine Änderungen im Zeitformat in der CSV-Datei müssen vorgenommen werden, um eine korekte Erstellung von Grafiken zu ermöglichen.

#
## 2026-10-06 – Anpassungen zum Batteriespeicher-Simulator

### Done:
- Konfigurierbare Schrittweite,  umgesetzt.
- Historische Kennzahlen für _min_, _max_, _avg_ hinzugefügt.
- Geräte können mit Leistungsbedarf und Steckdosennummer verwaltet und im pausierten Zustand einzeln, mehrfach oder vollständig getrennt werden. `P` bricht eine unvollständige Entladekonfiguration ab und setzt die Simulation fort.
- Struktur vom Konsolendashboard verbessert.
- Notifications optimiert.
- Zeitformat in der CSV-Ausgabe angepasst, um eine korrekte Erstellung von Grafiken zu ermöglichen.
- Einheitliches Meter-Interface für Batterieleistung, Gerätebedarf und Nettoleistung ergänzt, um Skalierbarkeit zu gewährleisten.
- Observable-Benachrichtigungen für Messwerte implementiert, damit externe Komponenten Aktualisierungen abonnieren können (+ Skalierbarkeit).
- Autoszenario implementiert, das die Simulation automatisch durchläuft und die CSV-Datei erstellt.
### KI:
GitHub Copilot GPT-6 Luna als IDE-Agent für 100% der Implementierung. Implementierungen wurden händisch geprüft und verstanden.
### Artefact: 
`Simulation/Meters/EnergyMeters.cs`, `Simulation/Meters/MeterReadingObservable.cs`, `Simulation/SimulationHistory.cs`, `AutoScenario.cs`.
### Test: 
Erfolgreiche Ausführung der Simulation mit manueller Steuerung und CSV-Ausgabe mit folgender Chartersellung.

## 2026-11-08 – Korrektur der virtuellen Batterieschrittweite

### Done:
- Lokaler Fallback verwendet eine virtuelle Schrittweite von 30 Minuten, wenn noch keine Schrittweite vom Simulation Master empfangen wurde. Nach einer Verbindung bleibt die zuletzt vom Master gelernte Schrittweite maßgeblich.
- Die Energieintegration bleibt auf Basis der virtuellen Zeitdifferenz in Stunden: Leistung in kW multipliziert mit Dauer in Stunden ergibt Energie in kWh.
### KI:
GitHub Copilot als IDE-Agent zur Analyse und Implementierung; Änderung wurde im Workspace geprüft.
### Artefact:
`Simulation/BatterySimulator.cs`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt. Für das Battery-Projekt wurden keine Tests gefunden. Der Lösungsbuild ist durch NU1301 blockiert, weil die konfigurierte lokale NuGet-Quelle `/Users/alex/.nuget/packages` in der Umgebung nicht verfügbar ist.

## 2026-11-08 – Battery UI, Leistungsgrenzen und CSV-Ausgabe

### Done:
- Hardwaregrenzen auf 5,024 kWh Kapazität, 2,5 kW Ladeleistung und 18 kW Entladeleistung festgelegt und in Modell sowie Optionenvalidierung durchgesetzt.
- Schrittweitensteuerung und Tempoanzeige entfernt; `+`/`-` regeln die manuelle Leistung mit 1-kW-Schritten.
- CSV-Ausgabe hängt an `data/battery-run.csv` an und wird nach drei virtuellen Stunden sowie beim Beenden geschrieben.
### KI:
GitHub Copilot als IDE-Agent zur Analyse, Implementierung und Dokumentationsaktualisierung; Änderungen wurden im Workspace geprüft.
### Artefact:
`Battery/Battery/BatteryModel.cs`, `Battery/Simulation/SimulationOptions.cs`, `Battery/Simulation/BatterySimulator.cs`, `Battery/Simulation/AutoScenario.cs`, `Battery/ConsoleUi/ConsoleDashboard.cs`, `Battery/Persistence/CsvSimulationLogger.cs`, `Battery/Program.cs`, `Battery/README.md`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt. Im Test Explorer wurden keine Battery-spezifischen Tests gefunden.

## 2026-11-08 – Zentrale Simulationszeit für Dual-Port-Charger

### Done:
Den Dual-Port-Charger an die zentrale Simulationszeit des Simulation Masters gekoppelt. Master-Zeitschritte steuern die Modellfortschreibung; der Charger sendet Zustandsmeldungen und bestätigt jeden Schritt. Der lokale Zeitlauf bleibt als Fallback bei unterbrochener Master-Verbindung verfügbar.
### KI:
GitHub Copilot als IDE-Agent zur Analyse der Battery-Synchronisierung und Dokumentation der Dual-Port-Charger-Änderung genutzt; die Integration wurde anhand des gemeinsamen Protokolls geprüft.
### Artefact:
`TeamShared/SimulationMasterClient.cs`, `Dual-Port-Charger/Program.cs`, `Dual-Port-Charger/SimulatorEngine.cs`, `Dual-Port-Charger/docs/development-journal/Alexander-Graf.md`.
### Test:
`Dual-Port-Charger/Dual-Port-Charger.csproj` und `PV_Simulator/PV_Simulator/PV_Simulator.csproj` wurden erfolgreich erstellt.

## 2026-11-08 – Konstante Auto-Steckdosenladung

### Done:
- Hardwaregrenzen auf 10 kWh Kapazität und 3 kW maximale Ladeleistung angehoben; maximale Entladeleistung bleibt 18 kW.
- Auto-Modus lädt bei nicht verfügbarer Solarenergie mit einem konstanten Ziel von 2,3 kW aus der Steckdose und verwendet während des Solarfensters weiterhin das 2,0-kW-Solarziel.
### KI:
GitHub Copilot als IDE-Agent zur Implementierung und Aktualisierung der Dokumentation; Änderungen wurden im Workspace geprüft.
### Artefact:
`Battery/Battery/BatteryModel.cs`, `Battery/Simulation/AutoScenario.cs`, `Battery/README.md`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt. Im Test Explorer wurden keine Battery-spezifischen Tests gefunden.

## 2026-11-08 – CSV-Ausgabe pro Simulatorlauf zurücksetzen

### Done:
- CSV-Datei wird beim Start des Battery-Simulators überschrieben und mit einer frischen Kopfzeile begonnen; periodisches Schreiben innerhalb eines Laufs bleibt erhalten.
### KI:
GitHub Copilot als IDE-Agent zur Implementierung und Dokumentationsaktualisierung; Änderung wurde im Workspace geprüft.
### Artefact:
`Battery/Persistence/CsvSimulationLogger.cs`, `Battery/README.md`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt. Im Test Explorer wurden keine Battery-spezifischen Tests gefunden.

## 2026-11-08 – ToU-Automatik, Gerätebudget und automatische Fehlerbehebung

### Done:
- Auto-Modus um zeitabhängige Ladeziele mit 60-minütigen linearen Rampen ergänzt: Off-Peak-Laden, Solarziel und Lastversorgung in den Spitzenzeiten.
- Zufällige Geräteprofile pro virtueller Stunde, ein priorisiertes 2,3-kW-Fahrzeug von 18:00 bis 01:00 sowie Grenzen von fünf Geräten und 20 kW kumulativer Last umgesetzt.
- Im Auto-Modus eine Fehlerwahrscheinlichkeit von 1 % pro virtueller Stundenmarke und eine automatische Rücksetzung nach 30 virtuellen Minuten ergänzt.
### KI:
GitHub Copilot als IDE-Agent zur Analyse, Implementierung und Dokumentationsaktualisierung; Änderungen wurden im Workspace geprüft.
### Artefact:
`Battery/Simulation/AutoScenario.cs`, `Battery/Simulation/BatterySimulator.cs`, `Battery/Simulation/SimulationOptions.cs`, `Battery/README.md`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt. Im Test Explorer wurden keine Battery-spezifischen Tests gefunden.

## 2026-11-08 – Battery-Modbus-Port an Charger-Konvention angepasst

### Done:
- Die vorhandene Battery-Modbus-Implementierung verwendet standardmäßig Port `1503` und kollidiert damit nicht mehr mit dem Dual-Port-Charger auf Port `1502`. Der Default ist in Optionen, Programmstart und Dashboard vereinheitlicht; `BATTERY_MODBUS_PORT` kann ihn weiterhin überschreiben.
### KI:
GitHub Copilot als IDE-Agent zur Prüfung der vorhandenen Modbus-Implementierung und Anpassung der Port-Defaults genutzt; Änderung anhand der Charger-Portkonvention überprüft.
### Artefact:
`Battery/Program.cs`, `Battery/Simulation/SimulationOptions.cs`, `Battery/ConsoleUi/ConsoleDashboard.cs`, `Battery/development-journal.md`.
### Test:
`Battery/Battery.csproj` wurde erfolgreich erstellt.

## 2026-11-08 – RabbitMQ-Telemetrie mit signierten Batterieleistungswerten

### Done:
- RabbitMQ-Telemetrie und Command-Empfang für Battery ergänzt. Die Wirkleistung wird in Watt mit Vorzeichen publiziert; negative Werte zeigen Entladung an. `Setpoint`/`SetPowerLimit`-Befehle in signierten Watt werden in signierte kW umgerechnet und über den bestehenden Simulator-Command-Queue verarbeitet.
- Moduswechsel sowie Fehlerauslösung und -rücksetzung sind über `battery-1` steuerbar.
### KI:
GitHub Copilot als IDE-Agent zur Integration mit `TeamRabbitMQShared` und Prüfung der vorzeichenbehafteten Werte genutzt; Umsetzung anhand der vorhandenen Modbus- und Charger-Muster geprüft.
### Artefact:
`Battery/Battery.csproj`, `Battery/Simulation/BatterySimulator.cs`, `TeamRabbitMQShared/Models/DeviceTelemetryMessage.cs`, `TeamRabbitMQShared/Models/DeviceCommandMessage.cs`, `TeamRabbitMQShared.Tests/TelemetryAndMappingTests.cs`.
### Test:
Der `TeamRabbitMQShared.Tests`-Harness wurde mit 6/6 erfolgreichen Tests ausgeführt.

## 2026-11-09 – RabbitMQ darf Simulator-Konsole nicht blockieren

### Done:
- RabbitMQ-Verbindungsaufbau und Command-Subscription aus dem Battery-Startpfad in einen abbrechbaren Hintergrundworker verschoben, damit das Dashboard nicht auf den Broker warten muss.
- Battery- und PV-Telemetrie laufen über begrenzte Latest-Value-Channels und werden im Hintergrund publiziert; ein langsamer oder nicht erreichbarer Broker blockiert weder den ersten Render noch Simulationsschritte.
- RabbitMQ-Befehle werden weiterhin in die jeweilige Simulator-Command-Queue gestellt und im Hauptloop verarbeitet. Worker werden beim Ende des Simulators abgebrochen und abgewartet.
### KI:
GitHub Copilot als IDE-Agent zur Ursachenanalyse und Refaktorierung der asynchronen RabbitMQ-Pfade sowie zur Journal-Dokumentation genutzt; Änderungen werden durch Datei-Diagnostik, Build und Harness geprüft.
### Artefact:
`Battery/Simulation/BatterySimulator.cs`, `Battery/development-journal.md`, `PV_Simulator/PV_Simulator/Simulation/SimulationRunner.cs`.
### Test:
Der vollständige Lösungsbuild war erfolgreich. Der direkte Aufruf des `TeamRabbitMQShared.Tests`-Harnesses mit `dotnet run --no-build` bestand mit 6/6 Tests; Visual Studio Test Explorer unterstützt das Projekt ohne Testadapter/Testhost nicht.
