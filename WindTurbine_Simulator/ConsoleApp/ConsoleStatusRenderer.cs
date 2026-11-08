using System;
using System.Collections.Generic;
using Spectre.Console;
using Spectre.Console.Rendering;
using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.Simulation;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;

namespace WindTurbine_Simulator.ConsoleApp;

public sealed class ConsoleStatusRenderer
{
    public void Render(
        SimulationSnapshot snapshot,
        SimulationOptions options,
        double speedMultiplier,
        int? totalSteps)
    {
        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            var fallback = $"{snapshot.SimulatedAt:dd.MM.yyyy HH:mm} | Step {snapshot.Step} | Wind {snapshot.WindSpeedMps:F1} m/s | Power {snapshot.ActivePowerKw:F1} kW | Energy {snapshot.CumulativeEnergyKwh:F2} kWh";
            Console.WriteLine(fallback);
            return;
        }

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = "🌀 Windenergieanlagen Simulator (Vestas V90)";
                h.Subtitle = "Feldgerät: Onshore-Windkraftanlage";
                h.ModbusPort = options.ModbusPort;
                h.ModbusUnitId = options.ModbusUnitId;
                h.SimulatedTime = snapshot.SimulatedAt.DateTime;
                h.SpeedDescription = $"{speedMultiplier:0.##}x Tempo ({options.StepInterval.TotalSeconds:0.#}s/Schritt)";
                h.ModeBadgeMarkup = StatusBadge.Mode(true);
                h.StatusBadgeMarkup = snapshot.ActivePowerKw > 0
                    ? StatusBadge.Running("EINSPEISUNG")
                    : (snapshot.WindSpeedMps < 3.0 ? StatusBadge.Stopped("FLAUTE") : StatusBadge.Warning("STANDBY"));
                h.AccentColor = SimulationTheme.WindAccent;
            });

        if (snapshot.WindSpeedMps < 3.0)
        {
            dashboard.WithAlert("FLAUTE: Windgeschwindigkeit unter Anlaufschwelle (< 3.0 m/s). Keine Netzeinspeisung.", "bold black on yellow");
        }
        else if (snapshot.WindSpeedMps > 25.0)
        {
            dashboard.WithAlert("STURMABSCHALTUNG: Wind übersteigt Sicherheitsgrenze (> 25 m/s)! Sturmbremse aktiv.", "bold white on red");
        }

        var rotorRpm = snapshot.WindSpeedMps * 1.5;
        var pitchAngle = snapshot.ActivePowerKw >= options.RatedPowerKw ? 12.0 : 0.0;

        // Section 1: Power & Generation
        var powerMetrics = new List<MetricItem>
        {
            MetricItem.MarkupValue("Turbinenleistung", GaugeBar.Power(snapshot.ActivePowerKw, options.RatedPowerKw, "kW", 22), icon: "⚡"),
            MetricItem.Number("Nennleistung (Limit)", options.RatedPowerKw, "kW", "bold cyan", "0.0", "🔌"),
            MetricItem.Number("Tagesenergie", snapshot.CumulativeEnergyKwh, "kWh", "bold white", "0.000", "📊"),
            MetricItem.Number("Rotor-Drehzahl", rotorRpm, "rpm", "bold green3", "0.0", "🔄"),
            MetricItem.Number("Blade Pitch-Winkel", pitchAngle, "°", "bold white", "0.0", "📐")
        };

        // Section 2: Aerodynamics & Weather
        var stepText = totalSteps.HasValue ? $"{snapshot.Step} / {totalSteps.Value}" : $"{snapshot.Step}";
        var durationText = options.Duration.HasValue ? $"{options.Duration.Value:hh\\:mm} h" : "Unbegrenzt";

        var aeroMetrics = new List<MetricItem>
        {
            MetricItem.Number("Windgeschwindigkeit", snapshot.WindSpeedMps, "m/s", "bold deepskyblue1", "0.00", "💨"),
            MetricItem.Number("Rotorüberstrichene Fläche", options.RotorAreaM2, "m²", "bold white", "0", "⭕"),
            MetricItem.Number("Luftdichte", options.AirDensityKgPerM3, "kg/m³", "bold white", "0.000", "🌡️"),
            MetricItem.Number("Leistungsbeiwert (cp)", options.PowerCoefficient, "", "bold white", "0.00", "⚙️"),
            MetricItem.Text("Simulationsschritt", $"{stepText} ({durationText})", "bold white", "⏳")
        };

        dashboard.AddTwoColumns(
            ("Turbinenleistung & Netzeinspeisung", MetricGrid.Create(powerMetrics, 1)),
            ("Meteorologie & Rotordynamik", MetricGrid.Create(aeroMetrics, 1))
        );

        dashboard.WithKeyBindings(
            ("+", "Schneller"),
            ("-", "Langsamer"),
            ("P", "Pause"),
            ("Ctrl+C", "Beenden")
        );

        dashboard.Render();
    }
}
