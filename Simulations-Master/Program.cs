using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;

namespace SimulationsMaster;

public static class Program
{
    private const int DefaultPort = 1510;
    private const string TimeStepEnvironmentVariable = "SIMULATION_MASTER_TIME_STEP";

    public static async Task Main(string[] args)
    {
        SimulationTheme.InitializeConsole();

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

                if (!Console.IsInputRedirected && Console.KeyAvailable)
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
            AnsiConsole.MarkupLine($"[bold red]Simulation Master server error:[/] {Markup.Escape(exception.Message)}");
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
                AnsiConsole.MarkupLine($"[yellow]Simulation Master shutdown warning:[/] {Markup.Escape(exception.Message)}");
            }

            if (!serverTask.IsFaulted && !serverTask.IsCanceled)
                await serverTask.ConfigureAwait(false);
            AnsiConsole.MarkupLine("\n[bold yellow]Simulations-Master gestoppt.[/]");
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
        var snapshot = engine.GetStatusSnapshot();

        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            Console.WriteLine($"[MASTER] Time: {snapshot.CurrentVirtualTime:yyyy-MM-dd HH:mm:ss} | Step: {FormatTimeStep(snapshot.TimeStep)} | Clients: {server.ConnectedClientCount} | Running: {snapshot.IsRunning}");
            return;
        }

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = "⏱️ Energy Twin: Simulations-Master";
                h.Subtitle = "Zentraler Taktgeber & Zeitsynchronisation";
                h.ModbusPort = port;
                h.SimulatedTime = snapshot.CurrentVirtualTime;
                h.SpeedDescription = $"{FormatTimeStep(snapshot.TimeStep)} / Tick";
                h.ModeBadgeMarkup = snapshot.IsRunning ? StatusBadge.Running("AKTIV") : StatusBadge.Paused("PAUSIERT");
                h.StatusBadgeMarkup = server.ConnectedClientCount > 0
                    ? StatusBadge.Custom($"{server.ConnectedClientCount} CLIENTS", "black", "chartreuse3")
                    : StatusBadge.Stopped("0 CLIENTS");
                h.AccentColor = SimulationTheme.MasterAccent;
            });

        if (!snapshot.IsRunning)
        {
            dashboard.WithAlert("SIMULATION PAUSIERT: Virtueller Takt angehalten. Drücken Sie [R] oder [S] zum Fortsetzen.", "bold black on yellow");
        }

        var statusMetrics = new List<MetricItem>
        {
            MetricItem.Text("Virtuelle Zeit", snapshot.CurrentVirtualTime.ToString("yyyy-MM-dd HH:mm:ss.fff"), "bold gold1", "🕐"),
            MetricItem.Text("Startzeit", snapshot.StartVirtualTime.ToString("yyyy-MM-dd HH:mm:ss"), "bold white", "🏁"),
            MetricItem.Text("Takt-Schrittweite", FormatTimeStep(snapshot.TimeStep), "bold cyan", "⏱️"),
            MetricItem.Text("Taktgeber-Zustand", snapshot.IsRunning ? "Läuft (Takt aktiv)" : "Angehalten (Pausiert)", snapshot.IsRunning ? "bold green3" : "bold yellow", "⚙️")
        };

        var networkMetrics = new List<MetricItem>
        {
            MetricItem.Number("TCP Sync Server Port", port, "", "bold cyan", "0", "🔌"),
            MetricItem.Number("Verbundene Simulatoren", server.ConnectedClientCount, "Knoten", server.ConnectedClientCount > 0 ? "bold green3" : "dim grey", "0", "🔗"),
            MetricItem.Text("Stufen-Ladder", "1s, 10s, 1m, 15m, 1h, 2h, 12h, 24h, 7d", "grey74", "📊"),
            MetricItem.Text("Protokoll-Modus", "TCP Text-Streams (TIME, STEP)", "dim grey", "📡")
        };

        dashboard.AddTwoColumns(
            ("Zeitsynchronisation & Taktung", MetricGrid.Create(statusMetrics, 1)),
            ("Netzwerk & Verbundene Clients", MetricGrid.Create(networkMetrics, 1))
        );

        dashboard.WithKeyBindings(
            ("P", "Pause"),
            ("R", "Resume"),
            ("S", "Start"),
            ("+", "Schritt +"),
            ("-", "Schritt -"),
            ("Q", "Beenden")
        );

        SimulationTheme.PrepareFrame();
        AnsiConsole.Write(dashboard.Build());
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
