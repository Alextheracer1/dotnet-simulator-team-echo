namespace PV_Simulator.Domain;

public sealed record WeatherConditions
{
    public WeatherConditions(
        double cloudCoverFraction,
        double ambientTemperatureCelsius,
        Precipitation precipitation,
        bool cloudCoverManuallyOverridden = false,
        bool temperatureManuallyOverridden = false,
        bool manualRainActive = false)
    {
        if (cloudCoverFraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cloudCoverFraction));
        }

        CloudCoverFraction = cloudCoverFraction;
        AmbientTemperatureCelsius = ambientTemperatureCelsius;
        Precipitation = precipitation;
        CloudCoverManuallyOverridden = cloudCoverManuallyOverridden;
        TemperatureManuallyOverridden = temperatureManuallyOverridden;
        ManualRainActive = manualRainActive;
    }

    public double CloudCoverFraction { get; }
    public double AmbientTemperatureCelsius { get; }
    public Precipitation Precipitation { get; }
    public bool CloudCoverManuallyOverridden { get; }
    public bool TemperatureManuallyOverridden { get; }
    public bool ManualRainActive { get; }
}

public enum Precipitation
{
    None,
    Rain
}