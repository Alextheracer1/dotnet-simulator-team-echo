namespace WeatherSimulator.Models;

public sealed record WeatherResponse(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double TemperatureC,
    double PrecipitationMm,
    double CloudCoverPercent,
    double WindSpeedMps,
    double Azimuth,
    double Elevation);

