using System.Globalization;
using Spectre.Console;
using TeamConsoleShared;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Publisher;
using TeamShared.Weather;
using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.ConsoleApp;
using WindTurbine_Simulator.Domain;
using WindTurbine_Simulator.Modbus;
using WindTurbine_Simulator.Simulation;

SimulationTheme.InitializeConsole();

var ui = new ConsoleUi();
SimulationOptions options = ui.ReadOptions();

var model = new WindTurbineModel(
    options.AirDensityKgPerM3,
    options.RotorAreaM2,
    options.PowerCoefficient,
    options.RatedPowerKw);

using var modbusServer = new WindTurbineModbusService(options.ModbusPort, options.ModbusUnitId);
modbusServer.Start();
AnsiConsole.MarkupLine($"[grey62][[Modbus]][/] Server lauscht auf Port [bold cyan]{options.ModbusPort}[/] (Unit ID: [bold cyan]{options.ModbusUnitId}[/])");

var rabbitConfig = RabbitMqConfiguration.CreateDefault();
await using var rabbitPublisher = new RabbitMqPublisher(rabbitConfig);
await using var rabbitConsumer = new RabbitMqConsumer(rabbitConfig);
AnsiConsole.MarkupLine($"[grey62][[RabbitMQ]][/] Initialisiere Broker-Anbindung auf [bold cyan]{rabbitConfig.HostName}:{rabbitConfig.Port}[/]...");

var runner = new WindTurbineSimulationRunner(model, options, modbusServer, rabbitPublisher, rabbitConsumer);
var statusRenderer = new ConsoleStatusRenderer();
using var cancellationTokenSource = new CancellationTokenSource();

var csvPath = Path.Combine(
    Environment.CurrentDirectory,
    "exports",
    $"windturbine-simulation-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");

await using var csvLogger = new CsvSimulationLogger(csvPath);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

var masterHost = Environment.GetEnvironmentVariable("SIMULATION_MASTER_HOST") ?? "127.0.0.1";
await using var masterClient = new WindSimulationMasterClient(
    masterHost,
    GetSimulationMasterPort(),
    $"{Environment.MachineName}-WindTurbine");

ui.PrintStart(options, csvLogger.FilePath);

var weatherUrl = Environment.GetEnvironmentVariable("WEATHER_SIMULATOR_URL") ?? "http://localhost:5005";
var latitude = double.TryParse(Environment.GetEnvironmentVariable("WIND_LATITUDE"), CultureInfo.InvariantCulture, out var parsedLat) ? parsedLat : options.Latitude;
var longitude = double.TryParse(Environment.GetEnvironmentVariable("WIND_LONGITUDE"), CultureInfo.InvariantCulture, out var parsedLon) ? parsedLon : options.Longitude;

await using var weatherReceiver = await WeatherCallbackReceiver.StartAsync(6100, "wind", cancellationTokenSource.Token);
AnsiConsole.MarkupLine($"[grey62][[Wetter]][/] Callback-Empfänger lauscht auf Port [bold cyan]{weatherReceiver.BoundPort}[/]");
weatherReceiver.StartAutoRegistration(weatherUrl, $"{Environment.MachineName}-WindTurbine", "wind", latitude, longitude);

int? totalSteps = options.Duration.HasValue
    ? (int)Math.Ceiling(options.Duration.Value.TotalSeconds / options.StepInterval.TotalSeconds)
    : null;

try
{
    await masterClient.ConnectWithRetryAsync(cancellationTokenSource.Token);
    await foreach (var snapshot in runner.RunAsync(masterClient, weatherReceiver, cancellationTokenSource.Token))
    {
        statusRenderer.Render(snapshot, options, snapshot.SpeedMultiplier, totalSteps, runner.RabbitMqConnected, runner.RabbitMqPublishedCount);
        await csvLogger.WriteAsync(snapshot, cancellationTokenSource.Token);
    }

    ui.PrintCompleted();
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[bold yellow]Simulation wurde abgebrochen.[/]");
}
finally
{
    modbusServer.Stop();
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
