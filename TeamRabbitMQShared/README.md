# TeamRabbitMQShared Library

Gemeinsame wiederverwendbare Bibliothek für alle Simulatoren, Gateways und Viewer im Energy Twin 2026 Projekt.

## Zweck
Stellt die standardisierte RabbitMQ-Kommunikationsinfrastruktur bereit:
- **`RabbitMqPublisher`**: Thread-sicherer, asynchroner Publisher für Sensordaten (`DeviceTelemetryMessage`) und Steuerbefehle (`DeviceCommandMessage`).
- **`RabbitMqConsumer`**: Resilienter Consumer mit dynamischer Queue- und Topic-Bindung.
- **`ModbusTelemetryMapper`**: Konverter zwischen Modbus TCP-Registern (1–12 + gerätespezifisch) und RabbitMQ Telemetrienachrichten.
- **Topologie & Konventionen**: Standardisierte Topic-Hierarchie auf dem durable Topic Exchange `sim.exchange`.

## Verwendung in Projekten
Fügen Sie in Ihrer `.csproj` Datei eine Projekt-Referenz hinzu:
```xml
<ItemGroup>
  <ProjectReference Include="../TeamRabbitMQShared/TeamRabbitMQShared.csproj" />
</ItemGroup>
```
