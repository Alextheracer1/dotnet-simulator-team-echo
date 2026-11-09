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

## 2026-11-09 – WeatherSimulator: Integration des einheitlichen Konsolendesigns (TeamConsoleShared)

### WeatherSimulator – Spectre.Console Live-Dashboard & Status-Harmonisierung
- **Done:** Vollständige Integration des einheitlichen Konsolendesigns (`TeamConsoleShared`) in den `WeatherSimulator`:
  - `TeamConsoleShared`: `SimulationTheme.WeatherAccent = Color.SkyBlue1` hinzugefügt und Subtitle-Handling in `SimulationHeader` für Wetter-Provider optimiert.
  - `WeatherConsoleDashboard`: Neues Spectre.Console Live-Dashboard implementiert mit:
    - Standardisiertem `SimulationHeader` (Port 5005, Sim-Zeit, Master-Takt, Status-Badges `[HTTP :5005]` und `[MASTER ONLINE/OFFLINE]`).
    - Responsivem 2-Spalten-Layout (`MetricGrid`): Aktuelle Wetter- & Sonnendaten am Referenzstandort Salzburg (Temperatur, Bewölkungs-`GaugeBar`, Windgeschwindigkeit, Niederschlag, Azimut & Elevation mit Tag/Nacht-Indikator) sowie Dienst-Status & Zeitsynchronisation (Virtuelle Zeit, Master-Sync, CSV-Stützstellen, registrierte Feldgeräte, GET- & Push-Zähler).
    - Status-Tabelle für registrierte Feldgeräte (`pv`, `wind`, Callback-URLs, Geodaten, letzter Push-Status und Zeitabstand).
    - Ereignis- und Aktivitäts-Log-Panel für eingehende Requests, Registrierungen und Push-Benachrichtigungen.
    - Keybinding-Leiste (`Q / X` zum Beenden, `R` für Aktualisierung).
  - Resilientes Verbindungs- und Logging-Management:
    - `WeatherActivityLogger`: Thread-sicherer Service zur Erfassung von Aktivitäten, HTTP-GET-Anfragen und Push-Events.
    - Entkoppelung des Master-Verbindungsaufbaus (`WeatherSimulationMasterWorker` blockiert nicht mehr den WebHost-Start, sondern verbindet sich resilient im Hintergrund mit Reconnect-Schleife und Fallback-Uhr).
    - Unterdrückung von ASP.NET Core Konsolen-Noise (`builder.Logging.ClearProviders()`), um flackerfreies Terminal-Rendering zu garantieren.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Konzeptionierung des Live-Dashboards unter ASP.NET Core Kestrel, Implementierung der UI-Komponenten und thread-sicheren Schnittstellen in `TeamConsoleShared`.
- **Artefact:** `TeamConsoleShared/SimulationTheme.cs`, `TeamConsoleShared/Layout/SimulationHeader.cs`, `WeatherSimulator/WeatherSimulator.csproj`, `WeatherSimulator/ConsoleUi/WeatherConsoleDashboard.cs`, `WeatherSimulator/Services/WeatherActivityLogger.cs`, `WeatherSimulator/Services/WeatherClientRegistry.cs`, `WeatherSimulator/Services/WeatherSnapshotPusher.cs`, `WeatherSimulator/Services/WeatherSimulationClock.cs`, `WeatherSimulator/Services/WeatherSimulationMasterWorker.cs`, `WeatherSimulator/Services/WeatherSimulationMasterClient.cs`, `WeatherSimulator/Program.cs`, `AGENTS.md`.
- **Test:** Vollständiger Build der Solution (`dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1`) mit 0 Fehlern; Start des `WeatherSimulator` im Terminal, Überprüfung des Live-Renderings, der Responsive-Boxen und des sauberen Shutdowns bei `Q`/`Ctrl+C`.
- **Comment:** Durch die Entkoppelung des Master-Verbindungsaufbaus vom HTTP-Start startet der WeatherSimulator nun verzögerungsfrei auch dann, wenn der Simulations-Master noch nicht läuft, und blendet transparent den aktuellen Verbindungsstatus ein.

### WeatherSimulator – Behebung von Flackern, Textsprüngen & doppelten Elementen
- **Done:** Terminal-Flicker und vertikales Scrollen im `WeatherSimulator` vollständig behoben:
  - Layout-Höhe auf feste 14–17 Zeilen optimiert: Die Metrikwerte in den beiden Spalten wurden kompaktiert (keine mehrzeiligen Umbrüche mehr bei Zeitanzeigen oder Koordinaten), die Alert-Meldung gekürzt und die Detailtabelle dynamisch an die Terminalhöhe gekoppelt (`WindowHeight >= 32`). Dadurch bleibt der Frame immer deutlich unter der Standardterminalhöhe von 24 Zeilen.
  - Flackerfreies In-Place-Rendering: Das periodische `Console.Clear()` auf jedem Sekundentakt wurde durch eine intelligente Zustandserkennung (`layoutKey`) ersetzt. Ein vollständiger Puffer-Clear wird nur noch exakt einmal bei strukturellen Layout-Änderungen (Start, Fenster-Resize, Wechsel des Master-Alert-Status) ausgeführt. Während der normalen Simulationsticks wird der Frame flackerfrei per `Console.SetCursorPosition(0, 0)` in-place überschrieben.
  - Render-Loop harmonisiert: Die Aktualisierungsfrequenz läuft im 1-Sekunden-Takt mit 50 ms Tastenabfrage und Resize-Erkennung (analog zu `Simulations-Master`).
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Diagnose des Buffer-Overflows und Umbruchverhaltens in Spectre.Console, Implementierung der differenziellen Puffer-Clear-Logik zur Beseitigung von Sekundärflackern.
- **Artefact:** `TeamConsoleShared/SimulationTheme.cs`, `TeamConsoleShared/Layout/SimulationDashboard.cs`, `TeamConsoleShared/Layout/SimulationHeader.cs`, `WeatherSimulator/ConsoleUi/WeatherConsoleDashboard.cs`, `WeatherSimulator/Program.cs`.
- **Test:** Kompilierung und Verifikation des Dashboards; der Render-Frame bleibt stabil bei 17 Zeilen ohne Zeilenüberlauf, ohne vertikales Springen und ohne Sekundärflackern.
- **Comment:** Ein ständiges `Console.Clear()` auf jedem Frame führt zu sichtbarem Flackern, da das Terminal für Millisekunden schwarz wird. Durch die Entkoppelung (Clear nur bei struktureller Höhenänderung, sonst sauberes In-Place-Overwrite) vereint die Lösung absolute Artefaktfreiheit mit flüssiger, ruhiger Terminaldarstellung.

### KI-Agenten Onboarding- & Skill-Dokumentation (docs/ai-agent-onboarding.md)
- **Done:** Umfassenden Onboarding- und Schnellstart-Leitfaden für neue autonome KI-Agenten und Entwickler-LLMs konzipiert und implementiert (`docs/ai-agent-onboarding.md`):
  - Kompakte Gesamtsystem- und Architekturübersicht des Digital-Twin-Systems Energy Twin 2026 (PV, Windkraft, Großbatterie, Dual-Port-Charger, Simulations-Master, WeatherSimulator, Gateway, Viewer).
  - Spezifikation der vier Kommunikationsschichten (TCP-Masterzeit & Zeitschritt-Barriere via `ITimeMasterClient`, HTTP-Wetterpush & dynamische Callbacks, Modbus-TCP-Registerlayout nach LB02, RabbitMQ-Pub/Sub-Messaging über `sim.exchange`).
  - Vollständige Modul-, Port- und Routing-Key-Matrix aller beteiligten Applikationen.
  - Detaillierte Dokumentation und Handlungsanweisungen aller 6 projektspezifischen Agent-Skills (`build-and-test`, `development-journal`, `run-simulators`, `modbus-rabbitmq-gateway`, `rabbitmq-messaging`, `rabbitmq-viewer`).
  - Verbindliche Ausführungsregeln für macOS/Sandbox (`DOTNET_CLI_HOME=/tmp/dotnet_home`, `-m:1`) und strukturierte Checkliste für anstehende Entwicklungsaufgaben.
  - Verlinkung und Einbindung in die zentrale Dokumentationsübersicht `docs/README.md`.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Strukturierte Synthese der Architektur- und Skill-Vorgaben aus `PROJECT_CONTEXT.md`, `AGENTS.md` und `.agents/skills/*` in ein einheitliches, handlungsorientiertes Handbuch.
- **Artefact:** `docs/ai-agent-onboarding.md`, `docs/README.md`.
- **Test:** Kompilierung aller Solution-Projekte via `DOTNET_CLI_HOME=/tmp/dotnet_home /usr/local/share/dotnet/dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1` erfolgreich verifiziert (0 Fehler).
- **Comment:** Neue Agenten oder Subagents erhalten durch das Dokument sofort den nötigen Kontext, um ohne zeitaufwendiges Suchen oder Fehlannahmen zielgerichtet Builds, Simulatoren, Gateways und Journaleinträge nach Projektstandard zu bedienen.

### Konsolidierung der Agenten-Richtlinien & Onboarding-Dokumentation (AGENTS.md)
- **Done:** Zusammenführung der bisherigen `AGENTS.md` und des Onboarding-Handbuchs `docs/ai-agent-onboarding.md` zu einer zentralen, allumfassenden Referenzdatei `AGENTS.md` im Root-Verzeichnis des Projekts:
  - Vollständige Architektur- und Protokollübersicht (TCP-Master-Barriere, HTTP-Wetter-Push, Modbus-TCP nach LB02, RabbitMQ pub/sub).
  - Umfassende Modul-, Port- und Routing-Key-Matrix aller 13 Projekte/Module.
  - Exaktes LB02 Holding-Registerlayout (1–12) inklusive Datentypen, High/Low-Word-Aufteilung und gerätespezifischen Registern (13+).
  - Detaillierte RabbitMQ-Messaging-Konventionen (Topic-Exchange `sim.exchange`, DTOs `DeviceTelemetryMessage` & `DeviceCommandMessage`, Resilienzregeln).
  - Vollständiges Agent-Skill-Handbuch (`.agents/skills/`) mit Ausführungsbefehlen und Journal-Pflicht-Tags.
  - Entfernung der redundanten Zwischendatei `docs/ai-agent-onboarding.md` und Aktualisierung der Dokumentationsübersicht `docs/README.md`.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Redundanzfreie Synthese beider Leitfäden in eine konsistente Dokumentenstruktur im Root-Verzeichnis.
- **Artefact:** `AGENTS.md`, `docs/README.md`, `docs/development-journal/Alexander-Graf.md`.
- **Test:** Prüfung der Markdown-Links und Verifikation von Konsistenz und Formatierung der zusammengeführten Datei.
- **Comment:** Durch die Konsolidierung im Root-Verzeichnis (`AGENTS.md`) finden sowohl Entwickler als auch autonome KI-Agenten alle relevanten Standards, Ports, Protokolle und Skills an einem einzigen, standardisierten Einstiegspunkt.

### Interaktives Projekt-Auswahlmenü für Startup-Skripte (start-all)
- **Done:** Startup-Skripte für macOS/Linux (`scripts/start-all.sh`, `start-all.sh`) und Windows (`scripts/start-all.ps1`, `scripts/start-all.bat`, `start-all.ps1`) um ein interaktives Terminal-Auswahlmenü (TUI) erweitert:
  - Navigation und Steuerung: Auswahlzeiger bewegt sich mit den Pfeiltasten (↑/↓ sowie Vim-Keys k/j), Projekte werden mit der Leertaste an- oder abgewählt (`[X]` / `[ ]`).
  - Schnellfunktionen: 'a' / 'A' schaltet alle Komponenten gleichzeitig an/ab, Zifferntasten (1–9) erlauben die direkte Umschaltung eines Moduls.
  - Start & Abbruch: Drücken von `Enter` oder `S` startet in geordneter Reihenfolge ausschließlich die ausgewählten Projekte; `Q` bricht den Vorgang sauber ab.
  - Vollständige Projektliste: Alle 9 lauffähigen Projekte der Solution sind aufgeführt (die 8 Kernsimulationsdienste sind standardmäßig vorausgewählt, der interaktive `ModbusDiagnoseClient` kann optional zugeschaltet werden).
  - Flackerfreie Terminal-Darstellung: In-Place-Redraw via ANSI-Cursorsteuerung (vollständig abwärtskompatibel zu macOS Bash 3.2 ohne externe Abhängigkeiten) mit automatischer Cursor-Wiederherstellung bei Exit/Ctrl+C.
  - CLI-Unterstützung: Flags `--all` / `-y` sowie nicht-interaktive Shells (Pipes/CI) starten direkt ohne Menüabfrage.
- **KI:** Google Gemini 3.8 Flash (High) via Antigravity Assistant. Konzeptionierung des portablen TUI-State-Handlings unter Bash 3.2 (macOS) und PowerShell (Windows), PTY-Simulationstests für Tastenabfragen (Escape-Sequenzen für Pfeiltasten, Leertaste, Enter) und fehlerfreie Terminal-Synchronisation.
- **Artefact:** `scripts/start-all.sh`, `scripts/start-all.ps1`, `scripts/start-all.bat`, `start-all.sh`, `start-all.ps1`, `docs/development-journal/Alexander-Graf.md`.
- **Test:** PTY-basierte Integrationstests für Pfeiltasten-Navigation, Checkbox-Toggle per Leertaste, Direktwahl und Start-/Abbruch-Verhalten unter macOS; Syntaxvalidierung mit `bash -n` und Parameterüberprüfung mit `--help`.
- **Comment:** Entwickler können nun für gezielte Tests (z. B. isoliertes Debugging von Master + Charger oder PV + WeatherSimulator) genau die gewünschten Subsysteme hochfahren, ohne manuell Befehle in mehreren Konsolentabs ausführen zu müssen.

