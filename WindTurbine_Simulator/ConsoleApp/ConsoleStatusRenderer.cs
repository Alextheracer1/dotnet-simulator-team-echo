using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.Simulation;
using TeamConsoleShared;

namespace WindTurbine_Simulator.ConsoleApp;

public sealed class ConsoleStatusRenderer
{
    private readonly ConsoleStatusBox _box = new(84);

    public void Render(
        SimulationSnapshot snapshot,
        SimulationOptions options,
        double speedMultiplier,
        int? totalSteps)
    {
        Console.Clear();

        _box.WriteBorder();
        // NEU: Port und Unit-ID werden direkt im Titel angezeigt
        _box.WriteLine($"WINDTURBINEN-SIMULATION  |  Port: {options.ModbusPort}  |  Unit-ID: {options.ModbusUnitId}");
        _box.WriteBorder();
        _box.WriteLine(totalSteps.HasValue
            ? $"Zeit: {snapshot.SimulatedAt:dd.MM.yyyy HH:mm zzz}    Schritt: {snapshot.Step}/{totalSteps.Value}"
            : $"Zeit: {snapshot.SimulatedAt:dd.MM.yyyy HH:mm zzz}    Schritt: {snapshot.Step}");
        _box.WriteLine(options.Duration.HasValue
            ? $"Laufzeit: {options.Duration:hh\\:mm}    Schrittweite: {options.StepInterval.TotalSeconds:0.##} s"
            : $"Laufzeit: unbegrenzt    Schrittweite: {options.StepInterval.TotalSeconds:0.##} s");
        _box.WriteBorder();
        _box.WriteLine("LEISTUNG");
        _box.WriteLine(PowerBarFormatter.CreatePowerBar("Turbinenleistung", snapshot.ActivePowerKw, options.RatedPowerKw, "kW"));
        _box.WriteLine($"Windgeschwindigkeit: {snapshot.WindSpeedMps:0.00} m/s    Tagesenergie: {snapshot.CumulativeEnergyKwh:0.000} kWh");
        _box.WriteBorder();
        _box.WriteLine("ZUSTAND");
        _box.WriteLine($"Zeitraffer: {speedMultiplier:0.##}x");
        _box.WriteBorder();
        _box.WriteLine("TASTEN");
        _box.WriteLine("+ schneller | - langsamer | P Pause/Fortsetzen | Ctrl+C Abbruch");
        _box.WriteBorder();
    }
}