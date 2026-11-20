using System;
using Battery.Simulation;
using Xunit;

namespace Battery.Tests;

public class AutoScenarioTests
{
    [Fact]
    public void GetTargetPowerKW_ReturnsSolarCharging_DuringSolarHours()
    {
        // 13:00 is well within the 12:00 - 15:00 solar window
        var date = new DateTime(2026, 6, 15, 13, 0, 0);
        var power = AutoScenario.GetTargetPowerKW(date);

        Assert.Equal(AutoScenario.SolarChargingPowerKW, power);
    }

    [Fact]
    public void GetTargetPowerKW_ReturnsSocketCharging_DuringOffPeakHours()
    {
        // 02:00 and 21:00 are standard socket charging hours
        var night = new DateTime(2026, 6, 15, 2, 0, 0);
        var evening = new DateTime(2026, 6, 15, 21, 0, 0);

        Assert.Equal(AutoScenario.SocketChargingPowerKW, AutoScenario.GetTargetPowerKW(night));
        Assert.Equal(AutoScenario.SocketChargingPowerKW, AutoScenario.GetTargetPowerKW(evening));
    }

    [Fact]
    public void GetTargetPowerKW_InterpolatesRamp_DuringTransitionHours()
    {
        // 11:30 is 50% into the transition from Socket (2.3) to Solar (2.0)
        var rampDown = new DateTime(2026, 6, 15, 11, 30, 0);
        var powerDown = AutoScenario.GetTargetPowerKW(rampDown);
        Assert.Equal(2.15, powerDown, 4);

        // 15:30 is 50% into the transition from Solar (2.0) to Socket (2.3)
        var rampUp = new DateTime(2026, 6, 15, 15, 30, 0);
        var powerUp = AutoScenario.GetTargetPowerKW(rampUp);
        Assert.Equal(2.15, powerUp, 4);
    }

    [Fact]
    public void IsVehicleConnected_ReturnsTrue_OnlyDuringEveningAndNight()
    {
        // Connected: >= 18:00 or < 01:00
        Assert.True(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 18, 0, 0)));
        Assert.True(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 22, 30, 0)));
        Assert.True(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 0, 30, 0)));

        // Not connected: 01:00 to 17:59
        Assert.False(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 1, 0, 0)));
        Assert.False(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 8, 0, 0)));
        Assert.False(AutoScenario.IsVehicleConnected(new DateTime(2026, 6, 15, 14, 0, 0)));
    }

    [Fact]
    public void SelectDeviceProfiles_Throws_WhenRandomIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AutoScenario.SelectDeviceProfiles(DateTime.UtcNow, null!));
    }

    [Fact]
    public void SelectDeviceProfiles_SelectsBetween3And5Devices_InHighDemandPeriods()
    {
        var random = new Random(42);
        // High demand: 06:00-09:00 and 17:00-20:00
        var morningPeak = new DateTime(2026, 6, 15, 7, 30, 0);
        var eveningPeak = new DateTime(2026, 6, 15, 18, 30, 0);

        for (int i = 0; i < 20; i++)
        {
            var morningDevices = AutoScenario.SelectDeviceProfiles(morningPeak, random);
            Assert.InRange(morningDevices.Count, 3, 5);

            var eveningDevices = AutoScenario.SelectDeviceProfiles(eveningPeak, random);
            Assert.InRange(eveningDevices.Count, 3, 5);
        }
    }

    [Fact]
    public void SelectDeviceProfiles_SelectsBetween0And2Devices_InLowDemandPeriods()
    {
        var random = new Random(42);
        // Low demand: midday or late night
        var midday = new DateTime(2026, 6, 15, 13, 0, 0);
        var night = new DateTime(2026, 6, 15, 3, 0, 0);

        for (int i = 0; i < 20; i++)
        {
            var middayDevices = AutoScenario.SelectDeviceProfiles(midday, random);
            Assert.InRange(middayDevices.Count, 0, 2);

            var nightDevices = AutoScenario.SelectDeviceProfiles(night, random);
            Assert.InRange(nightDevices.Count, 0, 2);
        }
    }
}
