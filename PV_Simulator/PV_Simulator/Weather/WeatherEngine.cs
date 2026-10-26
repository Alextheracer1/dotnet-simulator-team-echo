using System;
using PV_Simulator.Domain;

namespace PV_Simulator.Weather;

public sealed class WeatherEngine(Location location, Random random)
{
    private const double CloudCoverChangeStep = 0.1;
    private const double TemperatureChangeStep = 2;
    private const double MinimumTemperatureCelsius = -50;
    private const double MaximumTemperatureCelsius = 60;
    private double _cloudCoverFraction = 0.35;
    private double _cloudTargetFraction = 0.35;
    private TimeSpan _cloudTargetRemaining;
    private TimeSpan _automaticRainRemaining;
    private double _temperatureAnomaly;
    private double _automaticTemperatureCelsius;
    private double? _cloudCoverOverride;
    private double? _temperatureOverride;
    private bool _manualRainActive;

    public WeatherEngine(Location location, Random random, DateTimeOffset startTime) : this(location, random)
    {
        _automaticTemperatureCelsius = CalculateAutomaticTemperature(startTime, TimeSpan.Zero);
    }

    public WeatherConditions Advance(DateTimeOffset simulatedAt, TimeSpan timeStep)
    {
        if (timeStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStep));
        }

        UpdateCloudCover(timeStep);
        _automaticTemperatureCelsius = CalculateAutomaticTemperature(simulatedAt, timeStep);
        UpdateAutomaticRain(timeStep);

        return new WeatherConditions(
            _cloudCoverOverride ?? _cloudCoverFraction,
            _temperatureOverride ?? _automaticTemperatureCelsius,
            _manualRainActive || _automaticRainRemaining > TimeSpan.Zero ? Precipitation.Rain : Precipitation.None,
            _cloudCoverOverride.HasValue,
            _temperatureOverride.HasValue,
            _manualRainActive);
    }

    public double AdjustCloudCover(int direction)
    {
        ValidateDirection(direction);
        _cloudCoverOverride ??= _cloudCoverFraction;
        _cloudCoverOverride = Math.Clamp(_cloudCoverOverride.Value + direction * CloudCoverChangeStep, 0, 1);
        return _cloudCoverOverride.Value;
    }

    public double AdjustTemperature(int direction)
    {
        ValidateDirection(direction);
        _temperatureOverride ??= _automaticTemperatureCelsius;
        _temperatureOverride = Math.Clamp(
            _temperatureOverride.Value + direction * TemperatureChangeStep,
            MinimumTemperatureCelsius,
            MaximumTemperatureCelsius);
        return _temperatureOverride.Value;
    }

    public void ResetCloudCoverOverride() => _cloudCoverOverride = null;

    public void ResetTemperatureOverride() => _temperatureOverride = null;

    public bool ToggleManualRain()
    {
        _manualRainActive = !_manualRainActive;
        return _manualRainActive;
    }

    private void UpdateCloudCover(TimeSpan timeStep)
    {
        if (_cloudTargetRemaining <= TimeSpan.Zero)
        {
            _cloudTargetFraction = random.NextDouble();
            _cloudTargetRemaining = TimeSpan.FromMinutes(40 + random.NextDouble() * 140);
        }

        var smoothing = 1 - Math.Exp(-timeStep.TotalHours / 0.75);
        _cloudCoverFraction += (_cloudTargetFraction - _cloudCoverFraction) * smoothing;
        _cloudCoverFraction = Math.Clamp(_cloudCoverFraction, 0, 1);
        _cloudTargetRemaining -= timeStep;
    }

    private double CalculateAutomaticTemperature(DateTimeOffset simulatedAt, TimeSpan timeStep)
    {
        var localTime = simulatedAt.TimeOfDay.TotalHours;
        var latitude = Math.Abs(location.Latitude);
        var warmestDay = location.Latitude < 0 ? 20 : 200;
        var annualAngle = 2 * Math.PI * (simulatedAt.DayOfYear - warmestDay) / 365.2425;
        var seasonalMean = 15 - latitude * 0.12 - location.ElevationMeters * 0.0065;
        var seasonalAmplitude = Math.Min(12, 2 + latitude * 0.14);
        var dailyAmplitude = 4.5 * Math.Sin(2 * Math.PI * (localTime - 9) / 24);
        var targetTemperature = seasonalMean
            + seasonalAmplitude * Math.Cos(annualAngle)
            + dailyAmplitude;
        if (timeStep > TimeSpan.Zero)
        {
            var randomAnomaly = (random.NextDouble() * 2 - 1) * 2;
            var smoothing = 1 - Math.Exp(-timeStep.TotalHours / 2);
            _temperatureAnomaly += (randomAnomaly - _temperatureAnomaly) * smoothing;
        }

        return Math.Clamp(
            targetTemperature + _temperatureAnomaly,
            MinimumTemperatureCelsius,
            MaximumTemperatureCelsius);
    }

    private void UpdateAutomaticRain(TimeSpan timeStep)
    {
        if (_automaticRainRemaining > TimeSpan.Zero)
        {
            _automaticRainRemaining = _automaticRainRemaining > timeStep
                ? _automaticRainRemaining - timeStep
                : TimeSpan.Zero;
        }

        if (_automaticRainRemaining == TimeSpan.Zero && _cloudCoverFraction > 0.65)
        {
            var hourlyRainHazard = (_cloudCoverFraction - 0.65) * 0.8;
            var rainProbability = 1 - Math.Exp(-hourlyRainHazard * timeStep.TotalHours);
            if (random.NextDouble() < rainProbability)
            {
                _automaticRainRemaining = TimeSpan.FromMinutes(20 + random.NextDouble() * 100);
            }
        }
    }

    private static void ValidateDirection(int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
}
