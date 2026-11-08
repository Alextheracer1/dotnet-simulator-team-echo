using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

namespace ModbusRabbitMqGateway;

public class GatewayService : IAsyncDisposable
{
    private readonly GatewayConfiguration _config;
    private readonly IRabbitMqPublisher _publisher;
    private readonly IRabbitMqConsumer _consumer;
    private readonly List<ModbusDeviceBridge> _bridges = new();

    private long _totalMessagesPublished;
    private long _totalCommandsProcessed;
    private bool _isRunning;

    public IReadOnlyList<ModbusDeviceBridge> Bridges => _bridges;
    public bool RabbitMqPublisherConnected => _publisher.IsConnected;
    public bool RabbitMqConsumerConnected => _consumer.IsConnected;
    public long TotalMessagesPublished => Interlocked.Read(ref _totalMessagesPublished);
    public long TotalCommandsProcessed => Interlocked.Read(ref _totalCommandsProcessed);
    public List<string> RecentActivity { get; } = new();
    private readonly object _activityLock = new();

    public GatewayService(GatewayConfiguration config, IRabbitMqPublisher publisher, IRabbitMqConsumer consumer)
    {
        _config = config;
        _publisher = publisher;
        _consumer = consumer;

        foreach (var endpoint in _config.Endpoints)
        {
            _bridges.Add(new ModbusDeviceBridge(endpoint));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        LogActivity("Starting Modbus-RabbitMQ Gateway...");

        await _publisher.StartAsync(cancellationToken);
        await _consumer.StartAsync(cancellationToken);

        // Subscribe to commands for all devices
        await _consumer.SubscribeCommandsAsync("sim.commands.#", async command =>
        {
            await HandleCommandAsync(command);
        }, cancellationToken);

        LogActivity("Subscribed to command topic 'sim.commands.#'");
    }

    public async Task PollAndRelayAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning) return;

        foreach (var bridge in _bridges)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var telemetry = bridge.PollTelemetry();
            if (telemetry is not null)
            {
                var success = await _publisher.PublishTelemetryAsync(telemetry, cancellationToken: cancellationToken);
                if (success)
                {
                    Interlocked.Increment(ref _totalMessagesPublished);
                }
            }
        }
    }

    private async Task HandleCommandAsync(DeviceCommandMessage command)
    {
        Interlocked.Increment(ref _totalCommandsProcessed);
        LogActivity($"Received command: {command.CommandType} for {command.TargetDeviceId} (Value: {command.Value})");

        var targetBridges = _bridges.Where(b =>
            command.TargetDeviceId == "*" ||
            b.Endpoint.DeviceId.Equals(command.TargetDeviceId, StringComparison.OrdinalIgnoreCase) ||
            b.Endpoint.DeviceType.Equals(command.TargetDeviceType, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var bridge in targetBridges)
        {
            var executed = bridge.ExecuteCommand(command);
            if (executed)
            {
                LogActivity($"Successfully wrote {command.CommandType} to {bridge.Endpoint.DeviceId} via Modbus");
            }
            else
            {
                LogActivity($"Failed writing command to {bridge.Endpoint.DeviceId}: {bridge.LastError}");
            }
        }

        await Task.CompletedTask;
    }

    public void LogActivity(string message)
    {
        lock (_activityLock)
        {
            RecentActivity.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (RecentActivity.Count > 10)
            {
                RecentActivity.RemoveAt(0);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isRunning = false;
        foreach (var bridge in _bridges)
        {
            bridge.Dispose();
        }

        await _publisher.DisposeAsync();
        await _consumer.DisposeAsync();
    }
}
