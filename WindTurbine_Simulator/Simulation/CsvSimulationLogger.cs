using System.Globalization;
using System.Text;

namespace WindTurbine_Simulator.Simulation;

public sealed class CsvSimulationLogger : IAsyncDisposable
{
    private readonly StreamWriter _writer;

    public CsvSimulationLogger(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        FilePath = filePath;

        var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _writer.WriteLine("timestamp;step;wind_mps;active_power_kw;cumulative_energy_kwh");
        _writer.Flush();
    }

    public string FilePath { get; }

    public async Task WriteAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{snapshot.SimulatedAt:O};{snapshot.Step};{snapshot.WindSpeedMps:F3};{snapshot.ActivePowerKw:F3};{snapshot.CumulativeEnergyKwh:F3}");

        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _writer.Dispose();
        return ValueTask.CompletedTask;
    }
}
