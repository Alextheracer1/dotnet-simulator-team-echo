using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Battery.Battery;
using Battery.Simulation;
using Spectre.Console;
using Spectre.Console.Rendering;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;

namespace Battery.ConsoleUi;

public sealed class ConsoleDashboard
{
    private readonly object _inputLock = new();
    private string _inputBuffer = string.Empty;
    private readonly int _modbusPort;
    private readonly byte _modbusUnitId;

    public ConsoleDashboard(int modbusPort = SimulationOptions.DefaultModbusPort, byte modbusUnitId = 1)
    {
        _modbusPort = modbusPort;
        _modbusUnitId = modbusUnitId;
    }

    public async Task ReadCommandsAsync(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;
                await writer.WriteAsync(line, cancellationToken);
            }

            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (TryGetImmediateCommand(key, out var immediateCommand))
            {
                if (!writer.TryWrite(immediateCommand))
                    return;
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                string command;
                lock (_inputLock)
                {
                    command = _inputBuffer;
                    _inputBuffer = string.Empty;
                }

                if (!writer.TryWrite(command))
                    return;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                lock (_inputLock)
                {
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer[..^1];
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                lock (_inputLock)
                    _inputBuffer += key.KeyChar;
            }
        }
    }

    private bool TryGetImmediateCommand(ConsoleKeyInfo key, out string command)
    {
        command = key.Key switch
        {
            ConsoleKey.Add or ConsoleKey.OemPlus => "power-up",
            ConsoleKey.Subtract or ConsoleKey.OemMinus => "power-down",
            ConsoleKey.A => "auto",
            ConsoleKey.M => "manual",
            ConsoleKey.D => "discharge",
            ConsoleKey.X => "disconnect",
            ConsoleKey.I => "idle",
            ConsoleKey.P => "toggle-pause",
            ConsoleKey.Q => "quit",
            ConsoleKey.C => "charge 2.5",
            ConsoleKey.F => "fault",
            ConsoleKey.R => "reset",
            ConsoleKey.H => "help",
            _ => string.Empty
        };

        if (command.Length == 0)
            return false;

        lock (_inputLock)
            return _inputBuffer.Length == 0;
    }

    public void Render(
        DateTime simulatedTime,
        TimeSpan elapsed,
        TimeSpan duration,
        ControlMode mode,
        BatteryModel battery,
        double targetPowerKW,
        int deviceCount,
        double totalDeviceDemandKW,
        bool paused,
        StateOfChargeSummary? rollingSummary,
        string? notice = null,
        string? masterStatus = null)
    {
        var inflowKW = Math.Max(0, targetPowerKW);
        var outflowKW = Math.Max(0, totalDeviceDemandKW);
        var totalFlowKW = inflowKW - outflowKW;
        var masterText = string.IsNullOrWhiteSpace(masterStatus) ? "Offline" : masterStatus;

        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            var fallback = $"{simulatedTime:yyyy-MM-dd HH:mm:ss} {mode}{(paused ? " (Paused)" : string.Empty)} | SOC {battery.StateOfChargePercent:F1}% | Battery {battery.CurrentPowerKW:+0.00;-0.00;0.00} kW | Demand {totalDeviceDemandKW:F2} kW";
            Console.WriteLine(fallback);
            return;
        }

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = "🔋 Batteriespeicher Simulator (BESS)";
                h.Subtitle = "Feldgerät: Stationärer Lithium-Eisenphosphat Speicher";
                h.ModbusPort = _modbusPort;
                h.ModbusUnitId = _modbusUnitId;
                h.SimulatedTime = simulatedTime;
                h.ModeBadgeMarkup = StatusBadge.Mode(mode == ControlMode.Auto);
                h.StatusBadgeMarkup = battery.State == BatteryState.Fault
                    ? StatusBadge.Fault("STÖRUNG")
                    : (paused ? StatusBadge.Paused("PAUSIERT") : StatusBadge.Running("BETRIEB"));
                h.AccentColor = SimulationTheme.BatteryAccent;
            });

        if (battery.State == BatteryState.Fault)
        {
            dashboard.WithAlert($"STÖRUNG AKTIV: {battery.FaultMessage ?? "Batterie meldet Fehler"}! Drücken Sie [R] zum Zurücksetzen.");
        }
        else if (!string.IsNullOrWhiteSpace(notice) && !notice.Equals("Operating", StringComparison.OrdinalIgnoreCase))
        {
            dashboard.WithAlert($"HINWEIS: {notice}", "bold black on yellow");
        }

        var maxChargeKw = battery.MaximumChargePowerKW > 0 ? battery.MaximumChargePowerKW : BatteryModel.HardwareMaximumChargePowerKW;
        var maxDischargeKw = battery.MaximumDischargePowerKW > 0 ? battery.MaximumDischargePowerKW : BatteryModel.HardwareMaximumDischargePowerKW;
        var maxDisplayPower = Math.Max(maxChargeKw, maxDischargeKw);

        // Column 1: Battery storage and state
        var batteryMetrics = new List<MetricItem>
        {
            MetricItem.MarkupValue("Ladezustand (SOC)", GaugeBar.StateOfCharge(battery.StateOfChargePercent, 12), icon: "🔋"),
            MetricItem.Text("Kapazität", $"{battery.StateOfChargeKWh:F3} / {battery.CapacityKWh:F3} kWh", "bold white", "📦"),
            MetricItem.MarkupValue("Batterieleistung", GaugeBar.Power(Math.Abs(battery.CurrentPowerKW), maxDisplayPower, "kW", 12, battery.CurrentPowerKW >= 0 ? "Laden" : "Entladen"), icon: "⚡"),
            MetricItem.Text("Betriebsstatus", battery.State.ToString(), battery.State == BatteryState.Fault ? "bold red" : "bold green3", "⚙️"),
            MetricItem.Text("Durchsatz", $"+{battery.ChargedEnergyKWh:F1} kWh / -{battery.DischargedEnergyKWh:F1} kWh", "bold white", "🔄")
        };

        // Column 2: Network, loads and master sync
        var masterColor = masterText.Contains("ONLINE", StringComparison.OrdinalIgnoreCase) || masterText.Contains("Connected", StringComparison.OrdinalIgnoreCase)
            ? "bold chartreuse3"
            : "dim grey";
        var gridMetrics = new List<MetricItem>
        {
            MetricItem.Text("Master-Sync", masterText, masterColor, "⏱️"),
            MetricItem.Number("Netzbezug (Inflow)", inflowKW, "kW", "bold cyan", icon: "🔌"),
            MetricItem.Number("Verbraucher (Outflow)", outflowKW, "kW", "bold orange1", icon: "🏢"),
            MetricItem.Number("Gesamtfluss (Netto)", totalFlowKW, "kW", totalFlowKW >= 0 ? "bold green3" : "bold red", "+0.00;-0.00;0.00", icon: "⚖️"),
            MetricItem.Text("Verbraucherlast", $"{deviceCount} Geräte ({totalDeviceDemandKW:F2} kW)", "bold white", "💡"),
            MetricItem.Text("SOC-Verlauf (Ø/Min/Max)", rollingSummary is null ? "--" : $"{rollingSummary.AveragePercent:F1}% / {rollingSummary.MinimumPercent:F1}% / {rollingSummary.MaximumPercent:F1}%", "bold yellow", "📈")
        };

        dashboard.AddTwoColumns(
            ("Batteriespeicher & Zellstatus", MetricGrid.Create(batteryMetrics, 1)),
            ("Netzanschluss, Lasten & Takt", MetricGrid.Create(gridMetrics, 1))
        );

        string currentInput;
        lock (_inputLock)
        {
            currentInput = _inputBuffer;
        }

        if (!string.IsNullOrEmpty(currentInput))
        {
            dashboard.AddSection("Aktuelle Konsoleneingabe", new Markup($"[bold yellow]> {Markup.Escape(currentInput)}[/]"));
        }

        dashboard.WithKeyBindings(
            ("A/M", "Auto/Manuell"),
            ("D", "Entladen"),
            ("C", "Laden 2.5kW"),
            ("I", "Leerlauf"),
            ("X", "Trennen"),
            ("P", "Pause"),
            ("+/-", "Leistung"),
            ("F/R", "Fehler/Reset"),
            ("Q", "Beenden")
        );

        dashboard.Render();
    }
}
