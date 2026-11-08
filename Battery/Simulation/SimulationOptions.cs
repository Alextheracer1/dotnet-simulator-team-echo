using Battery.Battery;

namespace Battery.Simulation;

public sealed class SimulationOptions
{
    public const int DefaultModbusPort = 1503;
    public const int MaximumDeviceCount = 5;
    public const double MaximumTotalDeviceDemandKW = 20;
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1);

    public double CapacityKWh { get; init; } = BatteryModel.HardwareMaximumCapacityKWh;
    public double MaximumChargePowerKW { get; init; } = BatteryModel.HardwareMaximumChargePowerKW;
    public double MaximumDischargePowerKW { get; init; } = BatteryModel.HardwareMaximumDischargePowerKW;
    public double InitialStateOfChargePercent { get; init; } = 0;
    public TimeSpan RealTimeStepInterval { get; init; } = TimeSpan.FromSeconds(1);
    public DateTime StartTime { get; init; } = DateTime.Today;
    public TimeSpan Duration { get; init; } = DefaultDuration;
    public string CsvPath { get; init; } = Path.Combine("Battery", "data", "battery-run.csv");
    public int ModbusPort { get; init; } = DefaultModbusPort;
    public byte ModbusUnitId { get; init; } = 1;
    public IReadOnlyList<DeviceLoad> DeviceLoads { get; init; } = Array.Empty<DeviceLoad>();
    public double TotalDeviceDemandKW => DeviceLoads.Sum(device => device.PowerDemandKW);

    public void Validate()
    {
        if (!double.IsFinite(CapacityKWh) || CapacityKWh <= 0 || CapacityKWh > BatteryModel.HardwareMaximumCapacityKWh)
            throw new ArgumentOutOfRangeException(nameof(CapacityKWh), $"Capacity must be positive and no greater than {BatteryModel.HardwareMaximumCapacityKWh} kWh.");
        if (!double.IsFinite(MaximumChargePowerKW) || MaximumChargePowerKW <= 0 || MaximumChargePowerKW > BatteryModel.HardwareMaximumChargePowerKW)
            throw new ArgumentOutOfRangeException(nameof(MaximumChargePowerKW), $"Maximum charge power must be positive and no greater than {BatteryModel.HardwareMaximumChargePowerKW} kW.");
        if (!double.IsFinite(MaximumDischargePowerKW) || MaximumDischargePowerKW <= 0 || MaximumDischargePowerKW > BatteryModel.HardwareMaximumDischargePowerKW)
            throw new ArgumentOutOfRangeException(nameof(MaximumDischargePowerKW), $"Maximum discharge power must be positive and no greater than {BatteryModel.HardwareMaximumDischargePowerKW} kW.");
        if (!double.IsFinite(InitialStateOfChargePercent) || InitialStateOfChargePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(InitialStateOfChargePercent), "Initial state of charge must be between 0 and 100 percent.");
        if (Duration <= TimeSpan.Zero || RealTimeStepInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Duration), "Duration and real-time step interval must be positive.");
        if (DeviceLoads is null)
            throw new ArgumentNullException(nameof(DeviceLoads));
        if (DeviceLoads.Count > MaximumDeviceCount)
            throw new ArgumentOutOfRangeException(nameof(DeviceLoads), $"At most {MaximumDeviceCount} devices may be connected.");
        var assignedSocketNumbers = new HashSet<int>();
        foreach (var device in DeviceLoads)
        {
            if (device is null)
                throw new ArgumentException("Device entries cannot be null.", nameof(DeviceLoads));
            if (!double.IsFinite(device.PowerDemandKW) || device.PowerDemandKW <= 0)
                throw new ArgumentOutOfRangeException(nameof(DeviceLoads), "Device power demand must be positive and finite.");
            if (device.SocketNumber < 0 ||
                (device.SocketNumber > 0 && !assignedSocketNumbers.Add(device.SocketNumber)))
                throw new ArgumentException("Specified device socket numbers must be positive and unique.", nameof(DeviceLoads));
        }
        if (!double.IsFinite(TotalDeviceDemandKW))
            throw new ArgumentOutOfRangeException(nameof(DeviceLoads), "Total device power demand must be finite.");
        if (TotalDeviceDemandKW > MaximumTotalDeviceDemandKW)
            throw new ArgumentOutOfRangeException(nameof(DeviceLoads), $"Total device power demand cannot exceed {MaximumTotalDeviceDemandKW} kW.");
        if (string.IsNullOrWhiteSpace(CsvPath))
            throw new ArgumentException("A CSV output path is required.", nameof(CsvPath));
        if (ModbusPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(ModbusPort), "Modbus TCP port must be between 1 and 65535.");
        if (ModbusUnitId is < 1 or > 247)
            throw new ArgumentOutOfRangeException(nameof(ModbusUnitId), "Modbus unit ID must be between 1 and 247.");
    }
}
