using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.Domain;
using WindTurbine_Simulator.Modbus;

namespace WindTurbine_Simulator.Simulation;

public sealed class WindTurbineSimulationRunner
{
    private readonly WindTurbineModel _model;
    private readonly SimulationOptions _options;
    private readonly WindTurbineModbusService _modbusService;

    public WindTurbineSimulationRunner(
        WindTurbineModel model,
        SimulationOptions options,
        WindTurbineModbusService modbusService)
    {
        _model = model;
        _options = options;
        _modbusService = modbusService;
    }

    public async IAsyncEnumerable<SimulationSnapshot> RunAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var simulatedAt = _options.StartTime;
        var elapsed = TimeSpan.Zero;
        var step = 0;
        var cumulativeEnergyKwh = 0.0;
        var speedMultiplier = _options.SpeedMultiplier;

        while (!_options.Duration.HasValue || elapsed < _options.Duration.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            speedMultiplier = await ReadRuntimeControlsAsync(speedMultiplier, cancellationToken);

            step++;
            var windSpeedMps = GenerateSimulatedWindSpeedMps(simulatedAt);
            var rawPowerKw = await CalculateActivePowerKwAsync(windSpeedMps, cancellationToken);

            // Sollwert (Leistungsbegrenzung) via Modbus berücksichtigen
            var powerLimitKw = _modbusService.ReadPowerLimitSetpoint();
            var activePowerKw = Math.Min(rawPowerKw, powerLimitKw);

            cumulativeEnergyKwh += activePowerKw * _options.StepInterval.TotalHours;

            // Plausible gerätespezifische Werte errechnen
            var rotorRpm = windSpeedMps * 1.5;
            var pitchAngle = activePowerKw >= _model.RatedPowerKw ? 12.0 : 0.0;

            var snapshot = new SimulationSnapshot(step, simulatedAt, windSpeedMps, activePowerKw, cumulativeEnergyKwh, speedMultiplier);

            // Modbus-Server synchron auf den neuesten Stand bringen
            _modbusService.Update(snapshot, rotorRpm, pitchAngle);

            yield return snapshot;

            simulatedAt = simulatedAt.Add(_options.StepInterval);
            elapsed += _options.StepInterval;

            var delayMilliseconds = Math.Max(100, _options.StepInterval.TotalMilliseconds / speedMultiplier);
            await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
        }
    }

    private static async Task<double> ReadRuntimeControlsAsync(double speedMultiplier, CancellationToken cancellationToken)
    {
        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.P)
            {
                Console.WriteLine("Pausiert — P zum Fortsetzen; Modbus bleibt erreichbar.");
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Console.KeyAvailable)
                    {
                        var pauseKey = Console.ReadKey(intercept: true).Key;
                        if (pauseKey == ConsoleKey.P) break;
                        speedMultiplier = HandleRuntimeKey(pauseKey, speedMultiplier);
                    }
                    // Nicht-blockierendes Warten, damit der Thread frei bleibt
                    await Task.Delay(100, cancellationToken);
                }
            }
            else
            {
                speedMultiplier = HandleRuntimeKey(key, speedMultiplier);
            }
        }
        return speedMultiplier;
    }

    private static double HandleRuntimeKey(ConsoleKey key, double speedMultiplier)
    {
        return key switch
        {
            ConsoleKey.Add or ConsoleKey.OemPlus => Math.Min(speedMultiplier * 2, 1_000_000),
            ConsoleKey.Subtract or ConsoleKey.OemMinus => Math.Max(speedMultiplier / 2, 0.01),
            _ => speedMultiplier
        };
    }

    private double GenerateSimulatedWindSpeedMps(DateTimeOffset simulatedAt)
    {
        var dayFactor = Math.Sin(((simulatedAt.TimeOfDay.TotalHours - 6.0) / 24.0) * 2.0 * Math.PI);
        var normalizedProfile = (dayFactor + 1.0) / 2.0;
        var span = _options.MaximumWindSpeedMps - _options.MinimumWindSpeedMps;
        var baseline = _options.MinimumWindSpeedMps + (span * normalizedProfile);
        var noise = (Random.Shared.NextDouble() * 2.0 - 1.0) * (span * 0.15);
        return Math.Clamp(baseline + noise, _options.MinimumWindSpeedMps, _options.MaximumWindSpeedMps);
    }

    private async Task<double> CalculateActivePowerKwAsync(double windSpeedMps, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return _model.CalculateActivePowerKw(windSpeedMps);
    }

    
}


public readonly record struct SimulationSnapshot(
    int Step,
    DateTimeOffset SimulatedAt,
    double WindSpeedMps,
    double ActivePowerKw,
    double CumulativeEnergyKwh,
    double SpeedMultiplier);