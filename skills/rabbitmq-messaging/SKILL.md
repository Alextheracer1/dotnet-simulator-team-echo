---
name: rabbitmq-messaging
description: >-
  Provides code snippets and guidelines for utilizing TeamRabbitMQShared to publish sensor telemetry and consume control commands in .NET simulators.
---

# RabbitMQ Messaging Integration with `TeamRabbitMQShared`

Use this skill when adding RabbitMQ communication capabilities to a simulator or tool.

## Referencing the Library
In your project `.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="../TeamRabbitMQShared/TeamRabbitMQShared.csproj" />
</ItemGroup>
```

## Creating a Publisher
```csharp
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

var config = new RabbitMqConfiguration { HostName = "localhost" };
var publisher = new RabbitMqPublisher(config);
await publisher.StartAsync();

var telemetry = new DeviceTelemetryMessage
{
    DeviceId = "charger-1",
    DeviceType = "charger",
    Timestamp = DateTime.UtcNow,
    VirtualTime = virtualTimestamp,
    OperatingState = 2, // Running
    ActivePowerWatts = 150000,
    CumulativeEnergyKwh = 340.5,
    DeviceSpecificData = new Dictionary<string, object>
    {
        ["SOC_Port1"] = 0.65,
        ["SOC_Port2"] = 0.40
    }
};

await publisher.PublishTelemetryAsync(telemetry);
```

## Creating a Consumer
```csharp
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;

var config = new RabbitMqConfiguration { HostName = "localhost" };
var consumer = new RabbitMqConsumer(config);
await consumer.StartAsync();

await consumer.SubscribeCommandsAsync("sim.commands.charger.1", async cmd =>
{
    if (cmd.CommandType == "SetPowerLimit")
    {
        device.SetPowerLimit((decimal)cmd.Value);
    }
    await Task.CompletedTask;
});
```
