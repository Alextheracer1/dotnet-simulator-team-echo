using System.Globalization;
using System.Text;
using PV_Simulator.Domain;

namespace PV_Simulator.Simulation;

public sealed class CsvSimulationLogger : IAsyncDisposable
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private readonly StreamWriter _writer;
    private readonly string _deviceId;
    private readonly double _moduleNominalPowerKilowattsPeak;
    private readonly double _inverterRatedPowerKilowatts;

    public CsvSimulationLogger(string filePath, PvArray array, Inverter inverter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(inverter);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FilePath = Path.GetFullPath(filePath);
        _writer = new StreamWriter(FilePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _deviceId = array.DeviceId;
        _moduleNominalPowerKilowattsPeak = array.ModuleNominalPowerKilowattsPeak;
        _inverterRatedPowerKilowatts = inverter.RatedPowerKilowatts;
        _writer.WriteLine("Step;DeviceId;Date;Time;Location;CloudCover_pct;AmbientTemperature_C;Precipitation;Irradiance_W_m2;Power_kW;PVModuleNominalPower_kWp;InverterMaximumOutput_kW;Power_pct;DailyEnergy_kWh;SnowCoverActive;InverterStatus;ManualFaultActive;CloudCoverManuallyOverridden;TemperatureManuallyOverridden;ManualRainActive");
        _writer.Flush();
    }

    public string FilePath { get; }

    public async Task WriteAsync(
        SimulationSnapshot snapshot,
        string locationName,
        CancellationToken cancellationToken = default)
    {
        var powerPercent = snapshot.PowerKilowatts / _moduleNominalPowerKilowattsPeak * 100;
        var fields = new[]
        {
            snapshot.StepNumber.ToString(GermanCulture),
            Escape(_deviceId),
            snapshot.SimulatedAt.ToString("dd.MM.yyyy", GermanCulture),
            snapshot.SimulatedAt.ToString("HH:mm:ss", GermanCulture),
            Escape(locationName),
            (snapshot.Weather.CloudCoverFraction * 100).ToString("F2", GermanCulture),
            snapshot.Weather.AmbientTemperatureCelsius.ToString("F2", GermanCulture),
            snapshot.Weather.Precipitation.ToString(),
            snapshot.IrradianceWattsPerSquareMeter.ToString("F2", GermanCulture),
            snapshot.PowerKilowatts.ToString("F4", GermanCulture),
            _moduleNominalPowerKilowattsPeak.ToString("F4", GermanCulture),
            _inverterRatedPowerKilowatts.ToString("F4", GermanCulture),
            powerPercent.ToString("F2", GermanCulture),
            snapshot.DailyEnergyKilowattHours.ToString("F4", GermanCulture),
            snapshot.SnowCoverActive.ToString(),
            snapshot.InverterStatus.ToString(),
            snapshot.ManualFaultActive.ToString(),
            snapshot.Weather.CloudCoverManuallyOverridden.ToString(),
            snapshot.Weather.TemperatureManuallyOverridden.ToString(),
            snapshot.Weather.ManualRainActive.ToString()
        };

        await _writer.WriteLineAsync(string.Join(';', fields).AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"") }\"";
}