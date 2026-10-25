using PV_Simulator.Domain;

namespace PV_Simulator.Calculations;

public sealed class PvPowerCalculator
{
    private const double TemperatureCoefficientPerCelsius = 0.004;

    public double CalculatePlaneOfArrayIrradiance(
        SolarPosition sun,
        PvArray array,
        WeatherConditions weather)
    {
        if (sun.ElevationDegrees <= 0)
        {
            return 0;
        }

        var elevation = DegreesToRadians(sun.ElevationDegrees);
        var tilt = DegreesToRadians(array.TiltDegrees);
        var azimuthDifference = DegreesToRadians(sun.AzimuthDegrees - array.AzimuthDegrees);
        var sineElevation = Math.Sin(elevation);
        var cloudTransmittance = 1 - 0.75 * Math.Pow(weather.CloudCoverFraction, 3.4);
        var globalHorizontal = 1000 * sineElevation * cloudTransmittance;
        var diffuseHorizontal = globalHorizontal * (0.1 + 0.8 * weather.CloudCoverFraction);
        var directNormal = Math.Max(0, globalHorizontal - diffuseHorizontal) / Math.Max(sineElevation, 0.1);
        var incidenceCosine = Math.Max(0,
            sineElevation * Math.Cos(tilt)
            + Math.Cos(elevation) * Math.Sin(tilt) * Math.Cos(azimuthDifference));
        var diffuseOnArray = diffuseHorizontal * (1 + Math.Cos(tilt)) / 2;
        var groundReflected = globalHorizontal * 0.2 * (1 - Math.Cos(tilt)) / 2;

        return Math.Max(0, directNormal * incidenceCosine + diffuseOnArray + groundReflected);
    }

    public double CalculatePowerKilowatts(
        PvArray array,
        Inverter inverter,
        WeatherConditions weather,
        double irradianceWattsPerSquareMeter,
        bool snowCoverActive,
        InverterStatus inverterStatus)
    {
        if (inverterStatus != InverterStatus.Running)
        {
            return 0;
        }

        var cellTemperature = weather.AmbientTemperatureCelsius
            + 25 * irradianceWattsPerSquareMeter / 800;
        var temperatureFactor = Math.Max(0, 1 - TemperatureCoefficientPerCelsius * (cellTemperature - 25));
        var dcPower = array.ModuleNominalPowerKilowattsPeak
            * irradianceWattsPerSquareMeter / 1000
            * (1 - array.SystemLossFraction)
            * temperatureFactor
            * (snowCoverActive ? 0 : 1);

        return Math.Min(dcPower, inverter.RatedPowerKilowatts);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}