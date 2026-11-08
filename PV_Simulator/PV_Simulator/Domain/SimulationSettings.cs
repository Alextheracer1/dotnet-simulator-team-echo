using System;

namespace PV_Simulator.Domain;

public sealed record SimulationSettings
{
    public SimulationSettings(DateTimeOffset startTime, TimeSpan timeStep, double speedMultiplier, TimeSpan? duration = null)
    {
        if (timeStep <= TimeSpan.Zero || timeStep > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeStep));
        }

        if (speedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        if (duration.HasValue && duration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        StartTime = startTime;
        TimeStep = timeStep;
        SpeedMultiplier = speedMultiplier;
        Duration = duration;
    }

    public DateTimeOffset StartTime { get; }
    public TimeSpan TimeStep { get; }
    public TimeSpan? Duration { get; }
    public double SpeedMultiplier { get; }
}