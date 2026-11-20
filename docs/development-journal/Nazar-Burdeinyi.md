
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

## 2026-11-09 – RabbitMQ-Status im Battery-Dashboard

### Done:
- RabbitMQ-Verbindungsstatus und Anzahl erfolgreich publizierter Telemetrie-Nachrichten im Battery-Dashboard ergänzt, analog zur Dual-Port-Charger-Anzeige.
### KI:
GitHub Copilot als IDE-Agent zur Ergänzung der Statusanzeige und Aktualisierung des Journals genutzt; Änderungen werden durch Datei-Diagnostik und Projektbuild geprüft.
### Artefact:
`Battery/Simulation/BatterySimulator.cs`, `Battery/ConsoleUi/ConsoleDashboard.cs`, `Battery/development-journal.md`.
### Test:
`Battery/Battery.csproj` und `Dual-Port-Charger/Dual-Port-Charger.csproj` wurden erfolgreich erstellt; Datei-Diagnostik für alle geänderten C#-Dateien ist fehlerfrei.

## 2026-11-09 – Master-Zeitabgleich nach Verbindungswiederherstellung

### Done:
- Lokale Battery-Zeit wird während fehlender Master-Verbindung weitergezählt und beim Wiederverbinden an die vom Master bestimmte Zeit angepasst.
- Der Master sammelt für fünf Master-Ticks Zeitstempel zuvor registrierter Battery- und Charger-Stationen und übernimmt den exakten Median; bei gerader Anzahl gilt der Mittelwert ausschließlich der beiden mittleren Werte.
- Battery und Dual-Port-Charger setzen bei `TimeReconciliation` ausschließlich ihre virtuelle Uhr zurück. Modellzustand und kumulierte Gerätewerte bleiben unverändert. PV-Dateien wurden nicht geändert und PV-Stationen sind von der Median-Reconciliation ausgeschlossen.
### KI:
GitHub Copilot als IDE-Agent zur Analyse und Implementierung des Reconnect-Protokolls sowie Aktualisierung der Dokumentation genutzt; Änderungen werden durch Builds der betroffenen Projekte und den vorhandenen Harness geprüft.
### Artefact:
`Battery/Simulation/BatterySimulator.cs`, `Battery/Simulation/SimulationMasterClient.cs`, `Dual-Port-Charger/SimulatorEngine.cs`, `Dual-Port-Charger/Program.cs`, `TeamShared/SimulationProtocol.cs`, `TeamShared/SimulationMasterClient.cs`, `TeamShared/SimulationMaster-Client-Protocol.md`, `Simulations-Master/TcpSimulationServer.cs`, `Simulations-Master/SimulationEngine.cs`.
### Test:
Der vollständige Lösungsbuild war erfolgreich; es verbleiben zwei vorhandene `MSB3245`-Warnungen zur nicht gefundenen `EasyModbus`-Assembly. Der `TeamRabbitMQShared.Tests`-Harness wurde mit 6/6 erfolgreichen Tests ausgeführt. Spezifische Reconnect-Protokoll-/Runtime-Tests sind nicht vorhanden.

## 2026-11-09 – Master-Tick-Tempo im Battery-Dashboard

### Done:
- Die Tempoanzeige zeigt die vom Simulation Master gelieferte Schrittweite im Format `1 Tick = ...` und passt die Einheit dynamisch an Sekunden, Minuten, Stunden und Tage an.
### KI:
GitHub Copilot zur Analyse und Implementierung der Dashboard-Anpassung sowie zur Aktualisierung des Journals genutzt.
### Artefact:
`Battery/Simulation/BatterySimulator.cs`, `Battery/ConsoleUi/ConsoleDashboard.cs`, `TeamConsoleShared/SimulationTimeFormatter.cs`, `Battery/development-journal.md`.

## 2026-11-09 – Master-Zeitschrittabgleich (TimeStepReconciliation) nach Verbindungswiederherstellung

### Done:
- Analog zum Zeitabgleich (`TimeReconciliation`) wurde ein Zeitschrittabgleich (`TimeStepReconciliation`) implementiert:
  - Bei der Wiederregistrierung übermitteln Battery und Dual-Port-Charger ihre zuletzt bekannten Zeitschrittweiten (`TimeStep`).
  - Der Simulations-Master sammelt diese Schrittweiten über fünf Ticks und ermittelt den exakten Median.
  - Der Master setzt seine Simulations-Schrittweite auf den ermittelten Median und sendet `TimeStepReconciliation` an Battery und Dual-Port-Charger.
  - Battery und Dual-Port-Charger passen ihre lokale Simulationsschrittweite an den reconcilierten Wert an, ohne das physische Modell zurückzusetzen.
### KI:
Google Gemini 3.8 Flash via Antigravity Assistant. Konzeptionierung basierend auf dem `TimeReconciliation`-Muster, Anpassung von Protokoll und Master/Client-Logik.
### Artefact:
`Battery/Simulation/BatterySimulator.cs`, `Battery/Simulation/SimulationMasterClient.cs`, `Dual-Port-Charger/SimulatorEngine.cs`, `TeamShared/SimulationProtocol.cs`, `TeamShared/SimulationMasterClient.cs`, `TeamShared/SimulationMaster-Client-Protocol.md`, `Simulations-Master/TcpSimulationServer.cs`, `Battery/development-journal.md`.
### Test:
Builds für `Battery`, `Dual-Port-Charger`, `Simulations-Master` und `TeamShared` via `dotnet build -m:1`; Ausführung aller Tests im Test-Harness.

## 2026-11-20 – Erstellung aussagekräftiger xUnit-Tests für Battery und Simulation Master

### Done:
- Erstellung der dedizierten Test-Suite `Battery.Tests`:
  - `BatteryModelTests.cs`: Validierung von Konstruktor-Grenzwerten, Lade- und Entladephysik, Leistungs-Clamping an Hardware-Grenzen, 80%-SoC-Tapering-Kurve, Top-Off-Schwellenwert (99,9%), Verhinderung von Überladung/Tiefentladung, Fehler-Injektion (`InjectFault`) und Fehler-Reset (`ResetFault`).
  - `AutoScenarioTests.cs`: Prüfung der zeitabhängigen Ladeleistung (Off-Peak vs. Solarfenster), 60-Minuten-Rampen-Interpolation, Fahrzeug-Konnektivitätsfenster (18:00–01:00) und dynamische Geräteprofil-Auswahl (Hochlast- vs. Schwachlastphasen).
  - `SimulationHistoryTests.cs`: Validierung der FIFO-Gleitfenstergrenze (`RollingReadingLimit = 96`), Berechnung rollierender Durchschnitte (Min/Max/Avg) sowie Tages-Aggregationen.
- Erstellung der dedizierten Test-Suite `Simulations-Master.Tests`:
  - `SimulationEngineTests.cs`: Zustandsübergänge (Start, Pause, Resume), Zeitschritt-Konfiguration (`ChangeTimeStep`), Validierung nicht-unterstützter Restarts, Bestimmung der nächsten Zeitschritt-Intervalle (`GetNearestTimeStep`), Commit-Validierung des Zeitschritts (`CommitTimeStep`) und Master-Uhr-Reconciliation (`ReconcileFromStation`).
  - `TcpSimulationServerTests.cs`: Automatisierte Netzwerk- und Protokolltests für den TCP-Master: Environment-Broadcasting bei Verbindung, Beantwortung von `EnvironmentRequest`, Station-Registrierung (`Register`), Fehlerbehandlung bei ungültigem JSON (`Error`-Antwort) und Zeitschritt-Barrieren-Synchronisation (`TimeStep` Broadcast mit Client `Acknowledgement` und Weiterführung der virtuellen Zeit).
- Einbindung beider Testprojekte in die Hauptlösung `dotNET_Programmierung_2026_Aufgabe_02.slnx`.
### KI:
Google Gemini 3.8 Flash via Antigravity Assistant zur Konzeptionierung, Generierung und Verifikation der xUnit-Testsuiten unter strikter Einhaltung der xUnit- und Spectre/Console-Runner-Standards des Repositories.
### Artefact:
`Battery.Tests/Battery.Tests.csproj`, `Battery.Tests/BatteryModelTests.cs`, `Battery.Tests/AutoScenarioTests.cs`, `Battery.Tests/SimulationHistoryTests.cs`, `Battery.Tests/Program.cs`, `Simulations-Master.Tests/Simulations-Master.Tests.csproj`, `Simulations-Master.Tests/SimulationEngineTests.cs`, `Simulations-Master.Tests/TcpSimulationServerTests.cs`, `Simulations-Master.Tests/Program.cs`, `dotNET_Programmierung_2026_Aufgabe_02.slnx`, `docs/development-journal/Nazar-Burdeinyi.md`.
### Test:
- `Battery.Tests`: 34/34 Tests erfolgreich bestanden (`dotnet run --project Battery.Tests/Battery.Tests.csproj --no-build`).
- `Simulations-Master.Tests`: 19/19 Tests erfolgreich bestanden (`dotnet run --project Simulations-Master.Tests/Simulations-Master.Tests.csproj --no-build`).
- Gesamter Lösungsbuild (`dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1`) erfolgreich ohne Compilerfehler.
