using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Models;

namespace TeamRabbitMQShared.Consumer;

public class RabbitMqConsumer : IRabbitMqConsumer
{
    private readonly RabbitMqConfiguration _config;
    private readonly ConnectionFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _isDisposed;
    private bool _isConnected;
    private long _receivedCount;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;

    private readonly ConcurrentBag<SubscriptionRegistration> _pendingSubscriptions = new();

    public bool IsConnected => _isConnected && _channel is { IsOpen: true };
    public long ReceivedCount => Interlocked.Read(ref _receivedCount);
    public event Action<bool>? ConnectionStateChanged;

    public RabbitMqConsumer(RabbitMqConfiguration? config = null)
    {
        _config = config ?? RabbitMqConfiguration.CreateDefault();
        _factory = new ConnectionFactory
        {
            HostName = _config.HostName,
            Port = _config.Port,
            UserName = _config.UserName,
            Password = _config.Password,
            VirtualHost = _config.VirtualHost,
            AutomaticRecoveryEnabled = true
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return true;

        if (DateTime.UtcNow - _lastConnectionAttempt < TimeSpan.FromSeconds(3))
        {
            return false;
        }

        _lastConnectionAttempt = DateTime.UtcNow;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return true;

            await CleanupConnectionAsync();

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _config.ExchangeName,
                type: _config.ExchangeType,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            SetConnected(true);

            // Re-apply any registered subscriptions
            foreach (var sub in _pendingSubscriptions)
            {
                await ActivateSubscriptionAsync(sub, cancellationToken);
            }

            return true;
        }
        catch (Exception)
        {
            SetConnected(false);
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            try
            {
                ConnectionStateChanged?.Invoke(connected);
            }
            catch { }
        }
    }

    public async Task<string> SubscribeTelemetryAsync(string topicPattern, Func<DeviceTelemetryMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        return await SubscribeRawAsync(topicPattern, async (routingKey, body) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(body);
                var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, _jsonOptions);
                if (telemetry is not null)
                {
                    await handler(telemetry);
                }
            }
            catch (Exception)
            {
                // Silently ignore corrupted or mismatched payloads
            }
        }, cancellationToken);
    }

    public async Task<string> SubscribeCommandsAsync(string topicPattern, Func<DeviceCommandMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        return await SubscribeRawAsync(topicPattern, async (routingKey, body) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(body);
                var command = JsonSerializer.Deserialize<DeviceCommandMessage>(json, _jsonOptions);
                if (command is not null)
                {
                    await handler(command);
                }
            }
            catch (Exception)
            {
                // Silently ignore corrupted payloads
            }
        }, cancellationToken);
    }

    public async Task<string> SubscribeRawAsync(string topicPattern, Func<string, byte[], Task> handler, CancellationToken cancellationToken = default)
    {
        var sub = new SubscriptionRegistration(topicPattern, handler);
        _pendingSubscriptions.Add(sub);

        var connected = await EnsureConnectedAsync(cancellationToken);
        if (connected)
        {
            await ActivateSubscriptionAsync(sub, cancellationToken);
        }

        return sub.Id;
    }

    private async Task ActivateSubscriptionAsync(SubscriptionRegistration sub, CancellationToken cancellationToken)
    {
        if (_channel is null || !_channel.IsOpen) return;

        // Create unique non-durable queue for this consumer subscription
        var queueName = $"q.{_config.ExchangeName}.{sub.Id}";
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: _config.ExchangeName,
            routingKey: sub.TopicPattern,
            arguments: null,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            Interlocked.Increment(ref _receivedCount);
            var body = ea.Body.ToArray();
            var routingKey = ea.RoutingKey;

            try
            {
                await sub.Handler(routingKey, body);
            }
            catch { }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            await CleanupConnectionAsync();
            SetConnected(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task CleanupConnectionAsync()
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
                _channel = null;
            }
        }
        catch { }

        try
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
            _connectionLock.Dispose();
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await CleanupConnectionAsync();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private class SubscriptionRegistration
    {
        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
        public string TopicPattern { get; }
        public Func<string, byte[], Task> Handler { get; }

        public SubscriptionRegistration(string topicPattern, Func<string, byte[], Task> handler)
        {
            TopicPattern = topicPattern;
            Handler = handler;
        }
    }
}
