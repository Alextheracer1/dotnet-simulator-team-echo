using System;
using EasyModbus;
using PV_Simulator.Domain;
using TeamShared;

namespace PV_Simulator.Modbus;

public static class PvRegisterMap
{
    // Gerätespezifische Register ab Index 13 (CommonRegisterMap.DeviceSpecificStart)
    public const int Irradiance_High = 13;   // Float32 (High Word) in W/m²
    public const int Irradiance_Low = 14;    // Float32 (Low Word)
    public const int Temperature_High = 15;  // Float32 (High Word) in °C
    public const int Temperature_Low = 16;   // Float32 (Low Word)
    public const int CloudCover_High = 17;   // Float32 (High Word) in %
    public const int CloudCover_Low = 18;    // Float32 (Low Word)
}

/// <summary>
/// Modbus TCP Server für den PV-Simulator (LB02-Standard auf Port 1504, Unit ID 1).
/// Stellt Standard-Register (1-12) und PV-spezifische Messwerte (13-18) bereit.
/// </summary>
public sealed class PvModbusService : IDisposable
{
    private readonly FieldDeviceModbusServer _server;
    private short _operatingState = 2; // 2 = Running
    private short _errorCode = 0;

    public int Port { get; }
    public byte UnitId { get; }

    public PvModbusService(int port = 1504, byte unitId = 1)
    {
        Port = port;
        UnitId = unitId;
        _server = new FieldDeviceModbusServer(port, unitId);
    }

    public void Start() => _server.Start();
    public void Stop() => _server.Stop();

    public float ReadPowerLimitSetpoint()
    {
        var setpoint = _server.ReadSetpoint();
        return setpoint <= 0 ? float.MaxValue : setpoint;
    }

    public void Update(SimulationSnapshot snapshot)
    {
        if (_server.CheckAndClearErrorReset())
        {
            _errorCode = 0;
            _operatingState = 2;
        }

        if (snapshot.ManualFaultActive || snapshot.InverterStatus == InverterStatus.Faulted)
        {
            _operatingState = 3;
            _errorCode = 1;
        }
        else if (snapshot.InverterStatus == InverterStatus.Off)
        {
            _operatingState = 0;
            _errorCode = 0;
        }
        else
        {
            _operatingState = 2;
            _errorCode = 0;
        }

        var virtualTimeSeconds = (int)snapshot.SimulatedAt.ToUnixTimeSeconds();
        var powerWatts = (float)(snapshot.PowerKilowatts * 1000.0);
        var energyKwh = (float)snapshot.DailyEnergyKilowattHours;
        short onOff = snapshot.InverterStatus != InverterStatus.Off ? (short)1 : (short)0;

        _server.UpdateCommonRegisters(
            operatingState: _operatingState,
            errorCode: _errorCode,
            activePower: powerWatts,
            cumulativeEnergy: energyKwh,
            virtualTime: virtualTimeSeconds,
            onOffState: onOff);

        WriteFloatRegister(PvRegisterMap.Irradiance_High, (float)snapshot.IrradianceWattsPerSquareMeter);
        WriteFloatRegister(PvRegisterMap.Temperature_High, (float)snapshot.Weather.AmbientTemperatureCelsius);
        WriteFloatRegister(PvRegisterMap.CloudCover_High, (float)(snapshot.Weather.CloudCoverFraction * 100.0));
    }

    private void WriteFloatRegister(int startRegister, float value)
    {
        var regs = ModbusClient.ConvertFloatToRegisters(value);
        _server.HoldingRegisters[startRegister] = (short)regs[0];
        _server.HoldingRegisters[startRegister + 1] = (short)regs[1];
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
    }
}

