using System.Globalization;

namespace TeamConsoleShared;

public static class SimulationTimeFormatter
{
    public static string FormatTickDuration(TimeSpan tickDuration)
    {
        if (tickDuration <= TimeSpan.Zero)
            return "0s";

        var remainingTicks = tickDuration.Ticks;
        var parts = new List<string>(4);
        AppendUnit(ref remainingTicks, TimeSpan.TicksPerDay, "d", parts);
        AppendUnit(ref remainingTicks, TimeSpan.TicksPerHour, "h", parts);
        AppendUnit(ref remainingTicks, TimeSpan.TicksPerMinute, "m", parts);

        if (remainingTicks > 0)
        {
            if (parts.Count == 0 && remainingTicks < TimeSpan.TicksPerSecond)
            {
                var milliseconds = (double)remainingTicks / TimeSpan.TicksPerMillisecond;
                parts.Add($"{milliseconds.ToString("0.###", CultureInfo.InvariantCulture)}ms");
            }
            else
            {
                var seconds = (double)remainingTicks / TimeSpan.TicksPerSecond;
                parts.Add($"{seconds.ToString("0.###", CultureInfo.InvariantCulture)}s");
            }
        }

        return string.Join(" ", parts);
    }

    private static void AppendUnit(ref long remainingTicks, long unitTicks, string unit, List<string> parts)
    {
        var count = remainingTicks / unitTicks;
        if (count == 0)
            return;

        parts.Add($"{count}{unit}");
        remainingTicks %= unitTicks;
    }
}
