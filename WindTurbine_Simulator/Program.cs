using System.Text;
using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.ConsoleApp;
using WindTurbine_Simulator.Domain;
using WindTurbine_Simulator.Modbus; // WICHTIG: Namespace für Modbus hinzugefügt
using WindTurbine_Simulator.Simulation;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var ui = new ConsoleUi();
SimulationOptions options = ui.ReadOptions();

var model = new WindTurbineModel(
    options.AirDensityKgPerM3,
    options.RotorAreaM2,
    options.PowerCoefficient,
    options.RatedPowerKw);

// NEU: Modbus-Server instanziieren und starten
// Nur Port und Unit-ID übergeben
using var modbusServer = new WindTurbineModbusService(options.ModbusPort, options.ModbusUnitId);
modbusServer.Start();
Console.WriteLine($"[Modbus] Server lauscht auf Port {options.ModbusPort} (Unit ID: {options.ModbusUnitId})");

// ANGEPASST: Modbus-Server wird nun an den Runner übergeben
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
    Console.WriteLine("Simulation wurde abgebrochen.");
}
finally // NEU: Stellt sicher, dass der Netzwerkport freigegeben wird
{
    modbusServer.Stop();
}