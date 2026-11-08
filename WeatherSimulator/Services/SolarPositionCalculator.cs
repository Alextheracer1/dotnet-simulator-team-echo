using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public sealed class SolarPositionCalculator : ISolarPositionCalculator
{
    public SolarPosition Calculate(double latitude, double longitude, DateTimeOffset dateTime)
    {
        var utc = dateTime.UtcDateTime;
        var fractionalHour = utc.TimeOfDay.TotalHours;
        var dayAngle = 2.0 * Math.PI / 365.0 * (utc.DayOfYear - 1 + (fractionalHour - 12.0) / 24.0);

        var equationOfTimeMinutes = 229.18 * (0.000075
            + 0.001868 * Math.Cos(dayAngle)
            - 0.032077 * Math.Sin(dayAngle)
            - 0.014615 * Math.Cos(2.0 * dayAngle)
            - 0.040849 * Math.Sin(2.0 * dayAngle));

        var solarDeclination = 0.006918
            - 0.399912 * Math.Cos(dayAngle)
            + 0.070257 * Math.Sin(dayAngle)
            - 0.006758 * Math.Cos(2.0 * dayAngle)
            + 0.000907 * Math.Sin(2.0 * dayAngle)
            - 0.002697 * Math.Cos(3.0 * dayAngle)
            + 0.00148 * Math.Sin(3.0 * dayAngle);

        var trueSolarMinutes = fractionalHour * 60.0 + equationOfTimeMinutes + 4.0 * longitude;
        var hourAngle = DegreesToRadians(trueSolarMinutes / 4.0 - 180.0);
        var latitudeRad = DegreesToRadians(latitude);

        var sinElevation = Math.Sin(latitudeRad) * Math.Sin(solarDeclination)
                         + Math.Cos(latitudeRad) * Math.Cos(solarDeclination) * Math.Cos(hourAngle);
        sinElevation = Math.Clamp(sinElevation, -1.0, 1.0);
        var elevation = Math.Asin(sinElevation);

        var azimuth = Math.Atan2(
            Math.Sin(hourAngle),
            Math.Cos(hourAngle) * Math.Sin(latitudeRad) - Math.Tan(solarDeclination) * Math.Cos(latitudeRad));

        var azimuthDegrees = (RadiansToDegrees(azimuth) + 180.0 + 360.0) % 360.0;
        var elevationDegrees = RadiansToDegrees(elevation);

        return new SolarPosition(elevationDegrees, azimuthDegrees);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}

