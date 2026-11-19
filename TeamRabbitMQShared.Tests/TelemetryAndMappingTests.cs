using System;
using System.Text.Json;
using EasyModbus;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Helpers;
using TeamRabbitMQShared.Models;
using TeamShared;
using Xunit;

namespace TeamRabbitMQShared.Tests;

public class TelemetryAndMappingTests
{
    [Fact]
    public void DeviceTelemetryMessage_SerializationAndDeserialization_MaintainsDataIntegrity()
    {
        // Arrange
        var telemetry = new DeviceTelemetryMessage
        {
            DeviceId = "charger-1",
            DeviceType = "charger",
            Timestamp = DateTime.UtcNow,
            VirtualTime = 1752537600,
            OperatingState = 2,
            ErrorCode = 0,
            ActivePowerWatts = 125500.5,
            CumulativeEnergyKwh = 432.1,
            IsOn = true,
            ModbusPort = 1502,
            ModbusUnitId = 1
        };
        telemetry.SetMetric("SOC1", 0.45);
        telemetry.SetMetric("SOC2", 0.85);

        // Act
        var json = JsonSerializer.Serialize(telemetry);
        var deserialized = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("charger-1", deserialized.DeviceId);
        Assert.Equal("charger", deserialized.DeviceType);
        Assert.Equal(2, deserialized.OperatingState);
        Assert.Equal("Running", deserialized.OperatingStateDescription);
        Assert.Equal(125500.5, deserialized.ActivePowerWatts);
        Assert.Equal(125.5005, deserialized.ActivePowerKw, 4);
        Assert.Equal(432.1, deserialized.CumulativeEnergyKwh);
        Assert.Equal(0.45, deserialized.GetMetric("SOC1"));
        Assert.Equal(0.85, deserialized.GetMetric("SOC2"));
    }

    [Fact]
    public void DeviceCommandMessage_FactoryMethods_ProduceExpectedProperties()
    {
        // Act
        var setLimit = DeviceCommandMessage.CreateSetPowerLimit("charger-1", 200000);
        var resetFault = DeviceCommandMessage.CreateResetFault("charger-1");
        var toggleCar = DeviceCommandMessage.CreateToggleCar("charger-1", 1);

        // Assert
        Assert.Equal("SetPowerLimit", setLimit.CommandType);
        Assert.Equal(200000, setLimit.Value);
        Assert.Equal("charger-1", setLimit.TargetDeviceId);

        Assert.Equal("ResetFault", resetFault.CommandType);
        Assert.Equal(1.0, resetFault.Value);

        Assert.Equal("ToggleCar", toggleCar.CommandType);
        Assert.Equal(1, toggleCar.Value);
    }

    [Fact]
    public void RabbitMqConfiguration_RoutingKeyBuilders_FormatTopicsCorrectly()
    {
        // Arrange
        var config = new RabbitMqConfiguration();

        // Act
        var telemetryKey = config.BuildTelemetryRoutingKey("charger", "charger-1");
        var commandKey = config.BuildCommandRoutingKey("charger", "charger-1");

        // Assert
        Assert.Equal("sim.telemetry.charger.charger-1", telemetryKey);
        Assert.Equal("sim.commands.charger.charger-1", commandKey);
    }

    [Fact]
    public void ModbusTelemetryMapper_FromRegisters_MapsStandardAndDeviceSpecificRegisters()
    {
        // Arrange: Prepare dummy registers matching CommonRegisterMap
        var registers = new int[25];
        registers[CommonRegisterMap.OperatingState] = 2; // Running
        registers[CommonRegisterMap.ErrorCode] = 0;

        int[] powerRegs = ModbusClient.ConvertFloatToRegisters(150000.0f);
        registers[CommonRegisterMap.ActivePower_High] = powerRegs[0];
        registers[CommonRegisterMap.ActivePower_Low] = powerRegs[1];

        int[] energyRegs = ModbusClient.ConvertFloatToRegisters(520.5f);
        registers[CommonRegisterMap.CumulativeEnergy_High] = energyRegs[0];
        registers[CommonRegisterMap.CumulativeEnergy_Low] = energyRegs[1];

        int virtualTime = 1752537600;
        registers[CommonRegisterMap.VirtualTime_Low] = (short)(virtualTime & 0xFFFF);
        registers[CommonRegisterMap.VirtualTime_High] = (short)((virtualTime >> 16) & 0xFFFF);

        registers[CommonRegisterMap.OnOffState] = 1;

        // Charger specific SOC registers
        int[] soc1Regs = ModbusClient.ConvertFloatToRegisters(0.55f);
        int[] soc2Regs = ModbusClient.ConvertFloatToRegisters(0.75f);
        registers[CommonRegisterMap.DeviceSpecificStart] = soc1Regs[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 1] = soc1Regs[1];
        registers[CommonRegisterMap.DeviceSpecificStart + 2] = soc2Regs[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 3] = soc2Regs[1];

        // Act
        var telemetry = ModbusTelemetryMapper.FromRegisters("charger-1", "charger", registers, 1502, 1);

        // Assert
        Assert.Equal("charger-1", telemetry.DeviceId);
        Assert.Equal("charger", telemetry.DeviceType);
        Assert.Equal(2, telemetry.OperatingState);
        Assert.Equal(150000.0f, (float)telemetry.ActivePowerWatts, 1f);
        Assert.Equal(520.5f, (float)telemetry.CumulativeEnergyKwh, 0.1f);
        Assert.True(telemetry.IsOn);
        Assert.Equal(0.55f, (float)telemetry.GetMetric("SOC1"), 0.01f);
        Assert.Equal(0.75f, (float)telemetry.GetMetric("SOC2"), 0.01f);
    }

    [Fact]
    public void ModbusTelemetryMapper_FromRegisters_MapsPvSpecificRegisters()
    {
        var registers = new int[25];
        registers[CommonRegisterMap.OperatingState] = 2;
        int[] powerRegs = ModbusClient.ConvertFloatToRegisters(45000.0f);
        registers[CommonRegisterMap.ActivePower_High] = powerRegs[0];
        registers[CommonRegisterMap.ActivePower_Low] = powerRegs[1];

        // PV: Irradiance (13-14), Temperature (15-16), CloudCover (17-18)
        int[] irr = ModbusClient.ConvertFloatToRegisters(850.0f);
        int[] temp = ModbusClient.ConvertFloatToRegisters(24.5f);
        int[] cloud = ModbusClient.ConvertFloatToRegisters(15.0f);
        registers[CommonRegisterMap.DeviceSpecificStart] = irr[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 1] = irr[1];
        registers[CommonRegisterMap.DeviceSpecificStart + 2] = temp[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 3] = temp[1];
        registers[CommonRegisterMap.DeviceSpecificStart + 4] = cloud[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 5] = cloud[1];

        var telemetry = ModbusTelemetryMapper.FromRegisters("pv-1", "pv", registers, 1504, 1);

        Assert.Equal("pv-1", telemetry.DeviceId);
        Assert.Equal("pv", telemetry.DeviceType);
        Assert.Equal(45.0, telemetry.ActivePowerKw, 1);
        Assert.Equal(850.0f, (float)telemetry.GetMetric("Irradiance"), 0.1f);
        Assert.Equal(24.5f, (float)telemetry.GetMetric("Temperature"), 0.1f);
        Assert.Equal(15.0f, (float)telemetry.GetMetric("CloudCover"), 0.1f);
    }

    [Fact]
    public void ModbusTelemetryMapper_FromRegisters_MapsWindSpecificRegisters()
    {
        var registers = new int[25];
        registers[CommonRegisterMap.OperatingState] = 2;
        int[] powerRegs = ModbusClient.ConvertFloatToRegisters(1200000.0f);
        registers[CommonRegisterMap.ActivePower_High] = powerRegs[0];
        registers[CommonRegisterMap.ActivePower_Low] = powerRegs[1];

        // Wind: WindSpeed (13-14), RotorRpm (15-16), PitchAngle (17-18)
        int[] speed = ModbusClient.ConvertFloatToRegisters(11.5f);
        int[] rpm = ModbusClient.ConvertFloatToRegisters(17.25f);
        int[] pitch = ModbusClient.ConvertFloatToRegisters(2.0f);
        registers[CommonRegisterMap.DeviceSpecificStart] = speed[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 1] = speed[1];
        registers[CommonRegisterMap.DeviceSpecificStart + 2] = rpm[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 3] = rpm[1];
        registers[CommonRegisterMap.DeviceSpecificStart + 4] = pitch[0];
        registers[CommonRegisterMap.DeviceSpecificStart + 5] = pitch[1];

        var telemetry = ModbusTelemetryMapper.FromRegisters("wind-1", "wind", registers, 1505, 1);

        Assert.Equal("wind-1", telemetry.DeviceId);
        Assert.Equal("wind", telemetry.DeviceType);
        Assert.Equal(1200.0, telemetry.ActivePowerKw, 1);
        Assert.Equal(11.5f, (float)telemetry.GetMetric("WindSpeed"), 0.1f);
        Assert.Equal(17.25f, (float)telemetry.GetMetric("RotorRpm"), 0.1f);
        Assert.Equal(2.0f, (float)telemetry.GetMetric("PitchAngle"), 0.1f);
    }
}
