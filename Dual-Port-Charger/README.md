# E-Auto-Ladesäule Simulator (.NET 2026 - LB01)

Dieser digitale Zwilling simuliert eine öffentliche E-Auto-Ladesäule ("Dual-Port Charger") mit zwei unabhängigen Ladepunkten. Die Anwendung wurde als C#-Konsolenanwendung entwickelt und erfüllt die Anforderungen der Aufgabe 01 (LB01) der Lehrveranstaltung ".NET Programmierung für industrienahe Anwendungen".

## Voraussetzungen

* **.NET 10.0 SDK** (gemäß Aufgabenstellung)
* Terminal/Konsole mit Unterstützung für UTF-8 Zeichen (für die grafische Balkendarstellung)

## Start- und Bedienhinweise

### Starten der Simulation
Navigieren Sie im Terminal in das Projektverzeichnis (dort wo die `.csproj`-Datei liegt) und starten Sie das Projekt mit:

```bash
dotnet run
```

### Bedienung (Tastenbelegung)
Die Simulation kann zur Laufzeit über die folgenden Tasten live gesteuert werden. Alle Eingaben werden sofort verarbeitet:

* **Modus-Steuerung**
  * `[A]` - **Auto-Modus aktivieren:** Fahrzeuge kommen und fahren zufallsgesteuert.
  * `[M]` - **Manuell-Modus aktivieren:** Setzt feste Fahrzeuge (Tesla Model S & Model Y) für manuelle Tests.
* **Manuelle Eingriffe (nur im Manuell-Modus `[M]`)**
  * `[1]` / `[2]` - Fahrzeug an Ladepunkt 1 bzw. 2 verbinden/trennen.
  * `[↑]` / `[↓]` - Manuelle Leistungsbegrenzung für die Ladepunkte erhöhen/verringern (in 5 kW Schritten).
* **Fehlersimulation**
  * `[F]` - **Fehler auslösen:** Die Anlage geht in den Störungszustand, die Ladeleistung sinkt sofort auf 0 kW.
  * `[R]` - **Fehler zurücksetzen:** Die Störung wird behoben, der Ladevorgang wird fortgesetzt.
* **Simulationssteuerung**
  * `[+]` / `[-]` - Simulationsgeschwindigkeit anpassen (Echtzeit-Delay pro Sim-Minute verringern/erhöhen).
  * `[P]` - Simulation pausieren / fortsetzen.
  * `[X]` - Simulation vorzeitig beenden (reguläres Ende nach 24 simulierten Stunden).

## Modellbeschreibung

Das Simulationsmodell bildet das physikalische und zeitliche Verhalten einer Schnellladesäule (High-Power-Charger) ab.

### 1. Anlagenmodell & Dynamisches Lastmanagement (Load Balancing)
* Die Ladesäule verfügt über eine maximale Netzanschlussleistung von **300 kW**.
* Die Leistung wird durch ein **Dynamic Load Balancing** fair auf beide Ladepunkte aufgeteilt, sofern beide aktiv sind (max. 150 kW pro Punkt, wenn beide volle Leistung fordern).
* Fordert ein Fahrzeug aufgrund seines hohen Ladezustands weniger Leistung an, wird die freigewordene Leistung dem anderen Ladepunkt dynamisch zur Verfügung gestellt.

### 2. Fahrzeugmodelle & Ladekurven
Das Modell nutzt nicht einfach einen linearen Ladevorgang, sondern simuliert **realistische Ladekurven** basierend auf dem State of Charge (SOC). Die Ladeleistung nimmt mit steigendem Akkustand ab.
Folgende Fahrzeuge sind im Katalog (`EVModelCatalog`) hinterlegt:
* **Tesla Model S 100D:** 100 kWh Akku, max. 142 kW Ladeleistung, starke Drosselung ab 50% SOC.
* **Tesla Model Y LR:** 78 kWh Akku, max. 250 kW Ladeleistung (fällt ab 30% SOC ab).
* **Porsche Taycan:** 93,4 kWh Akku, 800V-Architektur, extrem flache Kurve bis 270 kW.
* **VW ID.4 Pro:** 77 kWh Akku, max. 135 kW Ladeleistung.

### 3. Betriebsmodi
* **Auto-Modus:** Fahrzeuge der oben genannten Modelle kommen rein zufällig (10% Chance pro Minute) mit einem zufälligen Start-SOC (5%-20%) an. Auch das Ziel-SOC (Target SOC) variiert zwischen 50% und 100%. Ist das Ziel erreicht, fährt das Auto nach einiger Zeit (10% Chance pro Minute) selbstständig wieder ab.
* **Manuell-Modus:** Ermöglicht das Testen des Lastmanagements. Es werden fest zwei Teslas an den Punkten simuliert. Der Nutzer kann die Autos per Knopfdruck an- und abstecken sowie das Leistungslimit der Säule drosseln (z.B. um Netzdienlichkeit zu simulieren).

## Beispielparameter & Annahmen

* **Simulations-Schrittweite:** `1 Minute` (konfigurierbar in `Program.cs`)
* **Simulations-Dauer:** 24 Stunden (Start: 15.07.2026, 00:00 Uhr)
* **Gesamtleistung (`TotalStationPower`):** 300.000 Watt (300 kW)
* **Physikalische Berechnung:** Die zugeführte Energie wird pro Minute aus der anliegenden Wirkleistung exakt auf den jeweiligen Akku aufgeschlagen: `Energy += ActivePower * (1/60h)`.

## Logging und Datenexport

Bei jedem Simulationsschritt (standardmäßig alle 1 simulierte Minute) wird der aktuelle Zustand der Anlage in eine CSV-Datei exportiert.
* **Dateipfad:** `./logs/simulation.csv`
* **Erfasste Daten:** Zeitstempel, StationMaxLeistung (kW), Wirkleistung (kW), Gesamt-Energie (kWh), SOC Ladepunkt 1, SOC Ladepunkt 2.

Diese CSV-Datei kann nach dem Simulationslauf zur Erstellung von Ladekurven oder Auswertungen in Excel/Calc importiert werden.