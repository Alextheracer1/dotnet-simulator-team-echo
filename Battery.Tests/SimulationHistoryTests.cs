using System;
using Battery.Simulation;
using Xunit;

namespace Battery.Tests;

public class SimulationHistoryTests
{
    [Fact]
    public void RecordStateOfCharge_Throws_WhenPercentNotFinite()
    {
        var history = new SimulationHistory();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            history.RecordStateOfCharge(DateTime.UtcNow, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            history.RecordStateOfCharge(DateTime.UtcNow, double.PositiveInfinity));
    }

    [Fact]
    public void RecordStateOfCharge_TracksHistoryAndEnforcesRollingLimit()
    {
        var history = new SimulationHistory();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0);

        // Record 120 samples (exceeding limit of 96)
        for (int i = 0; i < 120; i++)
        {
            history.RecordStateOfCharge(baseTime.AddMinutes(15 * i), i * 0.5);
        }

        Assert.Equal(120, history.StateOfChargeByTimestamp.Count);
        Assert.Equal(SimulationHistory.RollingReadingLimit, history.RecentStateOfCharge.Count);

        // First sample in the rolling queue should be sample 120 - 96 = index 24 (value 24 * 0.5 = 12.0)
        var firstRecent = history.RecentStateOfCharge.First();
        Assert.Equal(12.0, firstRecent.Percent);
    }

    [Fact]
    public void GetRollingSummary_ReturnsNull_WhenEmpty()
    {
        var history = new SimulationHistory();

        Assert.Null(history.GetRollingSummary());
    }

    [Fact]
    public void GetRollingSummary_CalculatesAverageMinMax_Accurately()
    {
        var history = new SimulationHistory();
        var time = DateTime.UtcNow;

        history.RecordStateOfCharge(time, 20.0);
        history.RecordStateOfCharge(time.AddMinutes(1), 50.0);
        history.RecordStateOfCharge(time.AddMinutes(2), 80.0);

        var summary = history.GetRollingSummary();
        Assert.NotNull(summary);
        Assert.Equal(50.0, summary.AveragePercent);
        Assert.Equal(20.0, summary.MinimumPercent);
        Assert.Equal(80.0, summary.MaximumPercent);
    }

    [Fact]
    public void GetDailySummaries_AggregatesReadingsByDay()
    {
        var history = new SimulationHistory();
        var day1 = new DateTime(2026, 3, 10, 10, 0, 0);
        var day2 = new DateTime(2026, 3, 11, 12, 0, 0);

        history.RecordStateOfCharge(day1, 40.0);
        history.RecordStateOfCharge(day1.AddHours(2), 60.0);

        history.RecordStateOfCharge(day2, 70.0);
        history.RecordStateOfCharge(day2.AddHours(3), 90.0);

        var daily = history.GetDailySummaries(day1.Date);

        Assert.Equal(2, daily.Count);

        // Day 1: 40 and 60 -> sum 100, avg 50, min 40, max 60
        Assert.Equal(DateOnly.FromDateTime(day1), daily[0].Date);
        Assert.Equal(100.0, daily[0].TotalPercent);
        Assert.Equal(50.0, daily[0].AveragePercent);
        Assert.Equal(40.0, daily[0].MinimumPercent);
        Assert.Equal(60.0, daily[0].MaximumPercent);

        // Day 2: 70 and 90 -> sum 160, avg 80, min 70, max 90
        Assert.Equal(DateOnly.FromDateTime(day2), daily[1].Date);
        Assert.Equal(160.0, daily[1].TotalPercent);
        Assert.Equal(80.0, daily[1].AveragePercent);
        Assert.Equal(70.0, daily[1].MinimumPercent);
        Assert.Equal(90.0, daily[1].MaximumPercent);
    }
}
