## 2026-10-26

### WindTurbine Simulator – Dauer/Zeitschritt
Done: WindTurbinen-Simulator auf PV-ähnliche Laufzeitlogik mit 24h-Default, freiem Zeitschritt, Tagesprofil+Rauschen und CSV-Ausgabe umgestellt.

KI: Github Copilot GPT-6 Luna => Struktur und Ablauf für WindTurbine_Simulator gemäß neuen Parametern angepasst.

Artefacts: WindTurbine_Simulator\Configuration\SimulationOptions.cs, WindTurbine_Simulator\Simulation\WindTurbineSimulationRunner.cs, WindTurbine_Simulator\ConsoleApp\ConsoleUi.cs, WindTurbine_Simulator\Program.cs, WindTurbine_Simulator\Simulation\CsvSimulationLogger.cs

### WindTurbine Simulator – Laufzeitsteuerung
Done: Zeitraffer im laufenden Betrieb mit +/- ergänzt und Pause/Fortsetzen auf P eingeführt.

KI: Github Copilot GPT-6 Luna => Runtime-Steuerung analog PV-Simulator im Simulationsrunner umgesetzt.

Artefacts: WindTurbine_Simulator\Simulation\WindTurbineSimulationRunner.cs, WindTurbine_Simulator\ConsoleApp\ConsoleUi.cs

### TeamConsoleShared – Einheitlicher Ausgabestil
Done: Gemeinsame Console-Rendering-Assembly eingeführt und PV/Wind auf kompakten Blockstil mit Leistungsbalken vereinheitlicht.

KI: Github Copilot GPT-6 Luna => Shared Renderer erstellt und beide Simulatoren auf wiederverwendete Ausgabe umgestellt.

Artefacts: TeamConsoleShared\ConsoleStatusBox.cs, TeamConsoleShared\PowerBarFormatter.cs, PV_Simulator\PV_Simulator\ConsoleApp\ConsoleStatusRenderer.cs, WindTurbine_Simulator\ConsoleApp\ConsoleStatusRenderer.cs, WindTurbine_Simulator\ConsoleApp\ConsoleUi.cs, WindTurbine_Simulator\Simulation\WindTurbineSimulationRunner.cs, WindTurbine_Simulator\Program.cs


## 2026-10-29

### WeatherSimulator – Grundgerüst, CSV-Parser, Sonnenstand & REST-API
Done: WeatherSimulator als Minimal API auf Port 5005 mit CSV-In-Memory-Parser, Haversine-Standortwahl, Jahres-Wrapping, linearer Wetter-Interpolation und astronomischer Sonnenstandsberechnung implementiert.

KI: Github Copilot GPT-6 Luna => WeatherSimulator-Architektur abstimmen, Minimal API Endpunkte, CSV-Parser und astronomischen Berechnungsservice umsetzen.

Artefacts: WeatherSimulator\Program.cs, WeatherSimulator\Services\WeatherService.cs, WeatherSimulator\Services\IWeatherService.cs, WeatherSimulator\Services\SolarPositionCalculator.cs, WeatherSimulator\Services\ISolarPositionCalculator.cs, WeatherSimulator\Services\WeatherCsvParser.cs, WeatherSimulator\Services\IWeatherCsvParser.cs, WeatherSimulator\Models\WeatherRecord.cs, WeatherSimulator\Models\SolarPosition.cs, WeatherSimulator\Models\WeatherResponse.cs, WeatherSimulator\Models\WeatherDataset.cs, WeatherSimulator\WeatherSimulator.csproj

## 2026-11-08

### Simulation Master – Wind-/Weather-Anbindung
Done: Windturbine verarbeitet Master-Zeitschritte mit ACK und Offline-Fallback; WeatherSimulator startet HTTP erst nach gültiger Masterzeit.

KI: Github Copilot GPT-6 Luna => Master-Interface in Wind und Weather integriert und Fallback/Resynchronisierung ergänzt.

Artefacts: WindTurbine_Simulator\Program.cs, WindTurbine_Simulator\Simulation\WindTurbineSimulationRunner.cs, WeatherSimulator\Services\WeatherSimulationMasterWorker.cs

### Weather Push & Feldgeräte-Integration
Done: Wetter-Push via HTTP POST vom WeatherSimulator an PV und Windrad integriert, dynamische Callback-Ports (6000+/6100+) implementiert, Master-ACK-Reihenfolge in PV korrigiert und Modellberechnungen auf zentrale Wetterdaten umgestellt.

KI: Github Copilot GPT-6 Luna => Push-/Registrierungslogik im WeatherSimulator, dynamischen Callback-Empfänger in TeamShared sowie Anbindung in PV- und Windsimulatoren umsetzen.

Artefacts: TeamShared\Weather\DeviceWeatherRegistration.cs, TeamShared\Weather\WeatherSnapshotDto.cs, TeamShared\Weather\WeatherCallbackReceiver.cs, WeatherSimulator\Services\WeatherClientRegistry.cs, WeatherSimulator\Services\WeatherSnapshotPusher.cs, WeatherSimulator\Services\WeatherSimulationMasterWorker.cs, WeatherSimulator\Program.cs, WindTurbine_Simulator\Configuration\SimulationOptions.cs, WindTurbine_Simulator\Simulation\WindTurbineSimulationRunner.cs, WindTurbine_Simulator\Program.cs, PV_Simulator\PV_Simulator\Simulation\SimulationEngine.cs, PV_Simulator\PV_Simulator\Simulation\SimulationRunner.cs, PV_Simulator\PV_Simulator\Program.cs, TeamRabbitMQShared.Tests\WeatherIntegrationTests.cs, TeamRabbitMQShared.Tests\Program.cs

### PV Modbus-Server & RabbitMQ-Gateway Mapping (Weg A)
Done: PV-Simulator mit Modbus-TCP-Server (Port 1504, LB02-Standard) ausgestattet und ModbusRabbitMqGateway sowie ModbusTelemetryMapper um gerätespezifische PV- und Wind-Metriken erweitert.

KI: Github Copilot GPT-6 Luna => PvModbusService implementieren, ModbusTelemetryMapper für PV und Wind erweitern, GatewayConfiguration und RabbitMqViewer anpassen.

Artefacts: PV_Simulator\PV_Simulator\Modbus\PvModbusService.cs, PV_Simulator\PV_Simulator\Simulation\SimulationRunner.cs, PV_Simulator\PV_Simulator\Program.cs, WindTurbine_Simulator\Configuration\SimulationOptions.cs, WindTurbine_Simulator\Modbus\WindTurbineModbusService.cs, TeamRabbitMQShared\Helpers\ModbusTelemetryMapper.cs, ModbusRabbitMqGateway\GatewayConfiguration.cs, RabbitMqViewer\Program.cs, TeamRabbitMQShared.Tests\TelemetryAndMappingTests.cs, TeamRabbitMQShared.Tests\Program.cs

### Spectre.Console Markup-Escaping & Clean Build
Done: Spectre.Console Markup-Tags [Modbus] und [Wetter] durch doppelte Klammern [[...]] maskiert und veraltete Assembly-Referenzen entfernt.

KI: Github Copilot GPT-6 Luna => Spectre-Markup-Parserfehler in Wind- und PV-Simulatoren beheben und Projektdateien bereinigen.

Artefacts: WindTurbine_Simulator\Program.cs, PV_Simulator\PV_Simulator\Program.cs, TeamShared\TeamShared.csproj, ModbusDiagnoseClient\ModbusDiagnoseClient.csproj

### PV-Simulator – Unbegrenzte Laufzeit im Master-Betrieb
Done: 24h-Begrenzung im PV-Simulator aufgehoben, Simulationsdauer optional (nullable) gestaltet und Simulationsschleife auf unbegrenzten Master-Betrieb umgestellt.

KI: Github Copilot GPT-6 Luna => SimulationSettings, SimulationEngine, SimulationRunner und ConsoleStatusRenderer auf unbegrenzte Laufzeit analog Windrad anpassen.

Artefacts: PV_Simulator\PV_Simulator\Domain\SimulationSettings.cs, PV_Simulator\PV_Simulator\Simulation\SimulationEngine.cs, PV_Simulator\PV_Simulator\Simulation\SimulationRunner.cs, PV_Simulator\PV_Simulator\ConsoleApp\ConsoleStatusRenderer.cs, PV_Simulator\PV_Simulator\ConsoleApp\ConsoleInput.cs, PV_Simulator\PV_Simulator\Program.cs

### WindTurbine-Simulator – Standardmäßig unbegrenzte Laufzeit
Done: Standard-Simulationsdauer im Windrad auf unbegrenzt (inf / null) gesetzt, um vorzeitigen Abbruch nach 24h im Master-Betrieb zu verhindern.

KI: Github Copilot GPT-6 Luna => SimulationOptions und ConsoleUi Default-Duration auf unbegrenzt umstellen.

Artefacts: WindTurbine_Simulator\Configuration\SimulationOptions.cs, WindTurbine_Simulator\ConsoleApp\ConsoleUi.cs

## 2026-11-09 – Direkte RabbitMQ-Integration für PV und Windenergieanlage

### PV & WindTurbine Simulator – Native RabbitMQ Publisher & Consumer Anbindung
- **Done:** Direkte, brokerresiliente RabbitMQ-Anbindung für `PV_Simulator` und `WindTurbine_Simulator` analog zu `Dual-Port-Charger` und `Battery` implementiert. Beide Simulatoren publizieren bei jedem Simulationsschritt standardisierte Telemetriedaten (`DeviceTelemetryMessage`) auf den Exchange `sim.exchange` (`sim.telemetry.pv.pv-1` bzw. `sim.telemetry.wind.wind-1`) mit allen feldspezifischen Metriken (Einstrahlung, Temperatur, Bewölkung bzw. Windgeschwindigkeit, Rotor-RPM, Pitch-Winkel). Zudem abonnieren beide Simulatoren Steuerbefehle (`sim.commands.pv.#`, `sim.commands.wind.#`) zur Fernsteuerung von Leistungslimits (`SetPowerLimit`/`Setpoint`) sowie Fehler-Auslösung/Reset (`TriggerFault`/`ResetFault`). Der Verbindungs- und Sendestatus wird im Spectre.Console-Dashboard visualisiert.
- **KI:** Google Gemini 3.8 Flash (High) => Analyse der bisherigen Gateway- vs. Direkt-Architektur im Vergleich mit Dual-Port-Charger/Battery, Implementierung von Publisher/Consumer in Simulationsrunnern und Engine sowie UI-Dashboard-Integration.
- **Artefact:** `PV_Simulator/PV_Simulator/Simulation/SimulationRunner.cs`, `PV_Simulator/PV_Simulator/Simulation/SimulationEngine.cs`, `PV_Simulator/PV_Simulator/ConsoleApp/ConsoleStatusRenderer.cs`, `PV_Simulator/PV_Simulator/Program.cs`, `WindTurbine_Simulator/WindTurbine_Simulator.csproj`, `WindTurbine_Simulator/Simulation/WindTurbineSimulationRunner.cs`, `WindTurbine_Simulator/ConsoleApp/ConsoleStatusRenderer.cs`, `WindTurbine_Simulator/Program.cs`
- **Test:** Vollständiger Solution-Build via `dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1` (0 Fehler, 0 Warnungen) und Ausführung aller 9/9 Tests in `TeamRabbitMQShared.Tests` (PASSED).
- **Comment:** Zuvor sendeten PV und Wind nur über das optionale `ModbusRabbitMqGateway` Telemetriedaten an RabbitMQ. Durch die native Integration arbeiten alle vier Feldgeräte (Charger, Battery, PV, Wind) nun nach dem gleichen einheitlichen Muster und können sowohl autark via RabbitMQ als auch parallel via Modbus TCP betrieben werden. Der WeatherSimulator bleibt rein HTTP-basiert (REST/Push-Callbacks).

### Windows Startup-Skripte – Syntax- und Pfad-Korrektur (Batch & PowerShell)
- **Done:** Die Startskripte `scripts/start-all.bat` und `scripts/start-all.ps1` für Windows vollständig repariert. In der Batch-Datei führten ungeklammerte Klammern `(dotnet)` im `if`-Block zu einem Syntaxfehler (`"wurde" kann syntaktisch an dieser Stelle nicht verarbeitet werden`), während in PowerShell ein unvollständig geschlossenes Stringliteral vorlag (`TerminatorExpectedAtEndOfString`). Beide Skripte wurden auf saubere Pfadbehandlung für Verzeichnisse mit Leerzeichen (`5. Semester`, `.NET`), `docker-compose`/`docker compose`-Fallback und sichere Fenster-Starts aktualisiert und zusätzlich als Schnellstarter im Root-Verzeichnis bereitgestellt.
- **KI:** Google Gemini 3.8 Flash (High) => Parserfehler identifiziert, Windows-CRLF/ASCII-Kodierung und Argumentlisten bereinigt.
- **Artefact:** `scripts/start-all.bat`, `scripts/start-all.ps1`, `start-all.bat`, `start-all.ps1`
- **Test:** PowerShell-Syntax-Validierung via AST Parser (`PS1 SYNTAX PERFECT`) und CMD-Batchlauf erfolgreich geprüft.
