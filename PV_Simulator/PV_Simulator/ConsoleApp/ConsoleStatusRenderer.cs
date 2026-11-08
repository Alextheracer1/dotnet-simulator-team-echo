using System;
using System.Collections.Generic;
using Spectre.Console;
using Spectre.Console.Rendering;
using PV_Simulator.Domain;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;

namespace PV_Simulator.ConsoleApp;

public sealed class ConsoleStatusRenderer
{
    public void Render(
        SimulationSnapshot snapshot,
        Location location,
        PvArray array,
        Inverter inverter,
        SimulationSettings settings,
        TimeSpan duration,
        double speedMultiplier,
        int totalSteps,
        string selectedWeatherControl)
    {
        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            var fallback = $"{snapshot.SimulatedAt:dd.MM.yyyy HH:mm} | Step {snapshot.StepNumber}/{totalSteps} | PV {snapshot.PowerKilowatts:F2} kW | {snapshot.InverterStatus} | Cloud {snapshot.Weather.CloudCoverFraction:P0}";
            Console.WriteLine(fallback);
            return;
        }

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = $"☀️ Photovoltaik Simulator ({array.DeviceId})";
                h.Subtitle = $"Standort: {location.Name} ({location.Latitude:F2}°, {location.Longitude:F2}°)";
                h.ModbusPort = 1504;
                h.ModbusUnitId = 1;
                h.SimulatedTime = snapshot.SimulatedAt.DateTime;
                h.SpeedDescription = $"{speedMultiplier:0.##}x Tempo ({settings.TimeStep.TotalMinutes:0.#}m/Schritt)";
                h.ModeBadgeMarkup = StatusBadge.Mode(!snapshot.Weather.CloudCoverManuallyOverridden && !snapshot.Weather.TemperatureManuallyOverridden);
                h.StatusBadgeMarkup = snapshot.ManualFaultActive || snapshot.InverterStatus == InverterStatus.Faulted
                    ? StatusBadge.Fault(snapshot.InverterStatus.ToString().ToUpperInvariant())
                    : (snapshot.InverterStatus == InverterStatus.Running ? StatusBadge.Running("ERZEUGUNG") : StatusBadge.Stopped("AUS"));
                h.AccentColor = SimulationTheme.PvAccent;
            });

        if (snapshot.ManualFaultActive || snapshot.InverterStatus == InverterStatus.Faulted)
        {
            dashboard.WithAlert($"WECHSELRICHTER FEHLER: {snapshot.InverterStatus}! Taste [F] zum Umschalten.");
        }
        else if (snapshot.SnowCoverActive)
        {
            dashboard.WithAlert("SCHNEEDECKE AKTIV: Solarmodule sind verschneit! Taste [S] zum Räumen / Schneefall.", "bold black on deepskyblue1");
        }
        else if (snapshot.Weather.ManualRainActive)
        {
            dashboard.WithAlert("MANUELLER REGEN AKTIV: Taste [R] zum Beenden des Niederschlags.", "bold black on dodgerblue2");
        }

        // Section 1: PV Generation & Power
        var pvMetrics = new List<MetricItem>
        {
            MetricItem.MarkupValue("PV-Leistung", GaugeBar.Power(snapshot.PowerKilowatts, array.ModuleNominalPowerKilowattsPeak, "kWp", 22), icon: "⚡"),
            MetricItem.Number("Tagesenergie", snapshot.DailyEnergyKilowattHours, "kWh", "bold white", "0.000", "📊"),
            MetricItem.Number("Sonneneinstrahlung", snapshot.IrradianceWattsPerSquareMeter, "W/m²", "bold gold1", "0", "☀️"),
            MetricItem.Number("PV-Nennleistung", array.ModuleNominalPowerKilowattsPeak, "kWp", "bold cyan", "0.0", "🔌"),
            MetricItem.Number("Wechselrichter max.", inverter.RatedPowerKilowatts, "kW", "bold cyan", "0.0", "🔋"),
            MetricItem.Text("WR-Status", snapshot.InverterStatus.ToString(), snapshot.InverterStatus == InverterStatus.Running ? "bold green3" : (snapshot.InverterStatus == InverterStatus.Faulted ? "bold red" : "grey74"), "⚙️")
        };

        // Section 2: Environment & Weather
        var weatherMetrics = new List<MetricItem>
        {
            MetricItem.Text("Bewölkung", $"{snapshot.Weather.CloudCoverFraction:P0} ({(snapshot.Weather.CloudCoverManuallyOverridden ? "manuell" : "auto")})", "bold white", "☁️"),
            MetricItem.Text("Außentemperatur", $"{snapshot.Weather.AmbientTemperatureCelsius:0.0} °C ({(snapshot.Weather.TemperatureManuallyOverridden ? "manuell" : "auto")})", "bold white", "🌡️"),
            MetricItem.Text("Niederschlag", $"{snapshot.Weather.Precipitation}", "bold white", "🌧️"),
            MetricItem.Text("Modulzustand", snapshot.SnowCoverActive ? "Verschneit" : "Schneefrei", snapshot.SnowCoverActive ? "bold cyan" : "bold green3", "❄️"),
            MetricItem.Text("Aktive Steuerung", selectedWeatherControl, "bold yellow", "🎮"),
            MetricItem.Text("Fortschritt", $"Schritt {snapshot.StepNumber} / {totalSteps} ({duration:hh\\:mm})", "bold white", "⏳")
        };

        dashboard.AddTwoColumns(
            ("Solarenergie & PV-Leistung", MetricGrid.Create(pvMetrics, 1)),
            ("Umwelt, Wetter & Atmosphäre", MetricGrid.Create(weatherMetrics, 1))
        );

        dashboard.WithKeyBindings(
            ("C", "Wolken"),
            ("T", "Temperatur"),
            ("+/-", "Wert/Tempo"),
            ("A", "Automatik"),
            ("R", "Regen"),
            ("S", "Schnee"),
            ("F", "Fehler"),
            ("P", "Pause")
        );

        dashboard.Render();
    }
}
