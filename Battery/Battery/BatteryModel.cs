namespace Battery.Battery;

public sealed class BatteryModel
{
    public const double HardwareMaximumCapacityKWh = 10.0;
    public const double HardwareMaximumChargePowerKW = 3.0;
    public const double HardwareMaximumDischargePowerKW = 18.0;

    public double CapacityKWh { get; }
    public double MaximumChargePowerKW { get; }
    public double MaximumDischargePowerKW { get; }
    public double StateOfChargeKWh { get; private set; }
    public double StateOfChargePercent => StateOfChargeKWh / CapacityKWh * 100;
    public double CurrentPowerKW { get; private set; }
    public double ChargedEnergyKWh { get; private set; }
    public double DischargedEnergyKWh { get; private set; }
    public BatteryState State { get; private set; } = BatteryState.Idle;
    public string? FaultMessage { get; private set; }

    public BatteryModel(
        double capacityKWh,
        double maximumChargePowerKW,
        double maximumDischargePowerKW,
        double initialStateOfChargePercent)
    {
        if (!double.IsFinite(capacityKWh) || capacityKWh <= 0 || capacityKWh > HardwareMaximumCapacityKWh)
            throw new ArgumentOutOfRangeException(nameof(capacityKWh), $"Capacity must be positive and no greater than {HardwareMaximumCapacityKWh} kWh.");
        if (!double.IsFinite(maximumChargePowerKW) || maximumChargePowerKW <= 0 || maximumChargePowerKW > HardwareMaximumChargePowerKW)
            throw new ArgumentOutOfRangeException(nameof(maximumChargePowerKW), $"Maximum charge power must be positive and no greater than {HardwareMaximumChargePowerKW} kW.");
        if (!double.IsFinite(maximumDischargePowerKW) || maximumDischargePowerKW <= 0 || maximumDischargePowerKW > HardwareMaximumDischargePowerKW)
            throw new ArgumentOutOfRangeException(nameof(maximumDischargePowerKW), $"Maximum discharge power must be positive and no greater than {HardwareMaximumDischargePowerKW} kW.");
        if (!double.IsFinite(initialStateOfChargePercent) || initialStateOfChargePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(initialStateOfChargePercent));

        CapacityKWh = capacityKWh;
        MaximumChargePowerKW = maximumChargePowerKW;
        MaximumDischargePowerKW = maximumDischargePowerKW;
        StateOfChargeKWh = capacityKWh * initialStateOfChargePercent / 100;
    }

    public void Advance(double requestedPowerKW, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time must be positive.");
        if (!double.IsFinite(requestedPowerKW))
            throw new ArgumentOutOfRangeException(nameof(requestedPowerKW), "Power must be finite.");
        if (State == BatteryState.Fault)
        {
            CurrentPowerKW = 0;
            return;
        }

        var requested = Math.Clamp(requestedPowerKW, -MaximumDischargePowerKW, MaximumChargePowerKW);
        if (requested > 0)
        {
            var remainingEnergyKWh = CapacityKWh - StateOfChargeKWh;
            if (remainingEnergyKWh <= CapacityKWh * 0.001)
            {
                StateOfChargeKWh = CapacityKWh;
                ChargedEnergyKWh += remainingEnergyKWh;
                CurrentPowerKW = 0;
                State = BatteryState.Idle;
                return;
            }

            const double taperStartPercent = 80;
            const double taperSharpness = 0.5;
            var stateOfChargePercent = StateOfChargePercent;
            if (stateOfChargePercent > taperStartPercent)
            {
                var taperProgress = (stateOfChargePercent - taperStartPercent) / (100 - taperStartPercent);
                var endFactor = Math.Exp(-taperSharpness);
                var taperFactor = (Math.Exp(-taperSharpness * taperProgress) - endFactor) / (1 - endFactor);
                requested *= Math.Clamp(taperFactor, 0, 1);
            }
        }

        var simulatedHours = elapsed.TotalHours;
        var energyChangeKWh = requested * simulatedHours;
        if (energyChangeKWh > 0)
            energyChangeKWh = Math.Min(energyChangeKWh, CapacityKWh - StateOfChargeKWh);
        else if (energyChangeKWh < 0)
            energyChangeKWh = Math.Max(energyChangeKWh, -StateOfChargeKWh);

        StateOfChargeKWh = Math.Clamp(StateOfChargeKWh + energyChangeKWh, 0, CapacityKWh);
        CurrentPowerKW = energyChangeKWh / simulatedHours;
        if (energyChangeKWh > 0)
        {
            ChargedEnergyKWh += energyChangeKWh;
            State = BatteryState.Charging;
        }
        else if (energyChangeKWh < 0)
        {
            DischargedEnergyKWh += -energyChangeKWh;
            State = BatteryState.Discharging;
        }
        else
        {
            CurrentPowerKW = 0;
            State = BatteryState.Idle;
        }
    }

    public void InjectFault(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A fault message is required.", nameof(message));

        FaultMessage = message.Trim();
        CurrentPowerKW = 0;
        State = BatteryState.Fault;
    }

    public void ResetFault()
    {
        FaultMessage = null;
        CurrentPowerKW = 0;
        State = BatteryState.Idle;
    }
}
