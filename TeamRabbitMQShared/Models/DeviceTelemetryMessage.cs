using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TeamRabbitMQShared.Models;

/// <summary>
/// Unified sensor telemetry payload transmitted by field devices to RabbitMQ.
/// </summary>
public class DeviceTelemetryMessage
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("virtualTime")]
    public long VirtualTime { get; set; }

    [JsonPropertyName("operatingState")]
    public short OperatingState { get; set; } // 0=Off, 1=Booting, 2=Running, 3=Error

    [JsonPropertyName("operatingStateDescription")]
    public string OperatingStateDescription => OperatingState switch
    {
        0 => "Off",
        1 => "Booting",
        2 => "Running",
        3 => "Error",
        _ => "Unknown"
    };

    [JsonPropertyName("errorCode")]
    public short ErrorCode { get; set; }

    [JsonPropertyName("activePowerWatts")]
    /// <summary>Signed active power in watts; negative values represent reverse power flow.</summary>
    public double ActivePowerWatts { get; set; }

    [JsonPropertyName("activePowerKw")]
    public double ActivePowerKw => ActivePowerWatts / 1000.0;

    [JsonPropertyName("cumulativeEnergyKwh")]
    public double CumulativeEnergyKwh { get; set; }

    [JsonPropertyName("isOn")]
    public bool IsOn { get; set; } = true;

    [JsonPropertyName("modbusPort")]
    public int ModbusPort { get; set; }

    [JsonPropertyName("modbusUnitId")]
    public byte ModbusUnitId { get; set; } = 1;

    /// <summary>
    /// Device-specific sensor metrics (e.g., SOC1, SOC2 for chargers; SOC, Voltage, Current for battery; Irradiance for PV).
    /// </summary>
    [JsonPropertyName("deviceSpecificData")]
    public Dictionary<string, double> DeviceSpecificData { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public double GetMetric(string key, double defaultValue = 0.0)
    {
        return DeviceSpecificData.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public void SetMetric(string key, double value)
    {
        DeviceSpecificData[key] = value;
    }
}
