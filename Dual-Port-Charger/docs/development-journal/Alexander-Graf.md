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