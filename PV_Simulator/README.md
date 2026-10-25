# PV-Simulator – Digitaler Zwilling

Dieses Projekt ist eine in C# entwickelte Konsolenanwendung, die als digitaler Zwilling einer Photovoltaikanlage fungiert. Sie simuliert die Stromerzeugung über einen 24-Stunden-Zeitraum unter Berücksichtigung von Hardware-Parametern, physikalischen Gegebenheiten und dynamischen Wetterbedingungen.

## Voraussetzungen
* **System:** Windows, macOS oder Linux.
* **Framework:** .NET 10.0 SDK oder neuer muss installiert sein.
* **Konsole:** Ein Terminal, das UTF-8-Zeichen (für den dynamischen Leistungsbalken) unterstützt (z. B. Windows Terminal, VS Code Terminal oder Visual Studio Entwicklerkonsole).

## Start- und Bedienhinweise

**Programm starten:**
Navigiere im Terminal in den Projektordner und starte die Simulation mit folgendem Befehl:
```bash
dotnet run
```

**Start-Konfiguration:**
Beim Programmstart kannst du zwischen zwei Profilen wählen:
1. **Standardwerte (1):** Lädt den Standort FH Salzburg. Nennleistung der PV-Module und des Wechselrichters müssen zwingend eingegeben werden.
2. **Manuell (2):** Alle Parameter (Standort, Neigung, Ausfallwahrscheinlichkeiten) können individuell eingestellt werden (Drücken von `Enter` übernimmt den jeweiligen Standardwert).

**Steuerung während der Laufzeit:**
Während die Simulation läuft, kannst du die Umgebungsvariablen live über die Tastatur steuern:
* `P` - Pausiert die Simulation (Störungs-Tasten bleiben zur Vorbereitung aktiv).
* `+` / `-` - Steuert die Simulationsgeschwindigkeit (Zeitraffer).
* `F` - Löst einen manuellen Wechselrichter-Ausfall aus (Leistung fällt auf 0 kW).
* `S` - Simuliert plötzliche Schneebedeckung der Module (starker Leistungseinbruch).
* `R` - Aktiviert/Deaktiviert manuellen Regen.
* `C` - Wählt Wolkenbedeckung aus (danach mit `+` / `-` in 10%-Schritten steuern).
* `T` - Wählt Temperatur aus (danach mit `+` / `-` in 2°C-Schritten steuern).
* `A` - Gibt die Wetterkontrolle wieder an die Zufalls-Automatik ab.

*Alle Daten werden automatisch mit Zeitstempel im Ordner `/exports` als CSV-Datei für die Auswertung (z.B. in Excel) gespeichert.*

## Modellbeschreibung
Der Simulator berechnet die elektrische Leistung (in kW) in jedem Zeitschritt iterativ. Die Architektur trennt die Systemkomponenten physikalisch korrekt:
* **Wetter & Sonnenstand (`SimulationEngine` & `WeatherEngine`):** Basierend auf GPS-Koordinaten, Datum und Uhrzeit wird der genaue Sonnenstand berechnet. Temperatur und Wolkenbedeckung beeinflussen die globale Einstrahlung auf die Module.
* **PV-Array (`PvPowerCalculator`):** Berechnet die DC-Leistung basierend auf der effektiven Einstrahlung, dem Neigungswinkel und der Modul-Nennleistung (kWp). Schnee reduziert den Wirkungsgrad massiv.
* **Wechselrichter (`Inverter`):** Kappt die maximale AC-Ausgangsleistung bei Überlast. Er simuliert zudem MTBF (Mean Time Between Failures) durch eine statistische Ausfallwahrscheinlichkeit und eine definierte Reparaturdauer.

## Beispielparameter für einen Testlauf
Um einen realistischen und gut beobachtbaren Simulationslauf zu testen, empfehlen sich folgende Eingaben (Profil 2):

* **Standort:** FH Salzburg (Breitengrad: 47.72, Längengrad: 13.08)
* **PV-Modul-Nennleistung:** `10` (kWp)
* **Modulneigung:** `35` (Grad)
* **Azimut:** `180` (Süden)
* **Maximale Wechselrichter-Leistung:** `8` (kW) - *Bewusst kleiner dimensioniert, um das "Kappen" der Spitzenleistung zur Mittagszeit zu beobachten.*
* **Ausfallwahrscheinlichkeit:** `2` (% pro Stunde)
* **Simulationsschritt:** `5` (Minuten) - *Bietet eine flüssige Balken-Animation.*
* **Wetter-Zufallsseed:** `42` (Für einen reproduzierbaren Verlauf)