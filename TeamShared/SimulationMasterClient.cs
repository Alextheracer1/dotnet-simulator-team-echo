using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TeamShared.Simulation;

namespace TeamShared;

public sealed class SimulationMasterClient : ITimeMasterClient
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(15);
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
    private int _connected;
    private DateTime _lastKnownVirtualTime;
    private TimeSpan _lastKnownTimeStep = TimeSpan.FromSeconds(1);
    private SimulationEnvironment? _lastEnvironment;
    private bool _hasStationTime;

    public SimulationMasterClient(string host, int port, string stationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(stationId);
        _host = host;
        _port = port;
        _stationId = stationId;
    }

    public bool IsConnected => Volatile.Read(ref _connected) == 1;

    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is SocketException or IOException or InvalidOperationException)
            {
                await DisconnectAsync().ConfigureAwait(false);
                if (Console.IsOutputRedirected || Console.IsErrorRedirected)
                    Console.Error.WriteLine($"[Master connection] Attempt {attempt} to {_host}:{_port} failed: {exception.Message}");

                await Task.Delay(DefaultRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void UpdateStationTime(DateTime virtualTime)
    {
        _lastKnownVirtualTime = virtualTime;
        _hasStationTime = true;
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

                var initialLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (initialLine is null)
                    throw new IOException("Master closed the connection before sending its environment.");

                var initialMessage = Deserialize(initialLine);
                if (initialMessage.Type != SimulationMessageTypes.Environment)
                    throw new IOException($"Expected initial environment message, received '{initialMessage.Type}'.");

                _tcpClient = client;
                _reader = reader;
                _writer = writer;
                Volatile.Write(ref _connected, 1);

                await WriteMessageAsync(new SimulationMessage
                {
                    Type = SimulationMessageTypes.EnvironmentRequest
                }, cancellationToken, disconnectOnFailure: false).ConfigureAwait(false);
                var environmentLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (environmentLine is null)
                    throw new IOException("Master closed the connection before replying to the environment request.");

                var environment = Deserialize(environmentLine);
                if (environment.Type != SimulationMessageTypes.Environment)
                    throw new IOException($"Expected environment response, received '{environment.Type}'.");

                await WriteMessageAsync(new SimulationMessage
                {
                    Type = SimulationMessageTypes.Register,
                    StationId = _stationId,
                    VirtualTime = _hasStationTime ? _lastKnownVirtualTime : environment.VirtualTime,
                    TimeStep = _lastKnownTimeStep,
                    IsRunning = false
                }, cancellationToken, disconnectOnFailure: false).ConfigureAwait(false);

                if (!IsConnected)
                    throw new IOException("Connection to the Time Master was lost during registration.");

                _pendingMessages.Enqueue(environment);
                UpdateLastKnown(environment);
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
        return Task.FromResult(IsConnected ? Volatile.Read(ref _lastEnvironment) : null);
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
            var json = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                await DisconnectAsync().ConfigureAwait(false);
                return null;
            }

            var message = Deserialize(json);
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

    private async Task WriteMessageAsync(
        SimulationMessage message,
        CancellationToken cancellationToken,
        bool disconnectOnFailure = true)
    {
        var writer = _writer;
        if (!IsConnected || writer is null)
            throw new IOException("The Time Master connection is not available.");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(message, _jsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            Volatile.Write(ref _connected, 0);
            if (disconnectOnFailure)
                await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private SimulationMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<SimulationMessage>(json, _jsonOptions)
        ?? throw new JsonException("Master sent an empty synchronization message.");

    private void UpdateLastKnown(SimulationMessage message)
    {
        if (message.VirtualTime != default)
        {
            _lastKnownVirtualTime = message.VirtualTime;
            _hasStationTime = true;
        }
        if (message.TimeStep > TimeSpan.Zero)
            _lastKnownTimeStep = message.TimeStep;
        if (message.Type is SimulationMessageTypes.Environment or SimulationMessageTypes.TimeStep)
        {
            Volatile.Write(ref _lastEnvironment, new SimulationEnvironment(
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

        if (_reader is not null)
        {
            _reader.Dispose();
            _reader = null;
        }

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

    private async Task DisconnectAsync()
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