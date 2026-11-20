using System;
using Battery.Battery;
using Xunit;

namespace Battery.Tests;

public class BatteryModelTests
{
    [Fact]
    public void Constructor_SetsInitialProperties_Correctly()
    {
        // 10 kWh battery, 3 kW max charge, 18 kW max discharge, 50% initial SOC
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        Assert.Equal(10.0, battery.CapacityKWh);
        Assert.Equal(3.0, battery.MaximumChargePowerKW);
        Assert.Equal(18.0, battery.MaximumDischargePowerKW);
        Assert.Equal(5.0, battery.StateOfChargeKWh);
        Assert.Equal(50.0, battery.StateOfChargePercent);
        Assert.Equal(0.0, battery.CurrentPowerKW);
        Assert.Equal(0.0, battery.ChargedEnergyKWh);
        Assert.Equal(0.0, battery.DischargedEnergyKWh);
        Assert.Equal(BatteryState.Idle, battery.State);
        Assert.Null(battery.FaultMessage);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(10.5)] // Exceeds HardwareMaximumCapacityKWh (10.0)
    public void Constructor_Throws_WhenCapacityInvalid(double capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BatteryModel(capacity, 3.0, 18.0, 50.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(3.5)] // Exceeds HardwareMaximumChargePowerKW (3.0)
    public void Constructor_Throws_WhenMaxChargePowerInvalid(double maxChargePower)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BatteryModel(10.0, maxChargePower, 18.0, 50.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(19.0)] // Exceeds HardwareMaximumDischargePowerKW (18.0)
    public void Constructor_Throws_WhenMaxDischargePowerInvalid(double maxDischargePower)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BatteryModel(10.0, 3.0, maxDischargePower, 50.0));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Constructor_Throws_WhenInitialSocOutOfRange(double initialSoc)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BatteryModel(10.0, 3.0, 18.0, initialSoc));
    }

    [Fact]
    public void Advance_Throws_WhenElapsedIsZeroOrNegative()
    {
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            battery.Advance(2.0, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            battery.Advance(2.0, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Advance_Throws_WhenPowerIsNotFinite()
    {
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            battery.Advance(double.NaN, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            battery.Advance(double.PositiveInfinity, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Advance_ChargesBattery_IncreasesSocAndTracksEnergy()
    {
        // 10 kWh battery starting at 20% (2.0 kWh)
        var battery = new BatteryModel(10.0, 3.0, 18.0, 20.0);

        // Charge with 2.5 kW for 1 hour -> +2.5 kWh
        battery.Advance(2.5, TimeSpan.FromHours(1));

        Assert.Equal(BatteryState.Charging, battery.State);
        Assert.Equal(4.5, battery.StateOfChargeKWh, 4);
        Assert.Equal(45.0, battery.StateOfChargePercent, 2);
        Assert.Equal(2.5, battery.ChargedEnergyKWh, 4);
        Assert.Equal(2.5, battery.CurrentPowerKW, 4);
        Assert.Equal(0.0, battery.DischargedEnergyKWh);
    }

    [Fact]
    public void Advance_ClampsChargePower_ToHardwareMaximum()
    {
        // Battery with 3.0 kW max charge power
        var battery = new BatteryModel(10.0, 3.0, 18.0, 10.0);

        // Request 10.0 kW (exceeding 3.0 kW) for 1 hour
        battery.Advance(10.0, TimeSpan.FromHours(1));

        // Power should be clamped to 3.0 kW
        Assert.Equal(3.0, battery.CurrentPowerKW, 4);
        Assert.Equal(4.0, battery.StateOfChargeKWh, 4); // 1.0 + 3.0 = 4.0
        Assert.Equal(3.0, battery.ChargedEnergyKWh, 4);
    }

    [Fact]
    public void Advance_DischargesBattery_DecreasesSocAndTracksEnergy()
    {
        // 10 kWh battery starting at 80% (8.0 kWh)
        var battery = new BatteryModel(10.0, 3.0, 18.0, 80.0);

        // Discharge with -5.0 kW for 1 hour -> -5.0 kWh
        battery.Advance(-5.0, TimeSpan.FromHours(1));

        Assert.Equal(BatteryState.Discharging, battery.State);
        Assert.Equal(3.0, battery.StateOfChargeKWh, 4);
        Assert.Equal(30.0, battery.StateOfChargePercent, 2);
        Assert.Equal(5.0, battery.DischargedEnergyKWh, 4);
        Assert.Equal(-5.0, battery.CurrentPowerKW, 4);
    }

    [Fact]
    public void Advance_ClampsDischargePower_ToHardwareMaximum()
    {
        // Max discharge 18.0 kW, starting at 100% (10 kWh)
        var battery = new BatteryModel(10.0, 3.0, 18.0, 100.0);

        // Request -25.0 kW for 0.1 hours
        battery.Advance(-25.0, TimeSpan.FromHours(0.1));

        // Power clamped to -18.0 kW, energy = 1.8 kWh
        Assert.Equal(-18.0, battery.CurrentPowerKW, 4);
        Assert.Equal(8.2, battery.StateOfChargeKWh, 4);
        Assert.Equal(1.8, battery.DischargedEnergyKWh, 4);
    }

    [Fact]
    public void Advance_TapersChargePower_WhenSocAbove80Percent()
    {
        // Start battery at 85% SOC
        var battery = new BatteryModel(10.0, 3.0, 18.0, 85.0);

        // Request 2.0 kW for 0.1 hours
        battery.Advance(2.0, TimeSpan.FromHours(0.1));

        // Because SOC > 80%, effective power must be strictly less than 2.0 kW
        Assert.True(battery.CurrentPowerKW < 2.0, $"Expected tapered power < 2.0, got {battery.CurrentPowerKW}");
        Assert.True(battery.CurrentPowerKW > 0.0, $"Expected positive tapered power, got {battery.CurrentPowerKW}");
        Assert.Equal(BatteryState.Charging, battery.State);
    }

    [Fact]
    public void Advance_TopOffThreshold_CompletesFullChargeAndTransitionsToIdle()
    {
        // Battery nearly full: 99.95% (remaining energy 0.005 kWh <= 10.0 * 0.001 = 0.01 kWh)
        var battery = new BatteryModel(10.0, 3.0, 18.0, 99.95);

        // Advance charging
        battery.Advance(1.0, TimeSpan.FromHours(0.1));

        Assert.Equal(10.0, battery.StateOfChargeKWh);
        Assert.Equal(100.0, battery.StateOfChargePercent);
        Assert.Equal(0.0, battery.CurrentPowerKW);
        Assert.Equal(BatteryState.Idle, battery.State);
    }

    [Fact]
    public void Advance_PreventsDischarge_BelowZeroCapacity()
    {
        // Battery almost empty: 0.5 kWh (5%)
        var battery = new BatteryModel(10.0, 3.0, 18.0, 5.0);

        // Try to discharge 5.0 kW for 1 hour (would require 5.0 kWh)
        battery.Advance(-5.0, TimeSpan.FromHours(1));

        // Clamped to 0 kWh remaining, discharged only 0.5 kWh
        Assert.Equal(0.0, battery.StateOfChargeKWh);
        Assert.Equal(0.0, battery.StateOfChargePercent);
        Assert.Equal(0.5, battery.DischargedEnergyKWh, 4);
    }

    [Fact]
    public void Advance_ZeroPower_TransitionsToIdle()
    {
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        battery.Advance(0.0, TimeSpan.FromHours(1));

        Assert.Equal(BatteryState.Idle, battery.State);
        Assert.Equal(0.0, battery.CurrentPowerKW);
        Assert.Equal(5.0, battery.StateOfChargeKWh);
    }

    [Fact]
    public void FaultInjection_HaltsOperationAndBlocksAdvance()
    {
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        // Inject fault
        battery.InjectFault("Overheating detected");

        Assert.Equal(BatteryState.Fault, battery.State);
        Assert.Equal("Overheating detected", battery.FaultMessage);
        Assert.Equal(0.0, battery.CurrentPowerKW);

        // Advance should do nothing while in Fault
        battery.Advance(2.0, TimeSpan.FromHours(1));
        Assert.Equal(0.0, battery.CurrentPowerKW);
        Assert.Equal(5.0, battery.StateOfChargeKWh); // Unchanged
        Assert.Equal(BatteryState.Fault, battery.State);

        // Reset fault
        battery.ResetFault();
        Assert.Equal(BatteryState.Idle, battery.State);
        Assert.Null(battery.FaultMessage);

        // Now advance works normally again
        battery.Advance(2.0, TimeSpan.FromHours(1));
        Assert.Equal(BatteryState.Charging, battery.State);
        Assert.Equal(7.0, battery.StateOfChargeKWh, 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InjectFault_Throws_WhenMessageEmptyOrWhitespace(string message)
    {
        var battery = new BatteryModel(10.0, 3.0, 18.0, 50.0);

        Assert.Throws<ArgumentException>(() => battery.InjectFault(message));
    }
}
