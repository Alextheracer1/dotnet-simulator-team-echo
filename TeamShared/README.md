# TeamShared Modbus Library

**Zweck:**
Gemeinsame Class Library für alle Feldgeräte-Simulatoren und den Diagnose-Client. Kapselt die EasyModbus-Abhängigkeit und stellt den Modbus-Server-Wrapper (`FieldDeviceModbusServer`) sowie die Register-Zuordnung (`CommonRegisterMap`) für das gesamte Team bereit.

**Verantwortliche Person:**
[Name hier eintragen]

**Konfiguration:**
Keine eigenständige Laufzeitkonfiguration erforderlich.

**Verwendung:**
Wird von ausführbaren Projekten (Simulatoren, Diagnose-Client) über eine Projekt-Referenz (`ProjectReference`) eingebunden.

**Angebotene Schnittstellen:**
- `TeamShared.Modbus.FieldDeviceModbusServer`: Wrapper zur Initialisierung des EasyModbus-Servers.
- `TeamShared.Modbus.CommonRegisterMap`: Konstanten für die Registeradressen (01-12).

**Tests:**
Getestet durch Einbindung in den PV-Simulator und den Diagnose-Client; baut fehlerfrei in der Solution.

**Bekannte Einschränkungen:**
- Keine. Setzt die EasyModbus TCP.NETCore Bibliothek im übergeordneten Verzeichnis voraus.
