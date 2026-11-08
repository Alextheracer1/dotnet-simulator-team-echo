using System;
using System.IO;
using System.Linq;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;
using PV_Simulator.Modbus;
using PV_Simulator.Simulation;
using Spectre.Console;
using TeamConsoleShared;
using TeamShared.Weather;

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
    var masterHost = Environment.GetEnvironmentVariable("SIMULATION_MASTER_HOST") ?? "127.0.0.1";
    var masterPort = GetSimulationMasterPort();
    await using var masterClient = new PvSimulationMasterClient(
        masterHost,
        masterPort,
        $"{Environment.MachineName}-PV-{array.DeviceId}");
    await masterClient.ConnectWithRetryAsync();
    var masterEnvironment = await masterClient.RequestEnvironmentAsync()
        ?? throw new IOException("Der Simulation Master hat keine Startzeit geliefert.");
    settings = new SimulationSettings(
        new DateTimeOffset(DateTime.SpecifyKind(masterEnvironment.VirtualTime, DateTimeKind.Utc)),
        settings.TimeStep,
        settings.SpeedMultiplier);
    using var deviceIdLease = new DeviceIdLease(array.DeviceId);
    var engine = new SimulationEngine(location, array, inverter, settings, weatherSettings);
    var renderer = new ConsoleStatusRenderer();
    var runner = new SimulationRunner(renderer);
    var weatherUrl = Environment.GetEnvironmentVariable("WEATHER_SIMULATOR_URL") ?? "http://localhost:5005";
    await using var weatherReceiver = await WeatherCallbackReceiver.StartAsync(6000, "pv");
    AnsiConsole.MarkupLine($"[grey62][Wetter][/] Callback-Empfänger lauscht auf Port [bold cyan]{weatherReceiver.BoundPort}[/]");
    weatherReceiver.StartAutoRegistration(weatherUrl, array.DeviceId, "pv", location.Latitude, location.Longitude);

    var modbusPort = GetModbusPort();
    using var modbusServer = new PvModbusService(modbusPort, 1);
    modbusServer.Start();
    AnsiConsole.MarkupLine($"[grey62][Modbus][/] Server lauscht auf Port [bold cyan]{modbusPort}[/] (Unit ID: [bold cyan]1[/])");

    var safeDeviceId = string.Concat(array.DeviceId.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    var csvPath = Path.Combine(
        Environment.CurrentDirectory,
        "exports",
        $"pv-simulation-{safeDeviceId}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
    await using var csvLogger = new CsvSimulationLogger(csvPath, array, inverter);
    AnsiConsole.MarkupLine($"[grey62]CSV-Protokoll:[/] [cyan]{Markup.Escape(csvLogger.FilePath)}[/]");
    await runner.RunAsync(engine, location, array, inverter, settings, csvLogger, masterClient, weatherReceiver, modbusServer);
    AnsiConsole.MarkupLine("[bold green]Simulation abgeschlossen.[/]");
}
catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
{
    AnsiConsole.MarkupLine($"[bold red]Simulation konnte nicht gestartet werden:[/] {Markup.Escape(exception.Message)}");
}

static int GetModbusPort()
{
    var value = Environment.GetEnvironmentVariable("PV_MODBUS_PORT");
    if (string.IsNullOrWhiteSpace(value))
        return 1504;

    if (int.TryParse(value, out var port) && port is >= 1024 and <= 65535)
        return port;

    throw new InvalidOperationException("PV_MODBUS_PORT muss einen TCP-Port von 1024 bis 65535 enthalten.");
}

static int GetSimulationMasterPort()
{
    var value = Environment.GetEnvironmentVariable("SIMULATION_MASTER_PORT");
    if (string.IsNullOrWhiteSpace(value))
        return 1510;

    if (int.TryParse(value, out var port) && port is >= 1 and <= 65535)
        return port;

    throw new InvalidOperationException("SIMULATION_MASTER_PORT muss einen TCP-Port von 1 bis 65535 enthalten.");
}
