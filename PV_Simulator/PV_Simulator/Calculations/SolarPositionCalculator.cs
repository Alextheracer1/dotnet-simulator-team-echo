using PV_Simulator.Domain;

namespace PV_Simulator.Calculations;

public sealed class SolarPositionCalculator
{
    public SolarPosition Calculate(Location location, DateTimeOffset dateTime)
    {
        var utc = dateTime.UtcDateTime;
        var fractionalHour = utc.TimeOfDay.TotalHours;
        var dayAngle = 2 * Math.PI / 365 * (utc.DayOfYear - 1 + (fractionalHour - 12) / 24);
        var equationOfTimeMinutes = 229.18 * (0.000075
            + 0.001868 * Math.Cos(dayAngle)
            - 0.032077 * Math.Sin(dayAngle)
            - 0.014615 * Math.Cos(2 * dayAngle)
            - 0.040849 * Math.Sin(2 * dayAngle));
        var solarDeclination = 0.006918
            - 0.399912 * Math.Cos(dayAngle)
            + 0.070257 * Math.Sin(dayAngle)
            - 0.006758 * Math.Cos(2 * dayAngle)
            + 0.000907 * Math.Sin(2 * dayAngle)
            - 0.002697 * Math.Cos(3 * dayAngle)
            + 0.00148 * Math.Sin(3 * dayAngle);
        var trueSolarMinutes = fractionalHour * 60 + equationOfTimeMinutes + 4 * location.Longitude;
        var hourAngle = DegreesToRadians(trueSolarMinutes / 4 - 180);
        var latitude = DegreesToRadians(location.Latitude);
        var elevation = Math.Asin(
            Math.Sin(latitude) * Math.Sin(solarDeclination)
            + Math.Cos(latitude) * Math.Cos(solarDeclination) * Math.Cos(hourAngle));
        var azimuth = Math.Atan2(
            Math.Sin(hourAngle),
            Math.Cos(hourAngle) * Math.Sin(latitude) - Math.Tan(solarDeclination) * Math.Cos(latitude));

        return new SolarPosition(
            RadiansToDegrees(elevation),
            (RadiansToDegrees(azimuth) + 180 + 360) % 360);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
}