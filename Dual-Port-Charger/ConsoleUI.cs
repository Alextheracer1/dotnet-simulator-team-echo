using Spectre.Console;
using Spectre.Console.Rendering;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;

namespace Dual_Port_Charger;

public class ConsoleUI
{
    private readonly Device _device;
    private readonly SimulatorEngine _simulator;

    public ConsoleUI(Device device, SimulatorEngine simulator)
    {
        _device = device;
        _simulator = simulator;
    }

    public SimulationDashboard BuildDashboard()
    {
        var activePowerKw = (double)(_device.ActivePower / 1000m);
        var maxPowerKw = (double)(_device.Power / 1000m);
        var totalEnergyKwh = (double)(_device.Energy / 1000m);

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = "⚡ E-Auto-Ladesäule Simulator (Dual-Port CCS)";
                h.Subtitle = "Feldgerät: DC-Schnellladestation";
                h.ModbusPort = 1502;
                h.ModbusUnitId = 1;
                h.SimulatedTime = _simulator.SimulatedTime;
                h.SpeedDescription = $"1 Sim-Min = {_simulator.DelayMs} ms";
                h.ModeBadgeMarkup = StatusBadge.Mode(_simulator.AutoMode);
                h.StatusBadgeMarkup = _device.Fault
                    ? StatusBadge.Fault("STÖRUNG")
                    : StatusBadge.Running("FUNKTIONIERT");
                h.AccentColor = Color.Teal;
            });

        if (_device.Fault)
        {
            dashboard.WithAlert("STÖRUNG AKTIV: Ladestation meldet internen Fehler! Modbus Register 02 = 1. Drücken Sie [R] zum Zurücksetzen.");
        }

        // Section 1: Station Power & Load
        dashboard.AddSection("Netzanschluss & Lastmanagement", MetricGrid.Create(new[]
        {
            MetricItem.Number("Netzanschluss Max-Limit", maxPowerKw, "kW", "bold cyan", icon: "🔌"),
            MetricItem.Number("Aktuelle Stationslast", activePowerKw, "kW", activePowerKw > maxPowerKw * 0.9 ? "bold red" : "bold green3", icon: "⚡"),
            MetricItem.Number("Geladene Gesamtenergie", totalEnergyKwh, "kWh", "bold white", icon: "📊"),
            MetricItem.MarkupValue("Auslastung", GaugeBar.Power(activePowerKw, maxPowerKw, "kW", 26))
        }, columns: 1));

        // Section 2: Charging Points (2 Columns: LP 1 and LP 2)
        var cp1 = _device.ChargingPoints[0];
        var cp2 = _device.ChargingPoints[1];

        dashboard.AddTwoColumns(
            BuildChargingPointCard(cp1),
            BuildChargingPointCard(cp2)
        );

        // Section 3: Keybindings Footer
        dashboard.WithKeyBindings(
            ("1", "Auto LP1"),
            ("2", "Auto LP2"),
            ("+/-", "Tempo"),
            ("P", "Pause"),
            ("A/M", "Auto/Manuell"),
            ("F/R", "Fehler/Reset"),
            ("↑/↓", "Leistung"),
            ("X", "Beenden")
        );

        return dashboard;
    }

    private (string Title, IRenderable Content) BuildChargingPointCard(ChargingPoint cp)
    {
        var activeKw = (double)(cp.ActivePower / 1000m);
        var requestedKw = (double)(cp.GetRequestedPower() / 1000m);
        var socFraction = (double)cp.SOC;
        var targetSocFraction = cp.IsConnected ? (double?)cp.TargetSOC : null;

        var carName = cp.IsConnected ? cp.AssignedEV.Name : "Kein Fahrzeug verbunden";

        string statusBadge = cp.Fault switch
        {
            true => StatusBadge.Fault("STÖRUNG"),
            false when !cp.IsConnected => StatusBadge.Stopped("FREI"),
            false when cp.SOC >= cp.TargetSOC => StatusBadge.Info("BEENDET"),
            _ => StatusBadge.Custom("LÄDT", "black", "chartreuse3")
        };

        var metrics = new List<MetricItem>
        {
            MetricItem.Text("Fahrzeug", carName, cp.IsConnected ? "bold white" : "dim grey", cp.IsConnected ? "🚗" : "🅿️"),
            MetricItem.MarkupValue("Status", statusBadge),
            MetricItem.MarkupValue("Leistung", $"{activeKw:F1} kW [grey58]({requestedKw:F1} kW)[/]", icon: "⚡"),
            MetricItem.MarkupValue("SOC", cp.IsConnected ? GaugeBar.StateOfCharge(socFraction, 10, targetSoc: targetSocFraction) : "[grey42]--[/]")
        };

        return ($"Ladepunkt {cp.Id} (Max: {cp.MaxPower / 1000:0} kW)", MetricGrid.Create(metrics, columns: 1));
    }

    public void Render()
    {
        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.CursorVisible = false;
                Console.SetCursorPosition(0, 0);
            }
            catch
            {
                // Ignore cursor movement errors in unsupported terminal environments
            }
        }

        AnsiConsole.Write(BuildDashboard().Build());
    }
}
