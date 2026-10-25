using System.Text;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;
using PV_Simulator.Simulation;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var input = new ConsoleInput();

try
{
    Console.WriteLine("Konfiguration des PV-Digitalzwillings");
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
    Console.WriteLine($"CSV-Protokoll: {csvLogger.FilePath}");
    await runner.RunAsync(engine, location, array, inverter, settings, csvLogger);
    Console.WriteLine("Simulation abgeschlossen.");
}
catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine($"Simulation konnte nicht gestartet werden: {exception.Message}");
}
