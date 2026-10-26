namespace TeamConsoleShared;

public static class PowerBarFormatter
{
    public static string CreatePowerBar(
        string label,
        double currentPowerKilowatts,
        double maxPowerKilowatts,
        string maxUnit,
        int barWidth = 28)
    {
        if (barWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barWidth));
        }

        var safeMax = maxPowerKilowatts > 0 ? maxPowerKilowatts : 1;
        var ratio = Math.Clamp(currentPowerKilowatts / safeMax, 0, 1);
        var percentage = (int)(ratio * 100);
        var filled = (int)(ratio * barWidth);
        var bar = new string('█', filled) + new string('░', barWidth - filled);

        return $"{label}: {currentPowerKilowatts,7:F2} kW [{bar}] {percentage,3}% von {maxPowerKilowatts:0.###} {maxUnit}";
    }
}
