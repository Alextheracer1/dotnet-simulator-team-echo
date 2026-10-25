using PV_Simulator.Calculations;
using PV_Simulator.Domain;
using PV_Simulator.Weather;

namespace PV_Simulator.Simulation;

public sealed class SimulationEngine
{
    private readonly Location _location;
    private readonly PvArray _array;
    private readonly Inverter _inverter;
    private readonly SimulationSettings _settings;
    private readonly WeatherEngine _weatherEngine;
    private readonly SolarPositionCalculator _solarPositionCalculator;
    private readonly PvPowerCalculator _powerCalculator;
    private readonly Random _random;
    private DateOnly? _energyDate;
    private double _dailyEnergyKilowattHours;
    private bool _snowCoverActive;
    private TimeSpan _faultRemaining;
    private InverterStatus _inverterStatus = InverterStatus.Off;
    private bool _manualFaultActive;
    private int _stepNumber;

    public SimulationEngine(
        Location location,
        PvArray array,
        Inverter inverter,
        SimulationSettings settings,
        WeatherSettings weatherSettings,
        Random? random = null)
    {
        _location = location;
        _array = array;
        _inverter = inverter;
        _settings = settings;
        _random = random ?? (weatherSettings.RandomSeed is int seed ? new Random(seed) : Random.Shared);
        _weatherEngine = new WeatherEngine(location, _random, settings.StartTime);
        _solarPositionCalculator = new SolarPositionCalculator();
        _powerCalculator = new PvPowerCalculator();
    }

    public bool ToggleManualFault()
    {
        _manualFaultActive = !_manualFaultActive;
        if (_manualFaultActive)
        {
            _inverterStatus = InverterStatus.Faulted;
        }
        else
        {
            _faultRemaining = TimeSpan.Zero;
            _inverterStatus = InverterStatus.Off;
        }

        return _manualFaultActive;
    }

    public bool ToggleSnowCover()
    {
        _snowCoverActive = !_snowCoverActive;
        return _snowCoverActive;
    }

    public double AdjustCloudCover(int direction) => _weatherEngine.AdjustCloudCover(direction);

    public double AdjustTemperature(int direction) => _weatherEngine.AdjustTemperature(direction);

    public void ResetCloudCoverOverride() => _weatherEngine.ResetCloudCoverOverride();

    public void ResetTemperatureOverride() => _weatherEngine.ResetTemperatureOverride();

    public bool ToggleManualRain() => _weatherEngine.ToggleManualRain();

    public SimulationSnapshot Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero || elapsed >= _settings.Duration)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        var timeStep = Min(_settings.TimeStep, _settings.Duration - elapsed);
        var simulatedAt = _settings.StartTime + elapsed + timeStep;
        var date = DateOnly.FromDateTime(simulatedAt.DateTime);
        if (_energyDate != date)
        {
            _energyDate = date;
            _dailyEnergyKilowattHours = 0;
        }

        _stepNumber++;
        var weather = _weatherEngine.Advance(simulatedAt, timeStep);
        var solarPosition = _solarPositionCalculator.Calculate(_location, simulatedAt);
        var irradiance = _powerCalculator.CalculatePlaneOfArrayIrradiance(solarPosition, _array, weather);
        UpdateInverterStatus(timeStep, irradiance);
        var power = _powerCalculator.CalculatePowerKilowatts(
            _array,
            _inverter,
            weather,
            irradiance,
            _snowCoverActive,
            _inverterStatus);
        _dailyEnergyKilowattHours += power * timeStep.TotalHours;

        return new SimulationSnapshot(
            _stepNumber,
            simulatedAt,
            weather,
            irradiance,
            power,
            _dailyEnergyKilowattHours,
            _snowCoverActive,
            _inverterStatus,
            _manualFaultActive);
    }

    private void UpdateInverterStatus(TimeSpan timeStep, double irradiance)
    {
        if (_manualFaultActive)
        {
            _inverterStatus = InverterStatus.Faulted;
            return;
        }

        if (_faultRemaining > TimeSpan.Zero)
        {
            _faultRemaining -= timeStep;
            if (_faultRemaining <= TimeSpan.Zero)
            {
                _inverterStatus = InverterStatus.Off;
            }
        }
        else if (_inverterStatus != InverterStatus.Faulted)
        {
            var failureProbability = 1 - Math.Pow(
                1 - _inverter.FailureProbabilityPerHour,
                timeStep.TotalHours);
            if (_random.NextDouble() < failureProbability)
            {
                _faultRemaining = _inverter.RepairDuration;
                _inverterStatus = InverterStatus.Faulted;
            }
        }

        if (_inverterStatus != InverterStatus.Faulted)
        {
            _inverterStatus = irradiance > 1 ? InverterStatus.Running : InverterStatus.Off;
        }
    }

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first <= second ? first : second;
}