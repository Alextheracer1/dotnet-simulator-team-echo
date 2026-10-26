using System;

namespace PV_Simulator.Domain;

public sealed record SimulationSettings
{
    public SimulationSettings(DateTimeOffset startTime, TimeSpan timeStep, double speedMultiplier)
    {
        if (timeStep <= TimeSpan.Zero || timeStep > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeStep));
        }

        if (speedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        StartTime = startTime;
        TimeStep = timeStep;
        SpeedMultiplier = speedMultiplier;
    }

    public DateTimeOffset StartTime { get; }
    public TimeSpan TimeStep { get; }
    public TimeSpan Duration { get; } = TimeSpan.FromHours(24);
    public double SpeedMultiplier { get; }
}