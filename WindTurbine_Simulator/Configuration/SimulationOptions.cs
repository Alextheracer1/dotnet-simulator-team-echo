namespace WindTurbine_Simulator.Configuration;

public sealed class SimulationOptions
{
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.Now;
    public TimeSpan StepInterval { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan? Duration { get; init; } = TimeSpan.FromHours(24);
    public double SpeedMultiplier { get; init; } = 60;
    public double MinimumWindSpeedMps { get; init; } = 2.5;
    public double MaximumWindSpeedMps { get; init; } = 18.0;
    public double AirDensityKgPerM3 { get; init; } = 1.225;
    public double RotorAreaM2 { get; init; } = 8000.0;
    public double PowerCoefficient { get; init; } = 0.42;
    public double RatedPowerKw { get; init; } = 3000.0;
    public int ModbusPort { get; init; } = 1504; // Eigener Port für das Windrad
    public byte ModbusUnitId { get; init; } = 1;
}
