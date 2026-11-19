namespace WeatherSimulator.Models;

public sealed record WeatherRecord(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double TemperatureC,
    double PrecipitationMm,
    double CloudCoverPercent,
    double WindSpeedMps);

