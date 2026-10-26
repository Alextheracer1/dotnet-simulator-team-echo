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
