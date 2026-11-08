using System;
using System.IO;
using System.Linq;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;
using PV_Simulator.Simulation;
using Spectre.Console;
using TeamConsoleShared;

SimulationTheme.InitializeConsole();

var input = new ConsoleInput();

try
{
    AnsiConsole.Write(
        new Panel(new Markup("[bold gold1]☀️ PV-Digitalzwilling & Solarsimulator[/]\n[grey62]Digitaler Zwilling zur Erzeugung von Solarstrom[/]"))
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: SimulationTheme.PvAccent))
            .Expand());

    var configuration = input.ReadConfiguration();
    var location = configuration.Location;
    var array = configuration.Array;
    var inverter = configuration.Inverter;
    var settings = configuration.Settings;
    var weatherSettings = configuration.WeatherSettings;
    using var deviceIdLease = new DeviceIdLease(array.DeviceId);
    var engine = new SimulationEngine(location, array, inverter, settings, weatherSettings);
    var renderer = new ConsoleStatusRenderer();
    var runner = new SimulationRunner(renderer);
    var safeDeviceId = string.Concat(array.DeviceId.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    var csvPath = Path.Combine(
        Environment.CurrentDirectory,
        "exports",
        $"pv-simulation-{safeDeviceId}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
    await using var csvLogger = new CsvSimulationLogger(csvPath, array, inverter);
    AnsiConsole.MarkupLine($"[grey62]CSV-Protokoll:[/] [cyan]{Markup.Escape(csvLogger.FilePath)}[/]");
    await runner.RunAsync(engine, location, array, inverter, settings, csvLogger);
    AnsiConsole.MarkupLine("[bold green]Simulation abgeschlossen.[/]");
}
catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
{
    AnsiConsole.MarkupLine($"[bold red]Simulation konnte nicht gestartet werden:[/] {Markup.Escape(exception.Message)}");
}
