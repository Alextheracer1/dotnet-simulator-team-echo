using System;

namespace PV_Simulator.Domain;

public static class SimulationDefaults
{
    public const string DeviceId = "PV-FH-01";
    public const double PvArrayTiltDegrees = 30;
    public const double PvArrayAzimuthDegrees = 180;
    public const double InverterFailureProbabilityPerHour = 0.01;
    public static readonly TimeSpan InverterRepairDuration = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan SimulationTimeStep = TimeSpan.FromMinutes(5);
    public const double SimulationSpeedMultiplier = 60;

    public static Location CreateLocation() => new("FH Salzburg", 47, 13, 400);

    public static SimulationSettings CreateSettings()
    {
        var today = DateTime.Today;
        var localMidnight = new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));

        return new SimulationSettings(localMidnight, SimulationTimeStep, SimulationSpeedMultiplier);
    }

    public static SimulationConfiguration Create(
        double pvModuleNominalPowerKilowattsPeak,
        double inverterMaximumOutputKilowatts)
    {
        return new SimulationConfiguration(
            CreateLocation(),
            new PvArray(DeviceId, pvModuleNominalPowerKilowattsPeak, PvArrayTiltDegrees, PvArrayAzimuthDegrees),
            new Inverter(inverterMaximumOutputKilowatts, InverterFailureProbabilityPerHour, InverterRepairDuration),
            CreateSettings(),
            new WeatherSettings(null));
    }
}