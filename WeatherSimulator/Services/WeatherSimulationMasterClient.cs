using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TeamShared.Simulation;

namespace WeatherSimulator.Services;

public sealed class WeatherSimulationMasterClient : ITimeMasterClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private readonly string _host;
    private readonly int _port;
    private readonly string _stationId;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentQueue<SimulationMessage> _pendingMessages = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private TcpClient? _tcpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private SimulationEnvironment? _environment;
    private DateTime _lastKnownVirtualTime;
    private TimeSpan _lastKnownTimeStep = TimeSpan.FromSeconds(1);
    private bool _hasKnownVirtualTime;
    private int _connected;

    public WeatherSimulationMasterClient(string host, int port, string stationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(stationId);
        _host = host;
        _port = port;
        _stationId = stationId;
    }

    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public DateTime LastKnownVirtualTime => _lastKnownVirtualTime;
    public TimeSpan LastKnownTimeStep => _lastKnownTimeStep;

    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is SocketException or IOException or InvalidOperationException)
            {
                attempt++;
                await DisconnectAsync().ConfigureAwait(false);
                if (Console.IsOutputRedirected)
                {
                    Console.Error.WriteLine($"[Simulation Master] Verbindung {_host}:{_port}, Versuch {attempt} fehlgeschlagen: {exception.Message}");
                }
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
                return;

            await DisposeConnectionAsync().ConfigureAwait(false);
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                var initialMessage = await ReadMessageAsync(reader, cancellationToken).ConfigureAwait(false);
                if (initialMessage.Type != SimulationMessageTypes.Environment)
                    throw new IOException($"Environment erwartet, erhalten: '{initialMessage.Type}'.");

                _tcpClient = client;
                _reader = reader;
                _writer = writer;
                Volatile.Write(ref _connected, 1);

                await WriteMessageAsync(new SimulationMessage
                {
                    Type = SimulationMessageTypes.EnvironmentRequest
                }, cancellationToken).ConfigureAwait(false);

                var environmentMessage = await ReadMessageAsync(reader, cancellationToken).ConfigureAwait(false);
                if (environmentMessage.Type != SimulationMessageTypes.Environment)
                    throw new IOException($"Environment-Antwort erwartet, erhalten: '{environmentMessage.Type}'.");

                await WriteMessageAsync(new SimulationMessage
                {
                    Type = SimulationMessageTypes.Register,
                    StationId = _stationId,
                    VirtualTime = _hasKnownVirtualTime ? _lastKnownVirtualTime : environmentMessage.VirtualTime,
                    TimeStep = _lastKnownTimeStep
                }, cancellationToken).ConfigureAwait(false);

                UpdateLastKnown(environmentMessage);
                _pendingMessages.Enqueue(environmentMessage);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public Task<SimulationEnvironment?> RequestEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsConnected ? Volatile.Read(ref _environment) : null);
    }

    public async Task<SimulationMessage?> WaitForNextMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingMessages.TryDequeue(out var pending))
            return pending;

        var reader = _reader;
        if (!IsConnected || reader is null)
            return null;

        try
        {
            var message = await ReadMessageAsync(reader, cancellationToken).ConfigureAwait(false);
            UpdateLastKnown(message);
            return message;
        }
        catch (IOException)
        {
            Volatile.Write(ref _connected, 0);
            return null;
        }
        catch (ObjectDisposedException)
        {
            Volatile.Write(ref _connected, 0);
            return null;
        }
    }

    public Task SendAcknowledgementAsync(long sequence, DateTime virtualTime, CancellationToken cancellationToken = default) =>
        WriteMessageAsync(new SimulationMessage
        {
            Type = SimulationMessageTypes.Acknowledgement,
            Sequence = sequence,
            VirtualTime = virtualTime
        }, cancellationToken);

    public Task SendStateAsync(SimulationMessage state, CancellationToken cancellationToken = default)
    {
        state.Type = SimulationMessageTypes.State;
        state.StationId ??= _stationId;
        return WriteMessageAsync(state, cancellationToken);
    }

    private async Task WriteMessageAsync(SimulationMessage message, CancellationToken cancellationToken)
    {
        var writer = _writer;
        if (!IsConnected || writer is null)
            throw new IOException("Keine Verbindung zum Simulation Master.");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(message, _jsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            Volatile.Write(ref _connected, 0);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<SimulationMessage> ReadMessageAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var json = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("Der Simulation Master hat die Verbindung geschlossen.");
        return JsonSerializer.Deserialize<SimulationMessage>(json, _jsonOptions)
            ?? throw new JsonException("Leere Synchronisationsnachricht vom Simulation Master.");
    }

    private void UpdateLastKnown(SimulationMessage message)
    {
        if (message.VirtualTime != default)
        {
            _lastKnownVirtualTime = message.VirtualTime;
            _hasKnownVirtualTime = true;
        }
        if (message.TimeStep > TimeSpan.Zero)
            _lastKnownTimeStep = message.TimeStep;
        if (message.Type is SimulationMessageTypes.Environment or SimulationMessageTypes.TimeStep)
        {
            Volatile.Write(ref _environment, new SimulationEnvironment(
                _lastKnownVirtualTime,
                message.Sequence,
                message.IsRunning,
                _lastKnownTimeStep));
        }
    }

    private async Task DisposeConnectionAsync()
    {
        Volatile.Write(ref _connected, 0);
        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }
        _reader?.Dispose();
        _reader = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        while (_pendingMessages.TryDequeue(out _))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _connectionLock.Dispose();
        _writeLock.Dispose();
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
