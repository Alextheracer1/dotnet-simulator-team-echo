# Modbus-Diagnose-Client

**Zweck:**
Konsolenanwendung zum Testen und Debuggen der Feldgeräte-Simulatoren. Liest den gemeinsamen Registerkopf, gerätespezifische Werte und erlaubt das Schreiben von Leistungsgrenzen.

**Verantwortliche Person:**
[Name hier eintragen]

**Konfiguration:**
Host, Port und Unit-ID (Zielgerät) werden interaktiv beim Programmstart abgefragt.

**Start- und Bedienhinweise:**
1. Zuerst einen Feldgeräte-Simulator (z. B. den PV-Simulator) starten, sodass der Modbus-Server läuft.
2. In diesem Verzeichnis im Terminal `dotnet run` ausführen.
3. Host (Standard `127.0.0.1`), Port (z.B. `1502`) und Unit-ID eingeben.
4. Das interaktive Menü nutzen (Ziffern 1-5 eingeben), um Register zu lesen oder Fehler zurückzusetzen.

**Benötigte Schnittstellen:**
- Ein aktiver Modbus TCP Server auf einem der Feldgeräte.
- `TeamShared` Library für die Modbus-Mapping-Konstanten.

**Tests:**
- Modbus-Protokollfehler und fehlende Netzwerkverbindungen werden mit Try-Catch abgefangen und textuell in der Konsole ausgegeben.
- Das Lesen des gemeinsamen Registerkopfs (01-11) und das Schreiben des Setpoints wurden geprüft.

**Bekannte Einschränkungen:**
- Der `Connect()`-Aufruf zur Fremdbibliothek EasyModbus erfolgt synchron. Ist das Zielgerät offline, blockiert der Aufruf kurz, bis ein Timeout auftritt (gemäß Vorgaben zulässig).
