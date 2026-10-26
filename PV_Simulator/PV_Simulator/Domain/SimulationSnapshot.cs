using System;

namespace PV_Simulator.Domain;

public sealed record SimulationSnapshot(
    int StepNumber,
    DateTimeOffset SimulatedAt,
    WeatherConditions Weather,
    double IrradianceWattsPerSquareMeter,
    double PowerKilowatts,
    double DailyEnergyKilowattHours,
    bool SnowCoverActive,
    InverterStatus InverterStatus,
    bool ManualFaultActive);
