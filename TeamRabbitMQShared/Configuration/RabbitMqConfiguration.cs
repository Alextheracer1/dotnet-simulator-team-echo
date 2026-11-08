using System;

namespace TeamRabbitMQShared.Configuration;

/// <summary>
/// Connection and exchange topology configuration for RabbitMQ messaging.
/// </summary>
public class RabbitMqConfiguration
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Default exchange used for simulation pub/sub topics.
    /// </summary>
    public string ExchangeName { get; set; } = "sim.exchange";
    public string ExchangeType { get; set; } = "topic";

    public string TelemetryTopicPrefix { get; set; } = "sim.telemetry";
    public string CommandTopicPrefix { get; set; } = "sim.commands";

    /// <summary>
    /// When true, attempts automatic background reconnection if broker becomes unavailable.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Builds routing key for telemetry: "sim.telemetry.<deviceType>.<deviceId>"
    /// </summary>
    public string BuildTelemetryRoutingKey(string deviceType, string deviceId)
    {
        var cleanType = string.IsNullOrWhiteSpace(deviceType) ? "device" : deviceType.ToLowerInvariant().Replace(' ', '_');
        var cleanId = string.IsNullOrWhiteSpace(deviceId) ? "0" : deviceId.ToLowerInvariant().Replace(' ', '_');
        return $"{TelemetryTopicPrefix}.{cleanType}.{cleanId}";
    }

    /// <summary>
    /// Builds routing key for commands: "sim.commands.<deviceType>.<deviceId>"
    /// </summary>
    public string BuildCommandRoutingKey(string deviceType, string deviceId)
    {
        var cleanType = string.IsNullOrWhiteSpace(deviceType) ? "*" : deviceType.ToLowerInvariant().Replace(' ', '_');
        var cleanId = string.IsNullOrWhiteSpace(deviceId) ? "*" : deviceId.ToLowerInvariant().Replace(' ', '_');
        return $"{CommandTopicPrefix}.{cleanType}.{cleanId}";
    }

    public static RabbitMqConfiguration CreateDefault()
    {
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
        var portStr = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER");
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS");

        var config = new RabbitMqConfiguration();
        if (!string.IsNullOrWhiteSpace(host)) config.HostName = host;
        if (int.TryParse(portStr, out var p)) config.Port = p;
        if (!string.IsNullOrWhiteSpace(user)) config.UserName = user;
        if (!string.IsNullOrWhiteSpace(pass)) config.Password = pass;

        return config;
    }
}
