namespace WindTurbine_Simulator.Domain;

public sealed class WindTurbineModel
{
    public WindTurbineModel(double airDensityKgPerM3, double rotorAreaM2, double powerCoefficient, double ratedPowerKw)
    {
        AirDensityKgPerM3 = airDensityKgPerM3;
        RotorAreaM2 = rotorAreaM2;
        PowerCoefficient = powerCoefficient;
        RatedPowerKw = ratedPowerKw;
    }

    public double AirDensityKgPerM3 { get; }
    public double RotorAreaM2 { get; }
    public double PowerCoefficient { get; }
    public double RatedPowerKw { get; }

    public double CalculateActivePowerKw(double windSpeedMps)
    {
        if (windSpeedMps <= 0)
        {
            return 0;
        }

        var powerWatts = 0.5 * AirDensityKgPerM3 * RotorAreaM2 * PowerCoefficient * Math.Pow(windSpeedMps, 3);
        var powerKw = powerWatts / 1000.0;

        return Math.Clamp(powerKw, 0, RatedPowerKw);
    }
}
