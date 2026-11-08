using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
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
        Console.OutputEncoding = System.Text.Encoding.UTF8;
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
            new FigletText("Modbus-RabbitMQ Gateway")
                .Color(Color.Cyan1));

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
        Console.SetCursorPosition(0, 0);

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.Title("[bold cyan]⚡ Energy Twin 2026: Modbus-RabbitMQ Gateway[/]");

        table.AddColumn(new TableColumn("[bold]Feldgerät[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Modbus Port[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Modbus Status[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Wirkleistung (kW)[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Gesamtenergie (kWh)[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Sensordaten / Metriken[/]"));
        table.AddColumn(new TableColumn("[bold]Erfolg / Fehler[/]").Centered());

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
                $"[bold]{bridge.Endpoint.DeviceId}[/] ({bridge.Endpoint.DeviceType})",
                bridge.Endpoint.Port.ToString(),
                modbusStatus,
                power,
                energy,
                extraMetrics,
                $"{bridge.PollSuccessCount} / [red]{bridge.PollFailureCount}[/]"
            );
        }

        var rabbitMqStatus = gateway.RabbitMqPublisherConnected
            ? "[bold chartreuse3]VERBUNDEN[/]"
            : "[bold red]GETRENNT (Offline)[/]";

        var summaryGrid = new Grid();
        summaryGrid.AddColumn();
        summaryGrid.AddColumn();
        summaryGrid.AddRow(
            new Markup($"[bold]RabbitMQ Broker Status:[/] {rabbitMqStatus} | [bold]Modus:[/] {(isPaused ? "[yellow]PAUSIERT[/]" : "[green]AKTIV[/]")}"),
            new Markup($"[bold]Publiziert:[/] [cyan]{gateway.TotalMessagesPublished}[/] | [bold]Befehle verarbeitet:[/] [yellow]{gateway.TotalCommandsProcessed}[/]")
        );

        var logLines = gateway.RecentActivity.TakeLast(6).Select(Markup.Escape);
        var logContent = string.Join("\n", logLines);
        var logPanel = new Panel(new Markup(string.IsNullOrEmpty(logContent) ? "[grey]Noch keine Ereignisse...[/]" : logContent))
        {
            Header = new PanelHeader("[bold grey]Ereignis- und Aktivitäts-Log[/]"),
            Border = BoxBorder.Rounded
        };

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header", summaryGrid),
                new Layout("Table", table),
                new Layout("Log", logPanel),
                new Layout("Footer", new Markup("[grey]Tasten: [P] Pause/Fortsetzen | [Q] Beenden[/]"))
            );

        AnsiConsole.Write(layout);
    }
}
