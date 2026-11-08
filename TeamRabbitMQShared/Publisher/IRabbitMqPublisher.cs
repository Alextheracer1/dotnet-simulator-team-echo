using System;
using System.Threading;
using System.Threading.Tasks;
using TeamRabbitMQShared.Models;

namespace TeamRabbitMQShared.Publisher;

public interface IRabbitMqPublisher : IAsyncDisposable, IDisposable
{
    bool IsConnected { get; }
    long PublishedCount { get; }
    event Action<bool>? ConnectionStateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task<bool> PublishTelemetryAsync(DeviceTelemetryMessage telemetry, string? customRoutingKey = null, CancellationToken cancellationToken = default);
    Task<bool> PublishCommandAsync(DeviceCommandMessage command, string? customRoutingKey = null, CancellationToken cancellationToken = default);
    Task<bool> PublishRawAsync(string routingKey, byte[] payload, string? contentType = "application/json", CancellationToken cancellationToken = default);
}
