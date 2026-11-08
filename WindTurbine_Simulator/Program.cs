using Spectre.Console;
using TeamConsoleShared;
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
AnsiConsole.MarkupLine($"[grey62][Modbus][/] Server lauscht auf Port [bold cyan]{options.ModbusPort}[/] (Unit ID: [bold cyan]{options.ModbusUnitId}[/])");

var runner = new WindTurbineSimulationRunner(model, options, modbusServer);
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

ui.PrintStart(options, csvLogger.FilePath);

int? totalSteps = options.Duration.HasValue
    ? (int)Math.Ceiling(options.Duration.Value.TotalSeconds / options.StepInterval.TotalSeconds)
    : null;

try
{
    await foreach (var snapshot in runner.RunAsync(cancellationTokenSource.Token))
    {
        statusRenderer.Render(snapshot, options, snapshot.SpeedMultiplier, totalSteps);
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
