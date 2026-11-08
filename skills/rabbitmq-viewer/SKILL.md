---
name: rabbitmq-viewer
description: >-
  Guides running and interacting with the RabbitMQ Live Telemetry Viewer to monitor simulation streams in real time.
---

# RabbitMQ Telemetry Viewer

The `RabbitMqViewer` console tool subscribes to the RabbitMQ topic exchange `sim.exchange` with pattern `sim.telemetry.#` and renders an interactive dashboard of all active field devices.

## Starting the Viewer
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
```

## Dashboard Features
* **Device Telemetry Grid:** Shows real-time Active Power (kW), Total Energy (kWh), SOC, and operational status.
* **Message Traffic Metrics:** Live count of messages received, packet rate, and uptime.
* **Interactive Controls:**
  * `[C]` Send test command to a field device
  * `[R]` Clear/Reset view
  * `[Q]` Exit
