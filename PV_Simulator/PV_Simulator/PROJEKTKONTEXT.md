# PV-Simulator – Projektkontext

Diese Datei fasst Anforderungen, Entscheidungen und den bisherigen Implementierungsstand zusammen, damit die Arbeit in einer späteren Sitzung fortgesetzt werden kann.

## Projektziel

Eine digitale Zwillingssimulation einer PV-Anlage als .NET-10-Konsolenanwendung. Ein Simulationslauf umfasst 24 Stunden und läuft im beschleunigten Zeitraffer. Es gibt keine GUI und die Anwendung arbeitet offline.

## Vereinbarte Anforderungen und Entscheidungen

- Objektorientierte, modulare Aufteilung; `Program.cs` soll nur Einstieg und Zusammensetzen der Komponenten enthalten.
- Näherungsweise realistische Leistung aus Sonnenstand, Einstrahlung, Bewölkung, Modulneigung/-ausrichtung, Anlagenverlusten, Temperatur, manuell gesetzter Schneebedeckung und Wechselrichterzustand.
- Standortangabe umfasst einen Namen (z. B. „FH Salzburg“) sowie Breitengrad und Längengrad. Offline kann ein freier Gebäude-/Ortsname nicht automatisch in Koordinaten aufgelöst werden; Koordinaten müssen daher eingegeben werden.
- Während der laufenden Simulation toggelt `S` die PV-Module zwischen schneefrei und vollständig verschneit. Die manuelle Schneebedeckung startet schneefrei; bei verschneiten Modulen wird die PV-Leistung auf null reduziert. Es werden keine Schnee-Klima- oder externen Schneedaten geladen.
- Wechselrichterfehler werden zufällig anhand einer konfigurierbaren Ausfallwahrscheinlichkeit pro Stunde ausgelöst und dauern eine konfigurierte Reparaturzeit.
- Simulationsschritt und Anfangsgeschwindigkeit sind konfigurierbar. Während des Laufs: `+` erhöht und `-` verringert die Geschwindigkeit; `P` pausiert bzw. setzt fort.
- Während des Laufs schaltet `F` eine manuelle Wechselrichterstörung ein oder aus. Manuelle Störung und zufälliger Ausfall werden im Snapshot unterschieden.
- Konsolenausgabe zeigt Simulationsdatum/-zeit, Schritt, Geschwindigkeit, Betriebszustand, Wetter, Leistung mit Prozentbalken, Tagesenergie, Einstrahlung und Wechselrichterzustand.
- Wetter wird offline synthetisch und dynamisch aus Datum/Standort erzeugt: saisonaler und täglicher Temperaturgang, geglättete Wolkenwellen sowie bewölkungsabhängige Regenereignisse. Das ist eine plausible Simulation, keine Wettervorhersage oder historische Wetterreihe.
- Der optionale Wetter-Seed wird bei der Konfiguration abgefragt; Enter erzeugt einen neuen Zufallsverlauf. Ein gesetzter Seed macht die Zufallsereignisse des Laufs reproduzierbar.
- Laufzeitsteuerung: `C` wählt Wolken, `T` Temperatur; `+`/`-` ändern die Auswahl um 10 Prozentpunkte bzw. 2 °C. Manuelle Overrides bleiben bestehen, bis `A` die ausgewählte Größe zur Automatik zurücksetzt. `R` toggelt manuellen Regen, `F` die manuelle Wechselrichterstörung und `S` die manuelle Schneebedeckung. Ohne ausgewählte Wettergröße steuern `+`/`-` weiter die Geschwindigkeit.
- Momentane PV-Leistung und maximale Wechselrichter-Ausgangsleistung werden in kW geführt; die PV-Modul-Nennleistung wird in kWp angegeben. Einstrahlung bleibt in W/m² und Energie in kWh.
- Jede PV-Anlage hat eine nicht leere Geräte-ID und eine manuell einzugebende PV-Modul-Nennleistung in kWp. Die maximale Wechselrichter-Ausgangsleistung wird unabhängig davon manuell in kW eingegeben. Beide Eingaben sind in Standard- und manuellem Profil verpflichtend; es gibt keine voreingestellten Hardwareleistungen.
- Ein Programmprozess simuliert eine Anlage; unterschiedliche Geräte-IDs können gleichzeitig in getrennten Prozessen laufen. Dieselbe ID wird während eines aktiven Laufs pro Benutzerprofil durch eine exklusive Dateisperre abgewiesen (Groß-/Kleinschreibung wird bei der Sperre ignoriert). Nach Prozessende kann die ID wiederverwendet werden.
- Geräte-ID sowie PV-Modul-Nennleistung und Wechselrichter-Ausgangsleistung werden mit den Einheiten kWp bzw. kW in Konfigurationsübersicht, laufender Konsolenausgabe und CSV ausgewiesen. CSV-Dateien enthalten die Geräte-ID und beide Leistungen pro Datensatz und erhalten zusätzlich eine eindeutige GUID im Namen.
- Der Startdialog bietet Standardwerte oder manuelle Eingabe. Manuelle Eingaben können pro Feld durch Enter den Default übernehmen.
- Nach Wahl des Profils wird verpflichtend ein Simulationsdatum gewählt: manuelle Datumseingabe oder ein saisonaler Stichtag (20.03. Frühling, 21.06. Sommer, 22.09. Herbst, 21.12. Winter; jeweils aktuelles Jahr). Die Simulation startet immer um 00:00 Uhr Ortszeit.
- Nach Profil- und Datumsauswahl werden Standort, Anlagen-/Wechselrichterparameter, Datum/Startzeit, Zeitschritt, Zeitraffer und Wetter-Seed gemeinsam als finale Konfiguration vor dem Simulationsstart angezeigt.
- Jeder Simulationsschritt wird in eine automatisch benannte CSV-Datei im Verzeichnis `exports` geschrieben. Für deutsches Excel nutzt die CSV Semikolon als Spaltentrennzeichen, `de-DE`-Zahlenformat mit Dezimalkomma sowie getrennte Spalten `Date` (`dd.MM.yyyy`) und `Time` (`HH:mm:ss`). Enthalten sind unter anderem Leistung/Nennleistung, Energie in kWh, Wetterwerte sowie manuelle Schnee-, Override- und Störungsflags.

### Standardwerte

- Standort: FH Salzburg, Breitengrad 47°, Längengrad 13°, Höhe 400 m.
- PV-Feld: Geräte-ID `PV-FH-01`, Modul-Nennleistung wird manuell in kWp eingegeben; Neigung 30°, Azimut 180°.
- Wechselrichter: maximale Ausgangsleistung wird getrennt manuell in kW eingegeben; zufällige Ausfallwahrscheinlichkeit 1 % pro Stunde, Reparaturdauer 60 Minuten.
- Startdatum: verpflichtende Auswahl (eigenes Datum oder Saison-Stichtag); Startzeit immer 00:00 Uhr Ortszeit. Simulationsschritt: 5 Minuten; Zeitraffer: 60x.
- Wetter-Seed: zufällig, sofern kein expliziter Seed eingegeben wird.
- Beim Standardprofil werden die verwendeten Werte vor Start auf der Konsole angezeigt.

## Aktueller Implementierungsstand

Das Grundgerüst wurde in das vorhandene .NET-10-Projekt integriert:

- `PV_Simulator/Program.cs` – Konfiguration und Zusammensetzen der Komponenten.
- `PV_Simulator/Domain/Location.cs` – Standortname, Koordinaten und Höhe.
- `PV_Simulator/Domain/PvArray.cs` – Geräte-ID, PV-Nennleistung, Neigung, Azimut und Systemverluste.
- `PV_Simulator/Domain/Inverter.cs` und `InverterStatus.cs` – Wechselrichterkonfiguration und Status.
- `PV_Simulator/Domain/WeatherConditions.cs` – Bewölkung, Temperatur und Niederschlag.
- `PV_Simulator/Domain/SimulationSettings.cs` und `SimulationSnapshot.cs` – Laufparameter und Messwerte.
- `PV_Simulator/Domain/WeatherSettings.cs` – optionaler Zufalls-Seed für reproduzierbare Wetter-/Ereignisverläufe.
- `PV_Simulator/Domain/SimulationConfiguration.cs` und `SimulationDefaults.cs` – zusammengefasste Konfiguration und zentrale Startdefaults.
- `PV_Simulator/Calculations/SolarPositionCalculator.cs` – angenäherte Sonnenposition.
- `PV_Simulator/Calculations/PvPowerCalculator.cs` – Einstrahlung auf die Module und Leistungsberechnung in kW einschließlich Temperatur-, manueller Schnee- und Wechselrichterbegrenzung.
- `PV_Simulator/Simulation/SimulationEngine.cs` – Simulationsschritte, Energie, zufällige Wechselrichterfehler und manuell getogglete Schneebedeckung.
- `PV_Simulator/Weather/WeatherEngine.cs` – separat gekapselter automatischer Temperatur-, Wolken- und Regenverlauf sowie manuelle Wetter-Overrides.
- `PV_Simulator/Simulation/SimulationRunner.cs` – 24-Stunden-Ablauf, Laufzeit-Tastatursteuerung und CSV-Aufruf pro Simulationsschritt.
- `PV_Simulator/Simulation/CsvSimulationLogger.cs` – strukturiertes CSV-Protokoll mit zeitlicher Auflösung entsprechend dem Simulationsschritt.
- `PV_Simulator/Simulation/DeviceIdLease.cs` – exklusive prozessübergreifende Sperre für aktive Geräte-IDs im lokalen Benutzerprofil.
- `PV_Simulator/ConsoleApp/ConsoleInput.cs` und `ConsoleStatusRenderer.cs` – interaktive Eingabe und Textausgabe.
- `PV_Simulator/ConsoleApp/SimulationDateSelector.cs` – Datumseingabe oder saisonaler Stichtag mit fester lokaler Startzeit 00:00 Uhr.

## Validierung

- Build des Projekts und der gesamten Lösung war erfolgreich.
- Im Projekt wurden keine Tests gefunden; ein separates Testprojekt wurde bisher nicht angelegt.

## Noch offen / sinnvolle nächste Schritte

1. Wetterverlauf über den Tag als Profil modellieren, statt Bewölkung und Temperatur 24 Stunden konstant zu halten.
2. Automatisierte Tests für Sonnenstand, kW-Leistungsberechnung, manuelle Schneesteuerung, CSV-Ausgabe und Wechselrichterfehler ergänzen.
3. Modellannahmen und Randfälle überprüfen, z. B. Tagesenergie über Mitternacht, Zeitzonen und Verhalten bei umgeleiteter Konsoleneingabe.
4. Bei Bedarf eine Offline-Ortsdatenbank ergänzen. Ohne Geocoding-Dienst können beliebige Gebäudenamen nicht automatisch Koordinaten liefern.
