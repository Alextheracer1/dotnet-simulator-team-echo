using System;
using System.Threading;
using System.Threading.Tasks;
using TeamRabbitMQShared.Models;

namespace TeamRabbitMQShared.Consumer;

public interface IRabbitMqConsumer : IAsyncDisposable, IDisposable
{
    bool IsConnected { get; }
    long ReceivedCount { get; }
    event Action<bool>? ConnectionStateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task<string> SubscribeTelemetryAsync(string topicPattern, Func<DeviceTelemetryMessage, Task> handler, CancellationToken cancellationToken = default);
    Task<string> SubscribeCommandsAsync(string topicPattern, Func<DeviceCommandMessage, Task> handler, CancellationToken cancellationToken = default);
    Task<string> SubscribeRawAsync(string topicPattern, Func<string, byte[], Task> handler, CancellationToken cancellationToken = default);
}
