namespace TeamShared.Weather;

/// <summary>
/// Registrierungsdaten eines Feldgeräts (PV oder Windrad) beim WeatherSimulator für Wetter-Push-Nachrichten.
/// </summary>
public sealed class DeviceWeatherRegistration
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "pv" oder "wind"
    public string CallbackUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

