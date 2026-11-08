---
name: run-simulators
description: >-
  Guides the orderly execution of the distributed Energy Twin simulation environment, including Simulations-Master, field devices (Dual-Port-Charger, Battery, PV), Gateway, and Diagnostics.
---

# Run Simulation Ecosystem

This runbook defines the recommended sequence for starting and orchestrating the Energy Twin 2026 simulation components.

## Startup Order

### 1. Central Master & Clock Provider
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Simulations-Master/Simulations-Master.csproj
```
*Port:* 5000 (TCP / Synchronization)

### 2. Field Devices (Simulators)
Open separate terminal tabs for each simulator:

* **Dual-Port-Charger (Modbus Port 1502, Unit ID 1):**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Dual-Port-Charger/Dual-Port-Charger.csproj
  ```
* **Battery Simulator (Modbus Port 1503, Unit ID 1):**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project Battery/Battery.csproj
  ```
* **PV Simulator (Modbus Port 1504, Unit ID 1):**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project PV_Simulator/PV_Simulator/PV_Simulator.csproj
  ```

### 3. Modbus-RabbitMQ Gateway
Relays Modbus register telemetry to RabbitMQ topics and routes RabbitMQ commands to Modbus setpoints:
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj
```

### 4. Telemetry Viewer & Diagnostic Tools
* **RabbitMQ Live Telemetry Viewer:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project RabbitMqViewer/RabbitMqViewer.csproj
  ```
* **Modbus Diagnose Client:**
  ```bash
  DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusDiagnoseClient/ModbusDiagnoseClient.csproj
  ```
