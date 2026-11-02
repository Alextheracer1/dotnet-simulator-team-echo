using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SimulationsMaster;

public static class Program
{
    private const int DefaultPort = 1510;
    private const string TimeStepEnvironmentVariable = "SIMULATION_MASTER_TIME_STEP";

    public static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var engine = new SimulationEngine(DateTime.UtcNow);
        engine.ChangeTimeStep(GetTimeStepFromEnvironment(TimeStepEnvironmentVariable, TimeSpan.FromSeconds(1)));
        var port = GetPortFromEnvironment("SIMULATION_MASTER_PORT", DefaultPort);
        var server = new TcpSimulationServer(port, engine);

        var serverTask = server.StartAsync(cancellationTokenSource.Token);

        try
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (serverTask.IsFaulted)
                    await serverTask.ConfigureAwait(false);

                RenderDashboard(engine, server, port);

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    HandleKey(engine, key.Key, cancellationTokenSource);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Net.Sockets.SocketException)
        {
            Console.Error.WriteLine($"Simulation Master server error: {exception.Message}");
        }
        finally
        {
            cancellationTokenSource.Cancel();
            try
            {
                await server.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.Net.Sockets.SocketException)
            {
                Console.Error.WriteLine($"Simulation Master shutdown warning: {exception.Message}");
            }

            if (!serverTask.IsFaulted && !serverTask.IsCanceled)
                await serverTask.ConfigureAwait(false);
            Console.WriteLine();
            Console.WriteLine("Simulations-Master stopped.");
        }
    }

    private static void HandleKey(SimulationEngine engine, ConsoleKey key, CancellationTokenSource cancellationTokenSource)
    {
        switch (key)
        {
            case ConsoleKey.Q:
                cancellationTokenSource.Cancel();
                break;

            case ConsoleKey.P:
                engine.Pause();
                break;

            case ConsoleKey.R:
                engine.Resume();
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                AdjustTimeStep(engine, increase: true);
                break;

            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                AdjustTimeStep(engine, increase: false);
                break;

            case ConsoleKey.S:
                engine.Start();
                break;
        }
    }

    private static void AdjustTimeStep(SimulationEngine engine, bool increase)
    {
        var timeSteps = engine.GetAvailableTimeSteps();
        var index = Array.IndexOf(timeSteps, engine.TimeStep);
        if (index < 0)
        {
            var nearest = engine.GetNearestTimeStep(engine.TimeStep);
            index = Array.IndexOf(timeSteps, nearest);
        }

        if (index < 0)
        {
            engine.ChangeTimeStep(timeSteps[0]);
            return;
        }

        var nextIndex = increase ? Math.Min(index + 1, timeSteps.Length - 1) : Math.Max(index - 1, 0);
        engine.ChangeTimeStep(timeSteps[nextIndex]);
    }

    private static void RenderDashboard(SimulationEngine engine, TcpSimulationServer server, int port)
    {
        Console.Clear();
        Console.WriteLine("SIMULATIONS-MASTER");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine("STATUS");

        var snapshot = engine.GetStatusSnapshot();
        Console.WriteLine($"  Running            : {(snapshot.IsRunning ? "YES" : "NO")}");
        Console.WriteLine($"  Virtual Time       : {snapshot.CurrentVirtualTime:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"  Start Virtual Time : {snapshot.StartVirtualTime:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"  Time Step / Tick   : {FormatTimeStep(snapshot.TimeStep)}");
        Console.WriteLine($"  Connected Clients  : {server.ConnectedClientCount}");

        Console.WriteLine();
        Console.WriteLine("CONTROLS");
        Console.WriteLine("  P   Pause");
        Console.WriteLine("  R   Resume");
        Console.WriteLine("  S   Start");
        Console.WriteLine("  +   Increase time step");
        Console.WriteLine("  -   Decrease time step");
        Console.WriteLine("  Q   Quit");
        Console.WriteLine();
        Console.WriteLine("  Time-step ladder: 1s, 10s, 1m, 15m, 1h, 2h, 12h, 24h, 7d");
        Console.WriteLine();
        Console.WriteLine($"Port: {port}");
    }

    private static int GetPortFromEnvironment(string variableName, int defaultPort)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            return defaultPort;

        if (int.TryParse(value, out var port) && port is >= 1 and <= 65535)
            return port;

        throw new InvalidOperationException($"Environment variable {variableName} must contain a TCP port from 1 through 65535.");
    }

    private static TimeSpan GetTimeStepFromEnvironment(string variableName, TimeSpan defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Trim();
        var unit = char.ToLowerInvariant(normalized[^1]);
        var numberText = normalized[..^1];
        if (unit is 's' or 'm' or 'h' or 'd' &&
            double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) &&
            double.IsFinite(amount) && amount > 0)
        {
            try
            {
                var parsed = unit switch
                {
                    's' => TimeSpan.FromSeconds(amount),
                    'm' => TimeSpan.FromMinutes(amount),
                    'h' => TimeSpan.FromHours(amount),
                    'd' => TimeSpan.FromDays(amount),
                    _ => defaultValue
                };
                if (parsed > TimeSpan.Zero)
                    return parsed;
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException(
                    $"Environment variable {variableName} must specify a duration within the supported TimeSpan range.");
            }
        }

        if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var timeStep) && timeStep > TimeSpan.Zero)
            return timeStep;

        throw new InvalidOperationException(
            $"Environment variable {variableName} must be a positive duration such as 15m or 00:15:00.");
    }

    private static string FormatTimeStep(TimeSpan timeStep)
    {
        if (timeStep == TimeSpan.FromDays(1))
            return "24h";
        if (timeStep.Ticks % TimeSpan.TicksPerDay == 0)
            return $"{timeStep.TotalDays:0.##}d";
        if (timeStep.Ticks % TimeSpan.TicksPerHour == 0)
            return $"{timeStep.TotalHours:0.##}h";
        if (timeStep.Ticks % TimeSpan.TicksPerMinute == 0)
            return $"{timeStep.TotalMinutes:0.##}m";
        if (timeStep.Ticks % TimeSpan.TicksPerSecond == 0)
            return $"{timeStep.TotalSeconds:0.##}s";

        return timeStep.ToString("c", CultureInfo.InvariantCulture);
    }
}

