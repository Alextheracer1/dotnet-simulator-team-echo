using System;
using EasyModbus;
using TeamShared;
using TeamRabbitMQShared.Models;

namespace TeamRabbitMQShared.Helpers;

/// <summary>
/// Facilitates bidirectional mapping between Modbus registers and RabbitMQ telemetry / command models.
/// </summary>
public static class ModbusTelemetryMapper
{
    /// <summary>
    /// Parses common registers (1-12) plus optional device-specific registers into a DeviceTelemetryMessage.
    /// </summary>
    public static DeviceTelemetryMessage FromRegisters(
        string deviceId,
        string deviceType,
        int[] holdingRegisters,
        int modbusPort = 1502,
        byte modbusUnitId = 1)
    {
        // EasyModbus indices can be 1-based depending on usage; normalize array access
        short GetReg(int address)
        {
            if (address >= 0 && address < holdingRegisters.Length)
                return (short)holdingRegisters[address];
            return 0;
        }

        float GetFloat(int highReg, int lowReg)
        {
            if (highReg < holdingRegisters.Length && lowReg < holdingRegisters.Length)
            {
                return ModbusClient.ConvertRegistersToFloat(new[] { holdingRegisters[highReg], holdingRegisters[lowReg] });
            }
            return 0f;
        }

        short opState = GetReg(CommonRegisterMap.OperatingState);
        short errCode = GetReg(CommonRegisterMap.ErrorCode);
        float activePower = GetFloat(CommonRegisterMap.ActivePower_High, CommonRegisterMap.ActivePower_Low);
        float cumulativeEnergy = GetFloat(CommonRegisterMap.CumulativeEnergy_High, CommonRegisterMap.CumulativeEnergy_Low);
        
        int vtLow = GetReg(CommonRegisterMap.VirtualTime_Low) & 0xFFFF;
        int vtHigh = GetReg(CommonRegisterMap.VirtualTime_High) & 0xFFFF;
        long virtualTime = (long)((vtHigh << 16) | vtLow);

        short onOff = GetReg(CommonRegisterMap.OnOffState);

        var msg = new DeviceTelemetryMessage
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            Timestamp = DateTime.UtcNow,
            VirtualTime = virtualTime,
            OperatingState = opState,
            ErrorCode = errCode,
            ActivePowerWatts = activePower,
            CumulativeEnergyKwh = cumulativeEnergy,
            IsOn = onOff == 1,
            ModbusPort = modbusPort,
            ModbusUnitId = modbusUnitId
        };

        // Device specific extraction
        if (deviceType.Equals("charger", StringComparison.OrdinalIgnoreCase))
        {
            if (holdingRegisters.Length > CommonRegisterMap.DeviceSpecificStart + 3)
            {
                float soc1 = GetFloat(CommonRegisterMap.DeviceSpecificStart, CommonRegisterMap.DeviceSpecificStart + 1);
                float soc2 = GetFloat(CommonRegisterMap.DeviceSpecificStart + 2, CommonRegisterMap.DeviceSpecificStart + 3);
                msg.SetMetric("SOC1", soc1);
                msg.SetMetric("SOC2", soc2);
            }
        }
        else if (deviceType.Equals("battery", StringComparison.OrdinalIgnoreCase))
        {
            if (holdingRegisters.Length > CommonRegisterMap.BatteryCurrent_Low)
            {
                float soc = GetFloat(CommonRegisterMap.BatteryStateOfCharge_High, CommonRegisterMap.BatteryStateOfCharge_Low);
                float volt = GetFloat(CommonRegisterMap.BatteryVoltage_High, CommonRegisterMap.BatteryVoltage_Low);
                float curr = GetFloat(CommonRegisterMap.BatteryCurrent_High, CommonRegisterMap.BatteryCurrent_Low);
                msg.SetMetric("BatterySOC", soc);
                msg.SetMetric("BatteryVoltage", volt);
                msg.SetMetric("BatteryCurrent", curr);
            }
        }

        return msg;
    }

    /// <summary>
    /// Writes a received RabbitMQ command to Modbus holding registers (via client).
    /// </summary>
    public static void ApplyCommandToModbusClient(ModbusClient client, DeviceCommandMessage command)
    {
        if (command.CommandType.Equals("SetPowerLimit", StringComparison.OrdinalIgnoreCase) ||
            command.CommandType.Equals("Setpoint", StringComparison.OrdinalIgnoreCase))
        {
            int[] regs = ModbusClient.ConvertFloatToRegisters((float)command.Value);
            client.WriteMultipleRegisters(CommonRegisterMap.Setpoint_High, regs);
        }
        else if (command.CommandType.Equals("ResetFault", StringComparison.OrdinalIgnoreCase))
        {
            client.WriteSingleRegister(CommonRegisterMap.ResetError, 1);
        }
    }
}
