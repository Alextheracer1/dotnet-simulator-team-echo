namespace PV_Simulator.Domain;

public sealed record Inverter
{
    public Inverter(double ratedPowerKilowatts, double failureProbabilityPerHour, TimeSpan repairDuration)
    {
        if (ratedPowerKilowatts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratedPowerKilowatts));
        }

        if (failureProbabilityPerHour is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(failureProbabilityPerHour));
        }

        if (repairDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(repairDuration));
        }

        RatedPowerKilowatts = ratedPowerKilowatts;
        FailureProbabilityPerHour = failureProbabilityPerHour;
        RepairDuration = repairDuration;
    }

    public double RatedPowerKilowatts { get; }
    public double FailureProbabilityPerHour { get; }
    public TimeSpan RepairDuration { get; }
}