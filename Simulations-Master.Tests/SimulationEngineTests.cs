using System;
using SimulationsMaster;
using Xunit;

namespace SimulationsMaster.Tests;

public class SimulationEngineTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultOrCustomTime()
    {
        var fixedTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var engine = new SimulationEngine(fixedTime);

        Assert.False(engine.IsRunning);
        Assert.Equal(fixedTime, engine.CurrentVirtualTime);
        Assert.Equal(fixedTime, engine.StartVirtualTime);
        Assert.Equal(TimeSpan.FromSeconds(1), engine.TimeStep);

        // Test with default (UtcNow)
        var before = DateTime.UtcNow;
        var defaultEngine = new SimulationEngine();
        var after = DateTime.UtcNow;

        Assert.False(defaultEngine.IsRunning);
        Assert.InRange(defaultEngine.CurrentVirtualTime, before, after);
    }

    [Fact]
    public void Start_Pause_Resume_ManagesRunningState_Correctly()
    {
        var initialTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new SimulationEngine(initialTime);

        // Advance current time before starting
        var advancedTime = initialTime.AddHours(2);
        engine.SetVirtualTime(advancedTime);

        // Start
        engine.Start();
        Assert.True(engine.IsRunning);
        Assert.Equal(advancedTime, engine.StartVirtualTime);

        // Calling Start again should be idempotent and preserve StartVirtualTime
        engine.Start();
        Assert.True(engine.IsRunning);
        Assert.Equal(advancedTime, engine.StartVirtualTime);

        // Pause
        engine.Pause();
        Assert.False(engine.IsRunning);

        // Calling Pause again is a no-op
        engine.Pause();
        Assert.False(engine.IsRunning);

        // Resume
        engine.Resume();
        Assert.True(engine.IsRunning);

        // Calling Resume again is a no-op
        engine.Resume();
        Assert.True(engine.IsRunning);
    }

    [Fact]
    public void SetVirtualTime_DirectlyUpdatesCurrentVirtualTime()
    {
        var engine = new SimulationEngine(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newTime = new DateTime(2026, 6, 1, 8, 30, 0, DateTimeKind.Utc);

        engine.SetVirtualTime(newTime);
        Assert.Equal(newTime, engine.CurrentVirtualTime);
        Assert.Equal(newTime, engine.GetCurrentVirtualTime());
    }

    [Fact]
    public void ChangeTimeStep_UpdatesTimeStep_AndValidatesPositiveDuration()
    {
        var engine = new SimulationEngine();

        engine.ChangeTimeStep(TimeSpan.FromMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(15), engine.TimeStep);

        // Zero and negative durations must throw
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.ChangeTimeStep(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.ChangeTimeStep(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void Restart_ThrowsNotSupportedException()
    {
        var engine = new SimulationEngine();
        Assert.Throws<NotSupportedException>(() => engine.Restart());
    }

    [Fact]
    public void AvailableTimeSteps_ReturnsNinePredefinedSteps()
    {
        var engine = new SimulationEngine();
        var steps = engine.GetAvailableTimeSteps();

        Assert.Equal(9, steps.Length);
        Assert.Contains(TimeSpan.FromSeconds(1), steps);
        Assert.Contains(TimeSpan.FromSeconds(10), steps);
        Assert.Contains(TimeSpan.FromMinutes(1), steps);
        Assert.Contains(TimeSpan.FromMinutes(15), steps);
        Assert.Contains(TimeSpan.FromHours(1), steps);
        Assert.Contains(TimeSpan.FromHours(2), steps);
        Assert.Contains(TimeSpan.FromHours(12), steps);
        Assert.Contains(TimeSpan.FromHours(24), steps);
        Assert.Contains(TimeSpan.FromDays(7), steps);
    }

    [Theory]
    [InlineData(4, 1)]            // 4s is closer to 1s than 10s
    [InlineData(8, 10)]           // 8s is closer to 10s
    [InlineData(45, 60)]          // 45s is closer to 60s (1 min) than 10s
    [InlineData(1200, 900)]       // 20 min (1200s) is closer to 15 min (900s) than 60 min (3600s)
    [InlineData(10800, 7200)]     // 3 hours (10800s) is closer to 2 hours (7200s) than 1 hour (3600s) or 12h (43200s)
    public void GetNearestTimeStep_SelectsClosestValidInterval(double requestSeconds, double expectedSeconds)
    {
        var engine = new SimulationEngine();
        var result = engine.GetNearestTimeStep(TimeSpan.FromSeconds(requestSeconds));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Fact]
    public void CommitTimeStep_AdvancesVirtualTime_OnlyWhenExpectedTimeMatches()
    {
        var initial = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var engine = new SimulationEngine(initial);
        var step = TimeSpan.FromMinutes(1);

        // Success case: previous time matches
        var committed = engine.CommitTimeStep(initial, step);
        Assert.True(committed);
        Assert.Equal(initial + step, engine.CurrentVirtualTime);

        // Failure case: out of sync / stale expected time
        var staleCommitted = engine.CommitTimeStep(initial, step);
        Assert.False(staleCommitted);
        Assert.Equal(initial + step, engine.CurrentVirtualTime); // Time remains unchanged

        // Invalid step <= 0 throws
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.CommitTimeStep(engine.CurrentVirtualTime, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.CommitTimeStep(engine.CurrentVirtualTime, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void ReconcileFromStation_OnlyAdvancesClockForward()
    {
        var current = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var engine = new SimulationEngine(current);

        // Station is ahead -> clock advances
        var future = current.AddHours(3);
        engine.ReconcileFromStation(future);
        Assert.Equal(future, engine.CurrentVirtualTime);

        // Station is behind -> clock does NOT move backward
        var past = current.AddHours(1);
        engine.ReconcileFromStation(past);
        Assert.Equal(future, engine.CurrentVirtualTime);

        // Station equals current -> clock unchanged
        engine.ReconcileFromStation(future);
        Assert.Equal(future, engine.CurrentVirtualTime);
    }

    [Fact]
    public void GetStatusSnapshot_ReturnsConsistentSnapshot()
    {
        var time = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var engine = new SimulationEngine(time);
        engine.ChangeTimeStep(TimeSpan.FromMinutes(5));
        engine.Start();

        var snapshot = engine.GetStatusSnapshot();
        Assert.True(snapshot.IsRunning);
        Assert.Equal(time, snapshot.CurrentVirtualTime);
        Assert.Equal(time, snapshot.StartVirtualTime);
        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.TimeStep);
    }
}
