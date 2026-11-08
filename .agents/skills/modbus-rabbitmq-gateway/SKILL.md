---
name: modbus-rabbitmq-gateway
description: >-
  Explains how to run, configure, and monitor the Modbus-RabbitMQ Gateway bridging field device Modbus TCP registers to RabbitMQ pub/sub messages.
---

# Modbus-RabbitMQ Gateway Operation

The `ModbusRabbitMqGateway` serves as a bidirectional bridge between industrial Modbus TCP servers and RabbitMQ message brokers.

## Key Responsibilities
1. **Telemetry Polling (Modbus Client -> RabbitMQ Publisher):**
   * Periodically queries holding registers 1–12 (OperatingState, ErrorCode, ActivePower, CumulativeEnergy, VirtualTime) plus device-specific registers.
   * Packages sensor readings into JSON `DeviceTelemetryMessage`.
   * Publishes messages to exchange `sim.exchange` under routing key `sim.telemetry.<deviceType>.<id>`.

2. **Command Dispatching (RabbitMQ Consumer -> Modbus Client):**
   * Subscribes to `sim.commands.<deviceType>.<id>`.
   * Upon receiving `SetPowerLimit` or `ResetFault`, writes to Setpoint registers (10–11) or Error Reset register (12) via Modbus TCP.

## Launching the Gateway
```bash
DOTNET_CLI_HOME=/tmp/dotnet_home dotnet run --project ModbusRabbitMqGateway/ModbusRabbitMqGateway.csproj
```

## Configuration
Options can be specified via environment variables or `appsettings.json`:
* `RABBITMQ_HOST`: Hostname (default `localhost`)
* `RABBITMQ_PORT`: AMQP Port (default `5672`)
* `MODBUS_POLL_INTERVAL_MS`: Polling interval in ms (default `1000`)
