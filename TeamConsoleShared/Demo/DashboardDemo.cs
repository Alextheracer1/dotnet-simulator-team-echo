using Spectre.Console;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;
using TeamConsoleShared.Live;

namespace TeamConsoleShared.Demo;

/// <summary>
/// Pre-built interactive showcase demonstrating the Spectre.Console dashboard capabilities.
/// </summary>
public static class DashboardDemo
{
    public static async Task RunInteractiveDemoAsync(CancellationToken cancellationToken = default)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Team Echo")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold green]Starte Spectre.Console Dashboard Showcase...[/]");
        await Task.Delay(1200, cancellationToken);

        var simulatedTime = new DateTime(2026, 7, 15, 12, 0, 0);
        var currentPowerKw = 185.0;
        var maxPowerKw = 300.0;
        var soc1 = 0.42;
        var soc2 = 0.81;
        var isAuto = true;
        var isPaused = false;
        var hasFault = false;

        await LiveSimulationView.StartDashboardAsync(async update =>
        {
            var running = true;
            while (running && !cancellationToken.IsCancellationRequested)
            {
                // Handle key inputs if available
                while (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true).Key;
                    switch (key)
                    {
                        case ConsoleKey.P:
                            isPaused = !isPaused;
                            break;
                        case ConsoleKey.A:
                            isAuto = !isAuto;
                            break;
                        case ConsoleKey.F:
                            hasFault = !hasFault;
                            break;
                        case ConsoleKey.Add or ConsoleKey.OemPlus:
                            currentPowerKw = Math.Min(maxPowerKw, currentPowerKw + 15);
                            break;
                        case ConsoleKey.Subtract or ConsoleKey.OemMinus:
                            currentPowerKw = Math.Max(0, currentPowerKw - 15);
                            break;
                        case ConsoleKey.X or ConsoleKey.Q:
                            running = false;
                            break;
                    }
                }

                if (!isPaused)
                {
                    simulatedTime = simulatedTime.AddMinutes(1);
                    soc1 = Math.Min(1.0, soc1 + 0.005);
                    if (soc1 >= 1.0) soc1 = 0.15;
                    soc2 = Math.Min(1.0, soc2 + 0.003);
                    if (soc2 >= 1.0) soc2 = 0.20;
                }

                // Build modern dashboard
                var dashboard = new SimulationDashboard()
                    .WithHeader(h =>
                    {
                        h.Title = "⚡ E-Auto-Ladesäule Simulator (Team Echo Demo)";
                        h.Subtitle = "Feldgerät: Dual-Port DC-Schnellladestation";
                        h.ModbusPort = 1502;
                        h.ModbusUnitId = 1;
                        h.SimulatedTime = simulatedTime;
                        h.SpeedDescription = "1 Sim-Min = 200 ms";
                        h.ModeBadgeMarkup = StatusBadge.Mode(isAuto);
                        h.StatusBadgeMarkup = hasFault
                            ? StatusBadge.Fault("FEHLER #101")
                            : isPaused
                                ? StatusBadge.Paused()
                                : StatusBadge.Running();
                        h.AccentColor = Color.Teal;
                    })
                    .WithAlert(hasFault ? "KRITISCH: Übertemperatur an Ladepunkt 1 erkannt! Modbus Register 02 = 1" : null)
                    .AddSection("Netzanschluss & Lastmanagement", MetricGrid.Create(new[]
                    {
                        MetricItem.Number("Maximalleistung Station", maxPowerKw, "kW", "bold cyan", icon: "🔌"),
                        MetricItem.Number("Aktuelle Wirkleistung", currentPowerKw, "kW", currentPowerKw > 250 ? "bold red" : "bold green3", icon: "⚡"),
                        MetricItem.Number("Gesamte geladene Energie", 1420.5, "kWh", "bold white", icon: "📊"),
                        MetricItem.MarkupValue("Leistungsbalken", GaugeBar.Power(currentPowerKw, maxPowerKw, "kW", 28))
                    }))
                    .AddTwoColumns(
                        ("Ladepunkt 1 (CCS 150 kW)", MetricGrid.Create(new[]
                        {
                            MetricItem.Text("Fahrzeug", "Porsche Taycan", "bold white", "🚗"),
                            MetricItem.MarkupValue("Status", StatusBadge.Custom("LÄDT", "black", "chartreuse3")),
                            MetricItem.Number("Ladeleistung", currentPowerKw * 0.65, "kW", "bold green3"),
                            MetricItem.MarkupValue("SOC", GaugeBar.StateOfCharge(soc1, 20, targetSoc: 0.90))
                        })),
                        ("Ladepunkt 2 (CCS 150 kW)", MetricGrid.Create(new[]
                        {
                            MetricItem.Text("Fahrzeug", "Tesla Model Y", "bold white", "🚙"),
                            MetricItem.MarkupValue("Status", StatusBadge.Custom("LÄDT", "black", "chartreuse3")),
                            MetricItem.Number("Ladeleistung", currentPowerKw * 0.35, "kW", "bold green3"),
                            MetricItem.MarkupValue("SOC", GaugeBar.StateOfCharge(soc2, 20, targetSoc: 0.80))
                        }))
                    )
                    .WithKeyBindings(
                        ("P", isPaused ? "Fortsetzen" : "Pause"),
                        ("+/-", "Leistung"),
                        ("A", "Auto/Manuell"),
                        ("F", "Fehler ein/aus"),
                        ("X", "Beenden")
                    );

                update(dashboard);
                await Task.Delay(200, cancellationToken);
            }
        });

        AnsiConsole.MarkupLine("\n[bold green]✓ Demo beendet.[/]");
    }
}
