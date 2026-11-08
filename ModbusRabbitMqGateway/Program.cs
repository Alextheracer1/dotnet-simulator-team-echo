using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Publisher;

namespace ModbusRabbitMqGateway;

class Program
{
    static async Task Main(string[] args)
    {
        SimulationTheme.InitializeConsole();
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var rabbitConfig = RabbitMqConfiguration.CreateDefault();
        var publisher = new RabbitMqPublisher(rabbitConfig);
        var consumer = new RabbitMqConsumer(rabbitConfig);
        var gatewayConfig = new GatewayConfiguration();

        await using var gateway = new GatewayService(gatewayConfig, publisher, consumer);
        await gateway.StartAsync(cts.Token);

        var isPaused = false;
        var running = true;

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new Panel(new Markup("[bold cyan]⚡ Modbus-RabbitMQ Gateway[/]\n[grey62]Brücke zwischen Modbus TCP Feldgeräten und RabbitMQ Message Broker[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(foreground: SimulationTheme.GatewayAccent))
                .Expand());

        AnsiConsole.MarkupLine("[bold green]Gateway gestartet. Initialisiere Polling-Schleife...[/]");
        await Task.Delay(1000, cts.Token);

        while (running && !cts.IsCancellationRequested)
        {
            while (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;
                switch (key)
                {
                    case ConsoleKey.P:
                        isPaused = !isPaused;
                        gateway.LogActivity(isPaused ? "Polling pausiert" : "Polling fortgesetzt");
                        break;
                    case ConsoleKey.Q or ConsoleKey.X:
                        running = false;
                        break;
                }
            }

            if (!isPaused)
            {
                await gateway.PollAndRelayAsync(cts.Token);
            }

            RenderDashboard(gateway, isPaused);

            try
            {
                await Task.Delay(gatewayConfig.PollInterval, cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        AnsiConsole.MarkupLine("[bold yellow]Modbus-RabbitMQ Gateway wird beendet...[/]");
    }

    private static void RenderDashboard(GatewayService gateway, bool isPaused)
    {
        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            Console.WriteLine($"[GATEWAY] Pub: {gateway.TotalMessagesPublished} | Cmds: {gateway.TotalCommandsProcessed} | Bridges: {gateway.Bridges.Count}");
            return;
        }

        var header = new SimulationHeader
        {
            Title = "⚡ Modbus-RabbitMQ Gateway",
            Subtitle = "Bridge & Telemetrie-Publisher (Modbus TCP → sim.exchange)",
            SpeedDescription = $"{gateway.Bridges.Count} Feldgeräte",
            ModeBadgeMarkup = isPaused ? StatusBadge.Paused("PAUSIERT") : StatusBadge.Running("POLLING AKTIV"),
            StatusBadgeMarkup = gateway.RabbitMqPublisherConnected
                ? StatusBadge.Custom("BROKER ONLINE", "black", "chartreuse3")
                : StatusBadge.Fault("BROKER OFFLINE"),
            AccentColor = SimulationTheme.GatewayAccent
        };

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(foreground: SimulationTheme.GatewayAccent))
            .Expand();

        table.Title("[bold cyan]Aktive Feldgeräte-Bridges[/]");
        table.AddColumn(new TableColumn("[bold grey85]Feldgerät[/]").Centered());
        table.AddColumn(new TableColumn("[bold grey85]Port[/]").Centered());
        table.AddColumn(new TableColumn("[bold grey85]Modbus[/]").Centered());
        table.AddColumn(new TableColumn("[bold grey85]Leistung[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold grey85]Energie[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold grey85]Sensordaten / Metriken[/]"));
        table.AddColumn(new TableColumn("[bold grey85]Erfolg / Fehler[/]").Centered());

        foreach (var bridge in gateway.Bridges)
        {
            var tel = bridge.LastTelemetry;
            var modbusStatus = bridge.IsConnected
                ? "[bold chartreuse3]ONLINE[/]"
                : "[bold red]OFFLINE[/]";

            var power = tel is not null ? $"{tel.ActivePowerKw:F2} kW" : "-";
            var energy = tel is not null ? $"{tel.CumulativeEnergyKwh:F2} kWh" : "-";

            var extraMetrics = "-";
            if (tel is not null && tel.DeviceSpecificData.Count > 0)
            {
                var parts = new List<string>();
                foreach (var (k, v) in tel.DeviceSpecificData)
                {
                    if (k.Contains("SOC", StringComparison.OrdinalIgnoreCase))
                        parts.Add($"{k}: {v:P0}");
                    else
                        parts.Add($"{k}: {v:F1}");
                }
                extraMetrics = string.Join(" | ", parts);
            }

            table.AddRow(
                $"[bold white]{bridge.Endpoint.DeviceId}[/] [grey]({bridge.Endpoint.DeviceType})[/]",
                $"[cyan]{bridge.Endpoint.Port}[/]",
                modbusStatus,
                $"[bold]{power}[/]",
                energy,
                extraMetrics,
                $"{bridge.PollSuccessCount} / [red]{bridge.PollFailureCount}[/]"
            );
        }

        var logLines = gateway.RecentActivity.TakeLast(5).Select(Markup.Escape);
        var logContent = string.Join("\n", logLines);
        var logPanel = new Panel(new Markup(string.IsNullOrEmpty(logContent) ? "[grey]Noch keine Ereignisse...[/]" : logContent))
        {
            Header = new PanelHeader("[bold grey]Ereignis- und Aktivitäts-Log[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Grey42)
        }.Expand();

        var summaryItems = new List<MetricItem>
        {
            MetricItem.Text("Broker Status", gateway.RabbitMqPublisherConnected ? "Verbunden" : "Offline", gateway.RabbitMqPublisherConnected ? "bold chartreuse3" : "bold red", "🐇"),
            MetricItem.Text("Betriebsmodus", isPaused ? "Pausiert" : "Aktiv (Polling)", isPaused ? "bold yellow" : "bold green3", "⚙️"),
            MetricItem.Number("Publizierte Pakete", gateway.TotalMessagesPublished, "", "bold cyan", "0", "📤"),
            MetricItem.Number("Befehle verarbeitet", gateway.TotalCommandsProcessed, "", "bold yellow", "0", "📥")
        };
        var summaryGrid = MetricGrid.Create(summaryItems, columns: 2);
        var summaryPanel = new Panel(summaryGrid)
            .Header("[bold white]Gateway Telemetrie-Statistiken[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: Color.Grey42))
            .Expand();

        var keyBar = KeyBindingBar.Create(new[]
        {
            ("P", "Pause / Fortsetzen"),
            ("Q / X", "Beenden")
        });

        var layoutGrid = new Grid().Expand();
        layoutGrid.AddColumn(new GridColumn());
        layoutGrid.AddRow(header.Build());
        layoutGrid.AddRow(summaryPanel);
        layoutGrid.AddRow(table);
        layoutGrid.AddRow(logPanel);
        layoutGrid.AddRow(keyBar);

        SimulationTheme.PrepareFrame();
        AnsiConsole.Write(layoutGrid);
    }
}
