using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using TeamConsoleShared.Components;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

namespace RabbitMqViewer;

class Program
{
    private static readonly ConcurrentDictionary<string, (DeviceTelemetryMessage Telemetry, DateTime ReceivedAt, long MessageCount)> Devices = new();
    private static readonly List<string> RecentLogs = new();
    private static readonly object LogLock = new();

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var config = RabbitMqConfiguration.CreateDefault();
        await using var consumer = new RabbitMqConsumer(config);
        await using var publisher = new RabbitMqPublisher(config);

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("RabbitMQ Viewer")
                .Color(Color.DeepSkyBlue1));

        AnsiConsole.MarkupLine("[bold cyan]Verbinde mit RabbitMQ und abonniere Telemetrie (sim.telemetry.#)...[/]");

        await consumer.StartAsync(cts.Token);
        await publisher.StartAsync(cts.Token);

        await consumer.SubscribeTelemetryAsync("sim.telemetry.#", async telemetry =>
        {
            Devices.AddOrUpdate(
                telemetry.DeviceId,
                (telemetry, DateTime.Now, 1),
                (key, existing) => (telemetry, DateTime.Now, existing.MessageCount + 1));

            AddLog($"Telemetrie empfangen von [bold]{telemetry.DeviceId}[/] ({telemetry.ActivePowerKw:F1} kW)");
            await Task.CompletedTask;
        }, cts.Token);

        var running = true;
        while (running && !cts.IsCancellationRequested)
        {
            while (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;
                switch (key)
                {
                    case ConsoleKey.C:
                        // Interactive command sender
                        await SendTestCommandAsync(publisher, cts.Token);
                        break;
                    case ConsoleKey.R:
                        Devices.Clear();
                        AddLog("Geräteliste zurückgesetzt.");
                        break;
                    case ConsoleKey.Q or ConsoleKey.X:
                        running = false;
                        break;
                }
            }

            RenderDashboard(config, consumer.IsConnected, consumer.ReceivedCount);

            try
            {
                await Task.Delay(500, cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        AnsiConsole.MarkupLine("[bold yellow]RabbitMQ Viewer beendet.[/]");
    }

    private static void RenderDashboard(RabbitMqConfiguration config, bool isConnected, long totalMessages)
    {
        Console.SetCursorPosition(0, 0);

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();

        var statusBadge = isConnected
            ? "[bold chartreuse3]● ONLINE[/]"
            : "[bold red]● OFFLINE (Warte auf Broker)[/]";

        headerGrid.AddRow(
            new Markup($"[bold]Broker:[/] {config.HostName}:{config.Port} | [bold]Status:[/] {statusBadge}"),
            new Markup($"[bold]Empfangene Pakete:[/] [cyan]{totalMessages}[/] | [bold]Aktive Geräte:[/] [green]{Devices.Count}[/]")
        );

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.Title("[bold deepskyblue1]📡 Aktive Feldgeräte-Telemetrie (RabbitMQ Live)[/]");

        table.AddColumn(new TableColumn("[bold]Geräte-ID[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Typ[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Zustand[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Wirkleistung[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Auslastung[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Energie[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Gerätespezifische Metriken[/]"));
        table.AddColumn(new TableColumn("[bold]Pakete[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Zuletzt gesehen[/]").Centered());

        if (Devices.IsEmpty)
        {
            table.AddRow(
                new Markup("[grey]Keine Daten[/]"),
                new Markup("[grey]-[/]"),
                new Markup("[grey]-[/]"),
                new Markup("[grey]-[/]"),
                new Markup("[grey]-[/]"),
                new Markup("[grey]-[/]"),
                new Markup("[grey]Warte auf Telemetrienachrichten aus 'sim.telemetry.#'...[/]"),
                new Markup("[grey]0[/]"),
                new Markup("[grey]-[/]")
            );
        }
        else
        {
            foreach (var kvp in Devices.OrderBy(d => d.Key))
            {
                var (tel, receivedAt, count) = kvp.Value;
                var age = (DateTime.Now - receivedAt).TotalSeconds;
                var ageMarkup = age < 3 ? $"[green]{age:F1}s[/]" : (age < 10 ? $"[yellow]{age:F1}s[/]" : $"[red]{age:F1}s[/]");

                var powerKw = tel.ActivePowerKw;
                var maxKw = tel.DeviceType.ToLowerInvariant() switch
                {
                    "charger" => 300.0,
                    "battery" => 150.0,
                    "pv" => 50.0,
                    "wind" => 200.0,
                    _ => Math.Max(100.0, powerKw * 1.2)
                };

                var bar = GaugeBar.Power(powerKw, maxKw, "kW", 16);

                var stateMarkup = tel.OperatingState switch
                {
                    2 => "[chartreuse3]Running[/]",
                    3 => "[red]Error[/]",
                    1 => "[yellow]Booting[/]",
                    0 => "[grey]Off[/]",
                    _ => "Unknown"
                };

                var metricsList = new List<string>();
                foreach (var (k, v) in tel.DeviceSpecificData)
                {
                    if (k.Contains("SOC", StringComparison.OrdinalIgnoreCase))
                        metricsList.Add($"{k}: [bold cyan]{v:P0}[/]");
                    else
                        metricsList.Add($"{k}: {v:F1}");
                }
                var metricsText = metricsList.Count > 0 ? string.Join(" | ", metricsList) : "-";

                table.AddRow(
                    new Markup($"[bold white]{tel.DeviceId}[/]"),
                    new Markup($"[cyan]{tel.DeviceType}[/]"),
                    new Markup(stateMarkup),
                    new Markup($"[bold]{powerKw:F1} kW[/]"),
                    new Markup(bar),
                    new Markup($"{tel.CumulativeEnergyKwh:F1} kWh"),
                    new Markup(metricsText),
                    new Markup($"[grey]{count}[/]"),
                    new Markup(ageMarkup)
                );
            }
        }

        var logContent = string.Join("\n", RecentLogs.TakeLast(6));
        var logPanel = new Panel(new Markup(string.IsNullOrEmpty(logContent) ? "[grey]Noch keine Ereignisse...[/]" : logContent))
        {
            Header = new PanelHeader("[bold grey]Live Nachrichten-Stream[/]"),
            Border = BoxBorder.Rounded
        };

        var footer = new Markup("[grey]Tasten: [bold yellow][C][/] Test-Befehl senden | [bold cyan][R][/] Liste leeren | [bold red][Q][/] Beenden[/]");

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header", headerGrid),
                new Layout("Table", table),
                new Layout("Log", logPanel),
                new Layout("Footer", footer)
            );

        AnsiConsole.Write(layout);
    }

    private static async Task SendTestCommandAsync(IRabbitMqPublisher publisher, CancellationToken ct)
    {
        Console.SetCursorPosition(0, 22);
        AnsiConsole.MarkupLine("[bold yellow]Test-Befehl an Charger senden: SetPowerLimit auf 150000 W (150 kW)...[/]");
        var cmd = DeviceCommandMessage.CreateSetPowerLimit("charger-1", 150000);
        var success = await publisher.PublishCommandAsync(cmd, cancellationToken: ct);
        if (success)
        {
            AddLog("[bold green]Testbefehl 'SetPowerLimit: 150kW' erfolgreich an RabbitMQ gesendet![/]");
        }
        else
        {
            AddLog("[bold red]Fehler beim Senden des Testbefehls (Broker offline?)[/]");
        }
        await Task.Delay(1000, ct);
    }

    private static void AddLog(string msg)
    {
        lock (LogLock)
        {
            RecentLogs.Add($"[grey][[{DateTime.Now:HH:mm:ss}]][/] {msg}");
            if (RecentLogs.Count > 15)
            {
                RecentLogs.RemoveAt(0);
            }
        }
    }
}
