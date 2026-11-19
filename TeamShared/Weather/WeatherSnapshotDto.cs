namespace TeamShared.Weather;

/// <summary>
/// Einheitliches DTO für Wetter-Snapshots (Push vom WeatherSimulator an Feldgeräte).
/// </summary>
public sealed record WeatherSnapshotDto(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double TemperatureC,
    double PrecipitationMm,
    double CloudCoverPercent,
    double WindSpeedMps,
    double Azimuth,
    double Elevation);

