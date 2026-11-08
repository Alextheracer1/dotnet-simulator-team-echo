using Battery.Battery;

namespace Battery.Simulation;

public static class AutoScenario
{
    public const double SocketChargingPowerKW = 2.3;
    public const double SolarChargingPowerKW = 2.0;

    private static readonly DeviceLoad[] DeviceProfiles =
    [
        new("Water heater", 1.8),
        new("Kitchen appliances", 0.9),
        new("Morning lighting", 0.5),
        new("Refrigerator", 0.4),
        new("Office equipment", 1.2),
        new("Cooker", 2.5),
        new("Laundry", 1.8),
        new("Evening lighting", 0.8),
        new("Entertainment", 0.5),
        new("Network equipment", 0.6)
    ];

    public static double GetTargetPowerKW(DateTime simulatedTime)
    {
        var timeOfDay = TimeOnly.FromDateTime(simulatedTime);
        var target = GetUnrampedTarget(timeOfDay);

        foreach (var transition in TargetTransitions)
        {
            var elapsed = timeOfDay - transition.StartTime;
            if (elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromHours(1))
                continue;

            var progress = elapsed.TotalHours;
            return transition.PreviousTargetKW +
                (transition.TargetKW - transition.PreviousTargetKW) * progress;
        }

        return target;
    }

    public static IReadOnlyList<DeviceLoad> SelectDeviceProfiles(DateTime simulatedTime, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var time = simulatedTime.TimeOfDay;
        var isHighDemandPeriod =
            (time >= new TimeSpan(6, 0, 0) && time < new TimeSpan(9, 0, 0) ||
             time >= new TimeSpan(17, 0, 0) && time < new TimeSpan(20, 0, 0));
        var minimumCount = isHighDemandPeriod ? 3 : 0;
        var maximumCount = isHighDemandPeriod ? 5 : 2;
        var selectedCount = random.Next(minimumCount, maximumCount + 1);
        var selected = new bool[DeviceProfiles.Length];

        for (var index = 0; index < selectedCount; index++)
        {
            int profileIndex;
            do
            {
                profileIndex = random.Next(DeviceProfiles.Length);
            } while (selected[profileIndex]);

            selected[profileIndex] = true;
        }

        return DeviceProfiles.Where((_, index) => selected[index]).ToArray();
    }

    public static bool IsVehicleConnected(DateTime simulatedTime)
    {
        var timeOfDay = TimeOnly.FromDateTime(simulatedTime);
        return timeOfDay >= new TimeOnly(18, 0) || timeOfDay < new TimeOnly(1, 0);
    }

    private static double GetUnrampedTarget(TimeOnly timeOfDay)
    {
        if (timeOfDay >= new TimeOnly(11, 0) && timeOfDay < new TimeOnly(15, 0))
            return SolarChargingPowerKW;

        return SocketChargingPowerKW;
    }

    private static readonly (TimeOnly StartTime, double PreviousTargetKW, double TargetKW)[] TargetTransitions =
    [
        (new TimeOnly(11, 0), SocketChargingPowerKW, SolarChargingPowerKW),
        (new TimeOnly(15, 0), SolarChargingPowerKW, SocketChargingPowerKW)
    ];
}
