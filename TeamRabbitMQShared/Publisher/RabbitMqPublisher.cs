using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Models;

namespace TeamRabbitMQShared.Publisher;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly RabbitMqConfiguration _config;
    private readonly ConnectionFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _isDisposed;
    private bool _isConnected;
    private long _publishedCount;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;

    public bool IsConnected => _isConnected && _channel is { IsOpen: true };
    public long PublishedCount => Interlocked.Read(ref _publishedCount);
    public event Action<bool>? ConnectionStateChanged;

    public RabbitMqPublisher(RabbitMqConfiguration? config = null)
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
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return true;

        // Prevent hammering reconnect attempts if broker is down
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

            // Declare topic exchange
            await _channel.ExchangeDeclareAsync(
                exchange: _config.ExchangeName,
                type: _config.ExchangeType,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            SetConnected(true);
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
            catch { /* Ignore handler exceptions */ }
        }
    }

    public async Task<bool> PublishTelemetryAsync(DeviceTelemetryMessage telemetry, string? customRoutingKey = null, CancellationToken cancellationToken = default)
    {
        var routingKey = customRoutingKey ?? _config.BuildTelemetryRoutingKey(telemetry.DeviceType, telemetry.DeviceId);
        var json = JsonSerializer.Serialize(telemetry, _jsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);

        return await PublishRawAsync(routingKey, payload, "application/json", cancellationToken);
    }

    public async Task<bool> PublishCommandAsync(DeviceCommandMessage command, string? customRoutingKey = null, CancellationToken cancellationToken = default)
    {
        var routingKey = customRoutingKey ?? _config.BuildCommandRoutingKey(command.TargetDeviceType, command.TargetDeviceId);
        var json = JsonSerializer.Serialize(command, _jsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);

        return await PublishRawAsync(routingKey, payload, "application/json", cancellationToken);
    }

    public async Task<bool> PublishRawAsync(string routingKey, byte[] payload, string? contentType = "application/json", CancellationToken cancellationToken = default)
    {
        if (_isDisposed) return false;

        var connected = await EnsureConnectedAsync(cancellationToken);
        if (!connected || _channel is null)
        {
            return false;
        }

        try
        {
            var props = new BasicProperties
            {
                ContentType = contentType ?? "application/octet-stream",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                exchange: _config.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);

            Interlocked.Increment(ref _publishedCount);
            return true;
        }
        catch (Exception)
        {
            SetConnected(false);
            return false;
        }
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
        catch { /* Ignore close error */ }

        try
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        catch { /* Ignore close error */ }
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
}
