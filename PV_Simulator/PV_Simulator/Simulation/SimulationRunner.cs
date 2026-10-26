using System;
using System.Threading;
using System.Threading.Tasks;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;

namespace PV_Simulator.Simulation;

public sealed class SimulationRunner(ConsoleStatusRenderer renderer)
{
    private WeatherControlSelection _selectedWeatherControl;

    public async Task RunAsync(
        SimulationEngine engine,
        Location location,
        PvArray array,
        Inverter inverter,
        SimulationSettings settings,
        CsvSimulationLogger csvLogger,
        CancellationToken cancellationToken = default)
    {
        var elapsed = TimeSpan.Zero;
        var speedMultiplier = settings.SpeedMultiplier;
        var totalSteps = (int)Math.Ceiling(settings.Duration.TotalSeconds / settings.TimeStep.TotalSeconds);

        while (elapsed < settings.Duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            speedMultiplier = ReadRuntimeControls(engine, speedMultiplier, cancellationToken);
            var timeStep = settings.TimeStep <= settings.Duration - elapsed
                ? settings.TimeStep
                : settings.Duration - elapsed;
            var snapshot = engine.Advance(elapsed);
            elapsed += timeStep;
            renderer.Render(
                snapshot,
                location,
                array,
                inverter,
                settings,
                settings.Duration,
                speedMultiplier,
                totalSteps,
                GetSelectedWeatherControlName());
            await csvLogger.WriteAsync(snapshot, location.Name, cancellationToken);

            var delayMilliseconds = Math.Max(100, timeStep.TotalMilliseconds / speedMultiplier);
            await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
        }
    }

    private double ReadRuntimeControls(
        SimulationEngine engine,
        double speedMultiplier,
        CancellationToken cancellationToken)
    {
        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.P)
            {
                Console.WriteLine("Pausiert — P zum Fortsetzen; Wettersteuerung und F bleiben verfügbar.");
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pauseKey = Console.ReadKey(intercept: true).Key;
                    if (pauseKey == ConsoleKey.P)
                    {
                        break;
                    }

                    speedMultiplier = HandleRuntimeKey(pauseKey, engine, speedMultiplier);
                }
            }
            else
            {
                speedMultiplier = HandleRuntimeKey(key, engine, speedMultiplier);
            }
        }

        return speedMultiplier;
    }

    private double HandleRuntimeKey(ConsoleKey key, SimulationEngine engine, double speedMultiplier)
    {
        switch (key)
        {
            case ConsoleKey.C:
                _selectedWeatherControl = WeatherControlSelection.CloudCover;
                Console.WriteLine("Wolkenbedeckung ausgewählt; +/- ändert sie in 10-Prozentpunkt-Schritten.");
                break;
            case ConsoleKey.T:
                _selectedWeatherControl = WeatherControlSelection.Temperature;
                Console.WriteLine("Temperatur ausgewählt; +/- ändert sie in 2-°C-Schritten.");
                break;
            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    Console.WriteLine($"Wolkenbedeckung: {engine.AdjustCloudCover(1):P0}");
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(1):0.0} °C");
                }
                else
                {
                    speedMultiplier = Math.Min(speedMultiplier * 2, 1_000_000);
                }

                break;
            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    Console.WriteLine($"Wolkenbedeckung: {engine.AdjustCloudCover(-1):P0}");
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(-1):0.0} °C");
                }
                else
                {
                    speedMultiplier = Math.Max(speedMultiplier / 2, 0.01);
                }

                break;
            case ConsoleKey.A:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    engine.ResetCloudCoverOverride();
                    Console.WriteLine("Wolkenbedeckung folgt wieder der Automatik.");
                    _selectedWeatherControl = WeatherControlSelection.None;
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    engine.ResetTemperatureOverride();
                    Console.WriteLine("Temperatur folgt wieder der Automatik.");
                    _selectedWeatherControl = WeatherControlSelection.None;
                }

                break;
            case ConsoleKey.R:
                Console.WriteLine($"Manueller Regen {(engine.ToggleManualRain() ? "aktiviert" : "deaktiviert")}.");
                break;
            case ConsoleKey.F:
                Console.WriteLine($"Manuelle Wechselrichterstörung {(engine.ToggleManualFault() ? "aktiviert" : "deaktiviert")}.");
                break;
            case ConsoleKey.S:
                Console.WriteLine($"PV-Module {(engine.ToggleSnowCover() ? "verschneit" : "schneefrei")}.");
                break;
        }

        return speedMultiplier;
    }

    private string GetSelectedWeatherControlName() => _selectedWeatherControl switch
    {
        WeatherControlSelection.CloudCover => "Wolken",
        WeatherControlSelection.Temperature => "Temperatur",
        _ => "keine ( +/− regeln die Geschwindigkeit)"
    };

    private enum WeatherControlSelection
    {
        None,
        CloudCover,
        Temperature
    }
}