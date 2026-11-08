using System;
using System.Globalization;
using PV_Simulator.Domain;
using Spectre.Console;
using TeamConsoleShared;

namespace PV_Simulator.ConsoleApp;

public sealed class ConsoleInput
{
    public SimulationConfiguration ReadConfiguration()
    {
        AnsiConsole.MarkupLine("[bold cyan]Startprofil wählen:[/] 1 = Standardwerte, 2 = Parameter manuell eingeben [grey](Enter = 1)[/]");
        SimulationConfiguration configuration;
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Auswahl: [/]");
            var choice = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(choice) || choice == "1")
            {
                var pvModulePower = ReadRequiredDouble("PV-Modul-Nennleistung in kWp", 0.01, 1_000_000);
                var inverterPower = ReadRequiredDouble("Maximale Wechselrichter-Ausgangsleistung in kW", 0.01, 1_000_000);
                configuration = SimulationDefaults.Create(pvModulePower, inverterPower);
                break;
            }

            if (choice == "2")
            {
                configuration = ReadManualConfiguration();
                break;
            }

            AnsiConsole.MarkupLine("[yellow]Bitte 1 oder 2 auswählen.[/]");
        }

        var settings = new SimulationSettings(
            DateTimeOffset.UtcNow,
            configuration.Settings.TimeStep,
            configuration.Settings.SpeedMultiplier);
        var weatherSettings = ReadWeatherSettings();
        configuration = new SimulationConfiguration(
            configuration.Location,
            configuration.Array,
            configuration.Inverter,
            settings,
            weatherSettings);

        DisplayConfiguration(configuration);
        return configuration;
    }

    private static SimulationConfiguration ReadManualConfiguration()
    {
        var defaultLocation = SimulationDefaults.CreateLocation();
        var defaultSettings = SimulationDefaults.CreateSettings();
        var name = ReadText("Standortname", defaultLocation.Name);
        var latitude = ReadDouble("Breitengrad (-90 bis 90)", -90, 90, defaultLocation.Latitude);
        var longitude = ReadDouble("Längengrad (-180 bis 180)", -180, 180, defaultLocation.Longitude);
        var elevation = ReadDouble("Höhe über Meer in Metern", -500, 9000, defaultLocation.ElevationMeters);
        var deviceId = ReadRequiredText("Eindeutige Geräte-ID der PV-Anlage");
        var peakPower = ReadRequiredDouble("PV-Modul-Nennleistung in kWp", 0.01, 1_000_000);
        var tilt = ReadDouble("Modulneigung in Grad (0 bis 90)", 0, 90, SimulationDefaults.PvArrayTiltDegrees);
        var azimuth = ReadDouble("Modul-Azimut in Grad (0 = Norden, 90 = Osten, 180 = Süden)", 0, 359.999, SimulationDefaults.PvArrayAzimuthDegrees);
        var inverterPower = ReadRequiredDouble("Maximale Wechselrichter-Ausgangsleistung in kW", 0.01, 1_000_000);
        var failureProbability = ReadDouble("Wechselrichter-Ausfallwahrscheinlichkeit in % pro Stunde", 0, 100, SimulationDefaults.InverterFailureProbabilityPerHour * 100) / 100;
        var repairMinutes = ReadDouble("Mittlere Reparaturdauer in Minuten", double.Epsilon, 1_000_000, SimulationDefaults.InverterRepairDuration.TotalMinutes);
        return new SimulationConfiguration(
            new Location(name, latitude, longitude, elevation),
            new PvArray(deviceId, peakPower, tilt, azimuth),
            new Inverter(inverterPower, failureProbability, TimeSpan.FromMinutes(repairMinutes)),
            new SimulationSettings(DateTimeOffset.UtcNow, defaultSettings.TimeStep, defaultSettings.SpeedMultiplier),
            new WeatherSettings(null));
    }

    private static WeatherSettings ReadWeatherSettings()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Wetter-Zufallsseed [grey][[Enter = zufällig]][/]: [/]");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return new WeatherSettings(null);
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out var seed))
            {
                return new WeatherSettings(seed);
            }

            AnsiConsole.MarkupLine("[yellow]Bitte eine ganze Zahl eingeben oder Enter drücken.[/]");
        }
    }

    private static string ReadRequiredText(string prompt)
    {
        while (true)
        {
            AnsiConsole.Markup($"[bold grey85]{Markup.Escape(prompt)}: [/]");
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            AnsiConsole.MarkupLine("[yellow]Dieses Feld darf nicht leer sein.[/]");
        }
    }

    private static string ReadText(string prompt, string defaultValue)
    {
        AnsiConsole.Markup($"[bold grey85]{Markup.Escape(prompt)} [grey][[{Markup.Escape(defaultValue)}]][/]: [/]");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static double ReadRequiredDouble(string prompt, double minimum, double maximum)
    {
        while (true)
        {
            AnsiConsole.Markup($"[bold grey85]{Markup.Escape(prompt)}: [/]");
            var input = Console.ReadLine();
            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
                && double.IsFinite(value)
                && value >= minimum
                && value <= maximum)
            {
                return value;
            }

            AnsiConsole.MarkupLine($"[yellow]Bitte eine gültige Zahl zwischen {minimum} und {maximum} eingeben.[/]");
        }
    }

    private static double ReadDouble(string prompt, double minimum, double maximum, double defaultValue)
    {
        while (true)
        {
            AnsiConsole.Markup($"[bold grey85]{Markup.Escape(prompt)} [grey][[{defaultValue}]][/]: [/]");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
                && double.IsFinite(value)
                && value >= minimum
                && value <= maximum)
            {
                return value;
            }

            AnsiConsole.MarkupLine($"[yellow]Bitte eine Zahl zwischen {minimum} und {maximum} eingeben.[/]");
        }
    }

    private static void DisplayConfiguration(SimulationConfiguration configuration)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(foreground: SimulationTheme.PvAccent))
            .Expand();

        table.Title("[bold gold1]Finale Simulationskonfiguration[/]");
        table.AddColumn(new TableColumn("[bold grey85]Parameter[/]"));
        table.AddColumn(new TableColumn("[bold grey85]Wert[/]"));

        table.AddRow("Standort", $"{configuration.Location.Name} ({configuration.Location.Latitude:F2}°, {configuration.Location.Longitude:F2}°, {configuration.Location.ElevationMeters} m)");
        table.AddRow("PV-Anlage", $"{configuration.Array.DeviceId} | {configuration.Array.ModuleNominalPowerKilowattsPeak:F2} kWp (Neigung: {configuration.Array.TiltDegrees}°, Azimut: {configuration.Array.AzimuthDegrees}°)");
        table.AddRow("Wechselrichter", $"{configuration.Inverter.RatedPowerKilowatts:F2} kW | Ausfallrate: {configuration.Inverter.FailureProbabilityPerHour:P0}/h");
        table.AddRow("Startzeit", "Wird vom Simulation Master übernommen");
        table.AddRow("Taktung", "Wird vom Simulation Master gesteuert");
        table.AddRow("Wetter", $"Automatisch | Seed: {configuration.WeatherSettings.RandomSeed?.ToString(CultureInfo.CurrentCulture) ?? "zufällig"}");

        AnsiConsole.Write(table);
    }
}
