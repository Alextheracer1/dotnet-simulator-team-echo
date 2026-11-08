using System;
using System.Collections.Generic;

namespace ModbusRabbitMqGateway;

public class ModbusDeviceEndpoint
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public byte UnitId { get; set; } = 1;
    public int RegisterCount { get; set; } = 24;
}

public class GatewayConfiguration
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public List<ModbusDeviceEndpoint> Endpoints { get; set; } = new()
    {
        new ModbusDeviceEndpoint { DeviceId = "charger-1", DeviceType = "charger", Host = "127.0.0.1", Port = 1502, UnitId = 1, RegisterCount = 20 },
        new ModbusDeviceEndpoint { DeviceId = "battery-1", DeviceType = "battery", Host = "127.0.0.1", Port = 1503, UnitId = 1, RegisterCount = 24 },
        new ModbusDeviceEndpoint { DeviceId = "pv-1", DeviceType = "pv", Host = "127.0.0.1", Port = 1504, UnitId = 1, RegisterCount = 14 },
        new ModbusDeviceEndpoint { DeviceId = "wind-1", DeviceType = "wind", Host = "127.0.0.1", Port = 1505, UnitId = 1, RegisterCount = 14 }
    };
}
