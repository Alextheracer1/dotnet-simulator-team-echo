using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TeamShared.Simulation;

namespace SimulationsMaster;

/// <summary>Hosts persistent station connections and coordinates time-step acknowledgement barriers.</summary>
public sealed class TcpSimulationServer : IAsyncDisposable
{
    private static readonly TimeSpan StepInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AcknowledgementTimeout = TimeSpan.FromSeconds(5);

    private readonly int _port;
    private readonly SimulationEngine _engine;
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private TcpListener? _listener;
    private long _sequence;

    public TcpSimulationServer(int port, SimulationEngine engine)
    {
        _port = port;
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public int ConnectedClientCount => _clients.Count;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            listener.Stop();
            throw new InvalidOperationException(
                $"Cannot bind the Simulation Master to TCP port {_port}. Another process may already be using it.",
                exception);
        }

        _listener = listener;
        var barrierTask = RunBarrierLoopAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var id = Guid.NewGuid();
                var session = new ClientSession(id, client);
                _clients.TryAdd(id, session);
                session.HandlerTask = HandleClientAsync(session, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
            _listener = null;
            foreach (var session in _clients.Values)
                session.Client.Dispose();

            try
            {
                await barrierTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    public async Task StopAsync()
    {
        _listener?.Stop();
        _listener = null;

        var sessions = _clients.Values.ToArray();
        foreach (var session in sessions)
            session.Client.Dispose();

        var handlers = sessions.Select(session => session.HandlerTask).Where(task => task is not null).Cast<Task>();
        try
        {
            await Task.WhenAll(handlers).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task HandleClientAsync(ClientSession session, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = session.Client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            session.Writer = writer;

            await WriteMessageAsync(session, CreateEnvironmentMessage(), cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var json = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (json is null)
                    break;

                SimulationMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<SimulationMessage>(json, _jsonOptions);
                }
                catch (JsonException)
                {
                    await WriteMessageAsync(session, new SimulationMessage
                    {
                        Type = SimulationMessageTypes.Error,
                        Status = "Invalid JSON message"
                    }, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (message is null)
                    continue;

                switch (message.Type)
                {
                    case SimulationMessageTypes.EnvironmentRequest:
                        await WriteMessageAsync(session, CreateEnvironmentMessage(), cancellationToken).ConfigureAwait(false);
                        break;

                    case SimulationMessageTypes.Register:
                        session.StationId = string.IsNullOrWhiteSpace(message.StationId) ? session.Id.ToString("N") : message.StationId;
                        session.IsRegistered = true;
                        if (!_engine.IsRunning && message.VirtualTime != default)
                            _engine.ReconcileFromStation(message.VirtualTime);
                        await WriteMessageAsync(session, CreateEnvironmentMessage(), cancellationToken).ConfigureAwait(false);
                        break;

                    case SimulationMessageTypes.State:
                        break;

                    case SimulationMessageTypes.Acknowledgement:
                        if (session.PendingAcknowledgement is { } pending &&
                            pending.Sequence == message.Sequence &&
                            pending.ExpectedVirtualTime == message.VirtualTime)
                            pending.Completion.TrySetResult(true);
                        break;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _clients.TryRemove(session.Id, out _);
            session.Client.Dispose();
        }
    }

    private async Task RunBarrierLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StepInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var environment = CreateEnvironmentMessage();
            var registered = _clients.Values.Where(client => client.IsRegistered).ToArray();

            if (!_engine.IsRunning)
            {
                await BroadcastAsync(environment, registered, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var previousTime = _engine.CurrentVirtualTime;
            var timeStep = _engine.TimeStep;
            var sequence = Interlocked.Increment(ref _sequence);
            var nextTime = previousTime + timeStep;
            var timeStepMessage = new SimulationMessage
            {
                Type = SimulationMessageTypes.TimeStep,
                Sequence = sequence,
                PreviousVirtualTime = previousTime,
                VirtualTime = nextTime,
                TimeStep = timeStep,
                IsRunning = true
            };

            foreach (var client in registered)
                client.PendingAcknowledgement = new PendingAck(sequence, nextTime);

            await BroadcastAsync(timeStepMessage, registered, cancellationToken).ConfigureAwait(false);

            var allAcknowledged = true;
            if (registered.Length > 0)
            {
                var acknowledgements = registered.Select(client => client.PendingAcknowledgement!.Completion.Task);
                try
                {
                    await Task.WhenAll(acknowledgements)
                        .WaitAsync(AcknowledgementTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    var timedOutClients = registered
                        .Where(client => client.PendingAcknowledgement?.Completion.Task.IsCompleted != true)
                        .ToArray();
                    foreach (var client in timedOutClients)
                    {
                        _clients.TryRemove(client.Id, out _);
                        client.Client.Dispose();
                    }

                    allAcknowledged = registered.Except(timedOutClients).All(client =>
                        client.PendingAcknowledgement?.Completion.Task.IsCompleted == true);
                }
            }

            foreach (var client in registered)
                client.PendingAcknowledgement = null;

            if (allAcknowledged)
                _engine.CommitTimeStep(previousTime, timeStep);
        }
    }

    private async Task BroadcastAsync(SimulationMessage message, IEnumerable<ClientSession> sessions, CancellationToken cancellationToken)
    {
        var sends = sessions.Select(session => WriteMessageAsync(session, message, cancellationToken));
        try
        {
            await Task.WhenAll(sends).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
        {
            foreach (var session in sessions)
            {
                if (session.Client.Connected)
                    continue;
                _clients.TryRemove(session.Id, out _);
                session.Client.Dispose();
            }
        }
    }

    private async Task WriteMessageAsync(ClientSession session, SimulationMessage message, CancellationToken cancellationToken)
    {
        var writer = session.Writer;
        if (writer is null)
            return;

        await session.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(message, _jsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            session.WriteLock.Release();
        }
    }

    private SimulationMessage CreateEnvironmentMessage()
    {
        var snapshot = _engine.GetStatusSnapshot();
        return new SimulationMessage
        {
            Type = SimulationMessageTypes.Environment,
            Sequence = Interlocked.Read(ref _sequence),
            VirtualTime = snapshot.CurrentVirtualTime,
            TimeStep = snapshot.TimeStep,
            IsRunning = snapshot.IsRunning,
            Status = "Environment"
        };
    }

    private sealed class ClientSession(Guid id, TcpClient client)
    {
        public Guid Id { get; } = id;
        public TcpClient Client { get; } = client;
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public StreamWriter? Writer { get; set; }
        public string? StationId { get; set; }
        public bool IsRegistered { get; set; }
        public Task? HandlerTask { get; set; }
        public PendingAck? PendingAcknowledgement { get; set; }
    }

    private sealed class PendingAck(long sequence, DateTime expectedVirtualTime)
    {
        public long Sequence { get; } = sequence;
        public DateTime ExpectedVirtualTime { get; } = expectedVirtualTime;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
