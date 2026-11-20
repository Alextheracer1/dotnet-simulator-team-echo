using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimulationsMaster;
using TeamShared.Simulation;
using Xunit;

namespace SimulationsMaster.Tests;

public class TcpSimulationServerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Server_BroadcastsEnvironment_UponClientConnection()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            // Give server a moment to bind
            await Task.Delay(150, cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            var line = await reader.ReadLineAsync(cts.Token);

            Assert.NotNull(line);
            var message = JsonSerializer.Deserialize<SimulationMessage>(line, JsonOptions);
            Assert.NotNull(message);
            Assert.Equal(SimulationMessageTypes.Environment, message.Type);
            Assert.Equal(engine.CurrentVirtualTime, message.VirtualTime);
            Assert.Equal(engine.IsRunning, message.IsRunning);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task Server_RespondsToEnvironmentRequest()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            using var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Consume initial environment greeting
            var initial = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(initial);

            // Send EnvironmentRequest
            var request = new SimulationMessage { Type = SimulationMessageTypes.EnvironmentRequest };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), cts.Token);

            // Read response
            var responseLine = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(responseLine);

            var response = JsonSerializer.Deserialize<SimulationMessage>(responseLine, JsonOptions);
            Assert.NotNull(response);
            Assert.Equal(SimulationMessageTypes.Environment, response.Type);
            Assert.Equal(engine.CurrentVirtualTime, response.VirtualTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task Server_HandlesClientRegistration_Correctly()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            using var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Consume initial greeting
            await reader.ReadLineAsync(cts.Token);

            // Register station
            var regMessage = new SimulationMessage
            {
                Type = SimulationMessageTypes.Register,
                StationId = "battery-station-1",
                StationType = "battery"
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(regMessage, JsonOptions).AsMemory(), cts.Token);

            // Registration response
            var responseLine = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(responseLine);

            var response = JsonSerializer.Deserialize<SimulationMessage>(responseLine, JsonOptions);
            Assert.NotNull(response);
            Assert.Equal(SimulationMessageTypes.Environment, response.Type);
            Assert.Equal(1, server.ConnectedClientCount);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task Server_ReturnsError_WhenMessageMalformed()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 4, 1, 14, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            using var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Consume greeting
            await reader.ReadLineAsync(cts.Token);

            // Write malformed JSON
            await writer.WriteLineAsync("{ bad json syntax !!! ".AsMemory(), cts.Token);

            var errorLine = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(errorLine);

            var error = JsonSerializer.Deserialize<SimulationMessage>(errorLine, JsonOptions);
            Assert.NotNull(error);
            Assert.Equal(SimulationMessageTypes.Error, error.Type);
            Assert.Equal("Invalid JSON message", error.Status);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task Server_AdvancesEngineVirtualTime_WhenClientAcknowledgesTimeStep()
    {
        var initialTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var port = GetFreePort();
        var engine = new SimulationEngine(initialTime);
        engine.ChangeTimeStep(TimeSpan.FromSeconds(1));
        engine.Start(); // Set engine to running

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            using var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Consume greeting
            await reader.ReadLineAsync(cts.Token);

            // Register
            var reg = new SimulationMessage
            {
                Type = SimulationMessageTypes.Register,
                StationId = "field-device-01",
                StationType = "charger"
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(reg, JsonOptions).AsMemory(), cts.Token);

            // Consume registration ack
            await reader.ReadLineAsync(cts.Token);

            // Wait for TimeStep message (PeriodicTimer ticks every 1 second)
            SimulationMessage? stepMessage = null;
            while (!cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is null) break;

                var msg = JsonSerializer.Deserialize<SimulationMessage>(line, JsonOptions);
                if (msg?.Type == SimulationMessageTypes.TimeStep)
                {
                    stepMessage = msg;
                    break;
                }
            }

            Assert.NotNull(stepMessage);
            Assert.Equal(SimulationMessageTypes.TimeStep, stepMessage.Type);
            Assert.Equal(initialTime + TimeSpan.FromSeconds(1), stepMessage.VirtualTime);

            // Send Acknowledgement
            var ack = new SimulationMessage
            {
                Type = SimulationMessageTypes.Acknowledgement,
                Sequence = stepMessage.Sequence,
                VirtualTime = stepMessage.VirtualTime
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(ack, JsonOptions).AsMemory(), cts.Token);

            // Wait briefly for server barrier loop to commit the step
            await Task.Delay(250, cts.Token);

            // Verify engine advanced
            Assert.True(engine.CurrentVirtualTime > initialTime,
                $"Expected virtual time to advance beyond {initialTime}, but was {engine.CurrentVirtualTime}");
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
