using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TeamRabbitMQShared.Models;

/// <summary>
/// Control / setpoint command transmitted via RabbitMQ to field devices.
/// </summary>
public class DeviceCommandMessage
{
    [JsonPropertyName("commandId")]
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("targetDeviceId")]
    public string TargetDeviceId { get; set; } = "*";

    [JsonPropertyName("targetDeviceType")]
    public string TargetDeviceType { get; set; } = "*";

    /// <summary>
    /// Type of command, e.g. "SetPowerLimit", "ResetFault", "TriggerFault", "ToggleCar", "SetOperatingState".
    /// </summary>
    [JsonPropertyName("commandType")]
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// Numeric payload value (power values are signed watts; other commands may use an ID or flag).
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("stringPayload")]
    public string? StringPayload { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static DeviceCommandMessage CreateSetPowerLimit(string deviceId, double powerWatts) =>
        CreateSetPowerLimit("charger", deviceId, powerWatts);

    public static DeviceCommandMessage CreateSetPowerLimit(string deviceType, string deviceId, double powerWatts) => new()
    {
        TargetDeviceId = deviceId,
        TargetDeviceType = deviceType,
        CommandType = "SetPowerLimit",
        Value = powerWatts
    };

    public static DeviceCommandMessage CreateResetFault(string deviceId) => new()
    {
        TargetDeviceId = deviceId,
        CommandType = "ResetFault",
        Value = 1.0
    };

    public static DeviceCommandMessage CreateTriggerFault(string deviceId) => new()
    {
        TargetDeviceId = deviceId,
        CommandType = "TriggerFault",
        Value = 1.0
    };

    public static DeviceCommandMessage CreateToggleCar(string deviceId, int portId) => new()
    {
        TargetDeviceId = deviceId,
        TargetDeviceType = "charger",
        CommandType = "ToggleCar",
        Value = portId
    };
}
