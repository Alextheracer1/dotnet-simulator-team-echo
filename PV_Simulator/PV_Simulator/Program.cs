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
    var masterHost = Environment.GetEnvironmentVariable("SIMULATION_MASTER_HOST") ?? "127.0.0.1";
    var masterPortText = Environment.GetEnvironmentVariable("SIMULATION_MASTER_PORT");
    var masterPort = string.IsNullOrWhiteSpace(masterPortText)
        ? 1510
        : int.TryParse(masterPortText, out var parsedPort) && parsedPort is >= 1 and <= 65535
            ? parsedPort
            : throw new InvalidOperationException("SIMULATION_MASTER_PORT must contain a TCP port from 1 through 65535.");
    using var cancellationTokenSource = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;
    await using var simulationMaster = new TeamShared.SimulationMasterClient(
        masterHost,
        masterPort,
        $"{Environment.MachineName}-{array.DeviceId}");
    try
    {
        await runner.RunAsync(engine, location, array, inverter, settings, csvLogger, cancellationTokenSource.Token, simulationMaster);
    }
    finally
    {
        cancellationTokenSource.Cancel();
        Console.CancelKeyPress -= cancelHandler;
    }
    AnsiConsole.MarkupLine("[bold green]Simulation abgeschlossen.[/]");
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[yellow]Simulation abgebrochen.[/]");
}
catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
{
    AnsiConsole.MarkupLine($"[bold red]Simulation konnte nicht gestartet werden:[/] {Markup.Escape(exception.Message)}");
}
