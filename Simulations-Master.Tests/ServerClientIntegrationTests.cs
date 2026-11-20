using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Battery.Simulation;
using SimulationsMaster;
using TeamShared.Simulation;
using Xunit;
using BatteryClient = Battery.Simulation.SimulationMasterClient;
using SharedClient = TeamShared.SimulationMasterClient;

namespace SimulationsMaster.Tests;

public class ServerClientIntegrationTests
{
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task HandshakeAndRegistration_EstablishesConnection_AndReceivesEnvironment()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            await using var client = new SharedClient("127.0.0.1", port, "station-test-01", "charger");
            await client.ConnectWithRetryAsync(cts.Token);

            Assert.True(client.IsConnected);
            Assert.Equal(1, server.ConnectedClientCount);

            var env = await client.RequestEnvironmentAsync(cts.Token);
            Assert.NotNull(env);
            Assert.Equal(engine.CurrentVirtualTime, env.VirtualTime);
            Assert.Equal(engine.TimeStep, env.TimeStep);
            Assert.False(env.IsRunning);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task BatteryClient_HandshakeAndRegistration_Succeeds()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            await using var client = new BatteryClient("127.0.0.1", port);
            await client.ConnectWithRetryAsync(cts.Token);

            Assert.True(client.IsConnected);
            Assert.Equal(1, server.ConnectedClientCount);

            var env = await client.RequestEnvironmentAsync(cts.Token);
            Assert.NotNull(env);
            Assert.Equal(engine.CurrentVirtualTime, env.VirtualTime);
            Assert.Equal(engine.CurrentVirtualTime, client.LastKnownVirtualTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task TimeStepBarrier_SingleClient_SynchronizesVirtualTime()
    {
        var initialTime = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var port = GetFreePort();
        var engine = new SimulationEngine(initialTime);
        engine.ChangeTimeStep(TimeSpan.FromSeconds(5));
        engine.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            await using var client = new SharedClient("127.0.0.1", port, "station-pv-01", "pv");
            await client.ConnectWithRetryAsync(cts.Token);

            // Consume initial registered environment
            var firstMsg = await client.WaitForNextMessageAsync(cts.Token);
            Assert.NotNull(firstMsg);
            Assert.Equal(SimulationMessageTypes.Environment, firstMsg.Type);

            // Wait for TimeStep message from server barrier loop
            SimulationMessage? stepMsg = null;
            while (!cts.IsCancellationRequested)
            {
                var msg = await client.WaitForNextMessageAsync(cts.Token);
                if (msg?.Type == SimulationMessageTypes.TimeStep)
                {
                    stepMsg = msg;
                    break;
                }
            }

            Assert.NotNull(stepMsg);
            Assert.Equal(initialTime + TimeSpan.FromSeconds(5), stepMsg.VirtualTime);

            // Send acknowledgement
            await client.SendAcknowledgementAsync(stepMsg.Sequence, stepMsg.VirtualTime, cts.Token);

            // Allow master barrier to commit
            await Task.Delay(250, cts.Token);

            Assert.Equal(initialTime + TimeSpan.FromSeconds(5), engine.CurrentVirtualTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task TimeStepBarrier_MultiClient_AdvancesOnlyWhenAllClientsAcknowledge()
    {
        var initialTime = new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc);
        var port = GetFreePort();
        var engine = new SimulationEngine(initialTime);
        engine.ChangeTimeStep(TimeSpan.FromSeconds(10));
        engine.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            // Connect 2 clients: Client 1 (Charger) and Client 2 (Battery)
            await using var client1 = new SharedClient("127.0.0.1", port, "station-charger-01", "charger");
            await using var client2 = new SharedClient("127.0.0.1", port, "station-battery-01", "battery");

            await client1.ConnectWithRetryAsync(cts.Token);
            await client2.ConnectWithRetryAsync(cts.Token);

            Assert.Equal(2, server.ConnectedClientCount);

            // Consume registration environment
            await client1.WaitForNextMessageAsync(cts.Token);
            await client2.WaitForNextMessageAsync(cts.Token);

            // Both clients wait for TimeStep
            var step1Task = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var m = await client1.WaitForNextMessageAsync(cts.Token);
                    if (m?.Type == SimulationMessageTypes.TimeStep) return m;
                }
                return null;
            });

            var step2Task = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var m = await client2.WaitForNextMessageAsync(cts.Token);
                    if (m?.Type == SimulationMessageTypes.TimeStep) return m;
                }
                return null;
            });

            var step1 = await step1Task;
            var step2 = await step2Task;

            Assert.NotNull(step1);
            Assert.NotNull(step2);
            Assert.Equal(step1.Sequence, step2.Sequence);
            Assert.Equal(step1.VirtualTime, step2.VirtualTime);

            // 1. Only Client 1 acknowledges first!
            await client1.SendAcknowledgementAsync(step1.Sequence, step1.VirtualTime, cts.Token);

            // Wait briefly to allow processing
            await Task.Delay(250, cts.Token);

            // Invariant: Virtual time must NOT advance because Client 2 has not acknowledged!
            Assert.Equal(initialTime, engine.CurrentVirtualTime);

            // 2. Now Client 2 acknowledges!
            await client2.SendAcknowledgementAsync(step2.Sequence, step2.VirtualTime, cts.Token);

            // Allow master barrier to complete
            await Task.Delay(250, cts.Token);

            // Both acknowledged -> Virtual time must now advance!
            Assert.Equal(initialTime + TimeSpan.FromSeconds(10), engine.CurrentVirtualTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task ClientTelemetry_SendState_ProcessedWithoutInterruptingBarrier()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 6, 1, 16, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            await using var client = new SharedClient("127.0.0.1", port, "station-state-test", "battery");
            await client.ConnectWithRetryAsync(cts.Token);

            var stateMsg = new SimulationMessage
            {
                ActivePowerKW = 4.5,
                CumulativeEnergyKWh = 120.0,
                StateOfChargePercent = 65.5,
                Status = "Charging",
                IsRunning = true,
                IsOn = true
            };

            await client.SendStateAsync(stateMsg, cts.Token);
            await Task.Delay(150, cts.Token);

            Assert.True(client.IsConnected);
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
    public async Task ClientReconnection_ResumesCommunication_Cleanly()
    {
        var port = GetFreePort();
        var engine = new SimulationEngine(new DateTime(2026, 6, 1, 17, 0, 0, DateTimeKind.Utc));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            // Connect client 1
            var client1 = new SharedClient("127.0.0.1", port, "reconnecting-station", "charger");
            await client1.ConnectWithRetryAsync(cts.Token);
            Assert.True(client1.IsConnected);
            Assert.Equal(1, server.ConnectedClientCount);

            // Disconnect client 1
            await client1.DisposeAsync();
            await Task.Delay(200, cts.Token);
            Assert.Equal(0, server.ConnectedClientCount);

            // Reconnect via client 2
            await using var client2 = new SharedClient("127.0.0.1", port, "reconnecting-station", "charger");
            await client2.ConnectWithRetryAsync(cts.Token);
            Assert.True(client2.IsConnected);
            Assert.Equal(1, server.ConnectedClientCount);

            var env = await client2.RequestEnvironmentAsync(cts.Token);
            Assert.NotNull(env);
            Assert.Equal(engine.CurrentVirtualTime, env.VirtualTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task AbruptDisconnect_ServerRemovesClient_AndBarrierContinuesForRemaining()
    {
        var initialTime = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc);
        var port = GetFreePort();
        var engine = new SimulationEngine(initialTime);
        engine.ChangeTimeStep(TimeSpan.FromSeconds(2));
        engine.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new TcpSimulationServer(port, engine);
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        try
        {
            await Task.Delay(150, cts.Token);

            await using var healthyClient = new SharedClient("127.0.0.1", port, "healthy-client", "charger");
            var dyingClient = new SharedClient("127.0.0.1", port, "dying-client", "battery");

            await healthyClient.ConnectWithRetryAsync(cts.Token);
            await dyingClient.ConnectWithRetryAsync(cts.Token);

            Assert.Equal(2, server.ConnectedClientCount);

            // Consume initial environment
            await healthyClient.WaitForNextMessageAsync(cts.Token);
            await dyingClient.WaitForNextMessageAsync(cts.Token);

            // Abruptly dispose dyingClient
            await dyingClient.DisposeAsync();

            // Wait for server to detect disconnection on dyingClient
            await Task.Delay(300, cts.Token);
            Assert.Equal(1, server.ConnectedClientCount);

            // Healthy client waits for next TimeStep
            SimulationMessage? step = null;
            while (!cts.IsCancellationRequested)
            {
                var m = await healthyClient.WaitForNextMessageAsync(cts.Token);
                if (m?.Type == SimulationMessageTypes.TimeStep)
                {
                    step = m;
                    break;
                }
            }

            Assert.NotNull(step);

            // Healthy client acknowledges
            await healthyClient.SendAcknowledgementAsync(step.Sequence, step.VirtualTime, cts.Token);

            await Task.Delay(250, cts.Token);

            // Time advances smoothly because dyingClient is no longer in the barrier
            Assert.True(engine.CurrentVirtualTime > initialTime);
        }
        finally
        {
            cts.Cancel();
            await server.StopAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
