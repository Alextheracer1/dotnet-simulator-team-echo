# Entwicklungsjournal – Alexander Graf

<!--
Verpflichtende Tags pro Eintrag:
- Done: Was wurde bearbeitet und welches Ergebnis liegt vor?
- KI: Werkzeug, Modell, Einsatzform und Umgang mit dem Ergebnis; bei keiner KI-Nutzung: keine.
- Artefact: Betroffene Dateien, CSV/Kurve, Screenshot, Dokumentation oder andere Ergebnisse.

Optionale Tags bei Relevanz:
- Comment: Entscheidung, Problem, Erkenntnis oder nächster Schritt.
- Test: Durchgeführter manueller oder automatisierter Test.
-->

## 2026-09-28 – Projektsetup und Basis-Simulator

- **Done:** .NET 10 Konsolenanwendung aufgesetzt. Strukturierung in `Program`, `SimulatorEngine` und `ConsoleUI`. Basis-Loop für die Simulationszeit (beschleunigt, 1-Minuten-Schritte) inklusive Tastatureingabe für Start/Stop/Tempo implementiert.
- **KI:** keine.
- **Artefact:** `Program.cs`, `SimulatorEngine.cs`, `ConsoleUI.cs`.
- **Comment:** Die strikte Trennung von UI (`ConsoleUI`) und Simulations-Loop (`SimulatorEngine`) sorgt für sauberen Code und verhindert Flackern in der Konsole.

## 2026-10-02 – Gerätemodellierung und Ladekurven

- **Done:** Fachklassen `Device` und `ChargingPoint` implementiert. Zur realistischen Simulation wurde die Klasse `EVModel` ergänzt, die nicht-lineare Ladekurven mithilfe linearer Interpolation (SOC zu Ladeleistung) abbildet.
- **KI:** Chat-Assistent Gemini als Coding-Hilfe genutzt, um realistische Akku-Größen und Ladekurven-Stützpunkte (Watt bei bestimmten SOC-Werten) für Modelle wie den Porsche Taycan oder Tesla Model Y zu generieren. Werte dann in C#-Logik überführt.
- **Artefact:** `DeviceSimulation.cs`, `EVModel.cs`.
- **Test:** Manueller Testlauf mit einem Tesla Model Y – die Ladeleistung drosselt erwartungsgemäß ab einem Akkustand (SOC) von über 80% deutlich.

## 2026-10-05 – Load Balancing, Betriebsmodi & Logging

- **Done:** Dynamisches Lastmanagement (Aufteilung der max. 300 kW auf beide Ports) implementiert. Auto-Modus (zufälliges Ankommen/Abfahren von Autos) sowie Fehlersimulation (Störung/Reset) eingebaut. CSV-Logging der Zustände angelegt.
- **KI:** KI (Copilot) genutzt, um einen ersten Vorschlag für das faire Aufteilen der Leistung im Lastmanagement (`ApplyDynamicLoadBalancing`) zu entwerfen. Den Code anschließend an die eigene Logik der `ChargingPoints` angepasst.
- **Artefact:** `logs/simulation.csv`, Anpassungen in `DeviceSimulation.cs` und `SimulatorEngine.cs`, Screenshots der Konsole mit aktiven Ladevorgängen und Fehlerzustand.
- **Comment:** Die Zufallsparameter (10% Chance für Ankunft, 5%-20% Start-SOC) führen zu einem sehr realistischen und abwechslungsreichen Log-Verlauf in der CSV-Datei.

## 2026-10-26 – Modbus-TCP-Server (TeamShared), Modbus-Diagnose-Client & Charger-Integration

- **Done:** Modbus-TCP-Kommunikationsinfrastruktur für das gesamte Team aufgebaut:
  - Gemeinsame Klassenbibliothek `TeamShared` mit Modbus-TCP-Server-Wrapper (`FieldDeviceModbusServer` basierend auf EasyModbus) und zentraler Register-Definition `CommonRegisterMap` (Holding Registers 1–12 nach LB02-Standard: OperatingState, ErrorCode, ActivePower, CumulativeEnergy, VirtualTime, OnOffState, Setpoint, ResetError) implementiert.
  - Interaktives Konsolenwerkzeug `ModbusDiagnoseClient` als Test- und Diagnoseanwendung entwickelt, um Register beliebiger Feldgeräte interaktiv abzufragen (Einzelregister, Header, Rohwerte), Setpoints zu schreiben und Fehler zu quittieren.
  - `Dual-Port-Charger` angebunden: Betreibt Modbus-Server auf Port 1502, mappt laufende Simulationswerte (Leistung, kumulierte Energie, Simulationszeit) in die Standardregister und stellt gerätespezifische Register 13 & 14 (SOC Port 1 & 2) bereit.
- **KI:** Google Gemini als Entwurfshilfe für die Konvertierung von 32-Bit-Gleitkommazahlen (Float) und Zeitstempeln in Modbus-16-Bit-Word-Paare (High/Low Word) sowie die Menüführung des Diagnose-Clients.
- **Artefact:** `TeamShared/TeamSharedModbus.cs`, `TeamShared/README.md`, `ModbusDiagnoseClient/DiagnoseClientProgram.cs`, `ModbusDiagnoseClient/README.md`, `Dual-Port-Charger/SimulatorEngine.cs`, `Dual-Port-Charger/DeviceSimulation.cs`.
- **Test:** Erfolgreicher Testlauf des `ModbusDiagnoseClient` gegen den laufenden `Dual-Port-Charger` auf Port 1502: Korrektes Auslesen des gemeinsamen Registerkopfs, Überprüfung der SOC-Werte und erfolgreiches Setzen eines Leistungslimits via Modbus-Schreibbefehl.
- **Comment:** Durch die Auslagerung in `TeamShared` müssen die anderen Simulatoren (PV, Wind, Batterie) den Modbus-Server nicht neu erfinden, sondern binden die Standardbibliothek direkt ein.

## 2026-10-28 – Spectre.Console UI-Library & Dual-Port-Charger Refactoring

- **Done:** Gemeinsame UI-Bibliothek `TeamConsoleShared` mit Spectre.Console implementiert. Umfasst modulare Komponenten (`StatusBadge`, `GaugeBar`, `MetricItem`, `MetricGrid`, `KeyBindingBar`), ein fluentes Layout-System (`SimulationDashboard`, `SimulationHeader`), Live-Display-Unterstützung (`LiveSimulationView`), Modbus-Diagnosetabellen (`RegisterTable`) und eine interaktive Demo (`DashboardDemo`). `Dual-Port-Charger` wurde als Referenzprojekt auf die neue Spectre.Console-Architektur migriert.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Konzeptionierung nach GrillMe-Rücksprache, Erstellung der UI-Komponenten in `TeamConsoleShared`, Integration in `Dual-Port-Charger` sowie Behebung von Terminal-Sonderzeichen und ANSI-Markup-Formatierung.
- **Artefact:** `TeamConsoleShared/Components/*`, `TeamConsoleShared/Layout/*`, `TeamConsoleShared/Live/*`, `TeamConsoleShared/Diagnostics/*`, `TeamConsoleShared/Demo/*`, `Dual-Port-Charger/ConsoleUI.cs`, `Dual-Port-Charger/Dual-Port-Charger.csproj`.
- **Test:** Kompilierung aller Solution-Projekte via `dotnet build` sowie Testausführung des `Dual-Port-Charger` Simulators mit Überprüfung der gerenderten Panels, Gauges und Tastenleisten.
- **Comment:** Die Trennung in wiederverwendbare Bausteine ermöglicht es auch den anderen Simulatoren (PV, Windkraft, Batterie) sowie dem Diagnose-Client, ohne Duplizierung auf dasselbe moderne Terminal-Dashboard aufzusetzen.

## 2026-11-08 – RabbitMQ-Ökosystem, Dual-Port-Charger Anbindung & Konsolen-Harmonisierung

### TeamRabbitMQShared, ModbusRabbitMqGateway & RabbitMqViewer
- **Done:** Robuste RabbitMQ-Messaging-Architektur für das Gesamtsystem aufgebaut:
  - `TeamRabbitMQShared`: Wiederverwendbare Bibliothek mit resilientem `RabbitMqPublisher` und `RabbitMqConsumer` (automatischer Hintergrund-Reconnect, Topic Exchange `sim.exchange`), DTOs (`DeviceTelemetryMessage`, `DeviceCommandMessage`) sowie `ModbusTelemetryMapper` für die Übersetzung von Modbus-Registern in typisierte Telemetriedaten.
  - `ModbusRabbitMqGateway`: Standalone-Gateway-Dienst (Weg A), der Modbus-TCP-Register von Feldgeräten periodisch ausliest, als Telemetrie über RabbitMQ publiziert und Steuerbefehle (`SetPowerLimit`, `ResetFault`) als Modbus-Schreibbefehle an die Geräte weiterleitet.
  - `RabbitMqViewer`: Interaktive Live-Monitoring-Konsolenapplikation mit Spectre.Console; abonniert `sim.telemetry.#` und visualisiert Sensordaten aller Feldgeräte in Echtzeit.
  - `TeamRabbitMQShared.Tests`: Unit- und Integrationstests für Telemetrie-Mapping, Command-Handling und Routing-Keys.
  - Guidelines & Workflows: Definition der Messaging-Standards in `AGENTS.md` sowie Skills (`skills/rabbitmq-messaging`, `skills/modbus-rabbitmq-gateway`, `skills/rabbitmq-viewer`, `skills/build-and-test`, `skills/run-simulators`).
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Konzeptionierung des fehlertoleranten Verbindungsmanagements mit Resilienzschleife, Definition der standardisierten Routing-Key-Hierarchie sowie Generierung der DTOs, Mappings und Unit-Tests.
- **Artefact:** `TeamRabbitMQShared/Publisher/*`, `TeamRabbitMQShared/Consumer/*`, `TeamRabbitMQShared/Models/*`, `TeamRabbitMQShared/Helpers/ModbusTelemetryMapper.cs`, `TeamRabbitMQShared/README.md`, `ModbusRabbitMqGateway/*`, `RabbitMqViewer/*`, `TeamRabbitMQShared.Tests/*`, `AGENTS.md`, `skills/*`.
- **Test:** Alle 9 Unit- & Integrationstests in `TeamRabbitMQShared.Tests` erfolgreich ausgeführt (`PASSED ✅`); Verifikation im Live-Betrieb mit laufendem RabbitMQ Broker, Gateway und `RabbitMqViewer`.
- **Comment:** Durch die Trennung in `TeamRabbitMQShared` können sowohl Gateways (Weg A) als auch Feldgeräte mit nativer RabbitMQ-Anbindung (Weg B) dieselben standardisierten Schnittstellen nutzen.

### Dual-Port-Charger – RabbitMQ-Pub/Sub & Simulations-Master-Integration
- **Done:** `Dual-Port-Charger` um direkte RabbitMQ-Anbindung und Master-Zeitsynchronisation erweitert:
  - Asynchroner Telemetrie-Publisher (`RabbitMqPublisher`): Sendet periodisch Zustandsdaten auf `sim.telemetry.charger.1` (Leistung, kumulierte Energie, Status, SOC Port 1 & 2).
  - Remote-Command-Consumer (`RabbitMqConsumer`): Abonniert `sim.commands.charger.#` und verarbeitet asynchron `SetPowerLimit` (dynamische Drosselung), `ResetFault` (Fehlerquittierung) und `ToggleCar` (Fahrzeugsimulation).
  - Anbindung an den zentralen `Simulations-Master` via `SimulationMasterClient` (Port 1510) zur Zeitsynchronisation (`VirtualTime`), Step-Handshake und nahtlosem Fallback bei Verbindungsverlust.
  - Erweiterung des Dashboards (`ConsoleUI`) um Verbindungs- und Telemetriestatus (RabbitMQ-Verbindung & gesendete Nachrichten, Master-Status).
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Integration des asynchronen Publisher- und Consumer-Handlings in die Simulationsschleife und thread-sichere Abstimmung von Modbus-Server, RabbitMQ und Master-Client.
- **Artefact:** `Dual-Port-Charger/SimulatorEngine.cs`, `Dual-Port-Charger/Program.cs`, `Dual-Port-Charger/ConsoleUI.cs`, `Dual-Port-Charger/Dual-Port-Charger.csproj`.
- **Test:** Manueller Funktionstest: Senden von `SetPowerLimit` via RabbitMQ-Kommando – Station passt die Ladeleistung dynamisch an; Test des Master-Syncs und des Fallbacks bei getrennter Master-Verbindung.
- **Comment:** Der Charger bedient gleichzeitig Modbus TCP (Port 1502), RabbitMQ (Pub/Sub) und den Master (Port 1510), bleibt dabei aber bei Ausfällen einzelner Kommunikationskanäle autark und stabil.

### TeamConsoleShared – SimulationTheme & Konsolen-Harmonisierung
- **Done:** Zentrales Theming-System `SimulationTheme` in `TeamConsoleShared` implementiert und auf alle Konsolenapplikationen ausgerollt:
  - Einheitliches Farbschema (Primär-, Sekundär- und Akzentfarben für PV, Wind, Batterie, Charger, Master, Gateway, Diagnose), konsistente Status-Farben, Box-Borders und Unicode-Icons.
  - Harmonisierung aller Konsolenprojekte des Teams (`Dual-Port-Charger`, `Battery`, `PV_Simulator`, `WindTurbine_Simulator`, `ModbusDiagnoseClient`, `RabbitMqViewer`, `Simulations-Master`) auf das gemeinsame Theme.
  - Behebung von Spectre.Console Markup-Parsing-Fehlern (Escaping von `[[...]]`) und ANSI-Kompatibilitätsproblemen in verschiedenen Terminalumgebungen.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Entwurf von `SimulationTheme.cs` und Refactoring der Rendering-Klassen über alle Solution-Projekte hinweg.
- **Artefact:** `TeamConsoleShared/SimulationTheme.cs`, `ModbusDiagnoseClient/DiagnoseClientProgram.cs`, `RabbitMqViewer/Program.cs`, `Simulations-Master/Program.cs`, `Battery/ConsoleUi/ConsoleDashboard.cs`, `PV_Simulator/PV_Simulator/ConsoleApp/ConsoleStatusRenderer.cs`, `WindTurbine_Simulator/ConsoleApp/ConsoleStatusRenderer.cs`.
- **Test:** Erfolgreicher Clean-Build der gesamten Solution (`dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1`) mit 0 Fehlern; Start-Checks der Konsolenanwendungen zur Bestätigung einheitlicher Kopfzeilen, Metriken und Statusanzeigen.
- **Comment:** Alle Simulatoren und Tools treten nun mit einem einheitlichen visuellen Erscheinungsbild auf, was die Bedienung und das Debugging im Team deutlich vereinfacht.
