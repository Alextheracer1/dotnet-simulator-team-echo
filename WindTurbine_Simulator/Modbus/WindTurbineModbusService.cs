using EasyModbus;
using TeamShared;
using WindTurbine_Simulator.Simulation;

namespace WindTurbine_Simulator.Modbus;

public static class WindTurbineRegisterMap
{
    // Gerätespezifische Register ab Index 13 (CommonRegisterMap.DeviceSpecificStart)
    public const int WindSpeed_High = 13;   // Float32 (High Word) in m/s
    public const int WindSpeed_Low = 14;    // Float32 (Low Word)
    public const int RotorRpm_High = 15;    // Float32 (High Word) in U/min
    public const int RotorRpm_Low = 16;     // Float32 (Low Word)
    public const int PitchAngle_High = 17;  // Float32 (High Word) in Grad
    public const int PitchAngle_Low = 18;   // Float32 (Low Word)
}

public sealed class WindTurbineModbusService : IDisposable
{
    private readonly FieldDeviceModbusServer _server;
    private short _operatingState = 2; // 2 = Running
    private short _errorCode = 0;

    public WindTurbineModbusService(int port, byte unitId)
    {
        _server = new FieldDeviceModbusServer(port, unitId);
    }

    public void Start() => _server.Start();
    public void Stop() => _server.Stop();

    public float ReadPowerLimitSetpoint()
    {
        // Liest den vom Diagnose-Client geschriebenen Leistungssollwert
        var setpoint = _server.ReadSetpoint();
        return setpoint <= 0 ? float.MaxValue : setpoint;
    }

    public void Update(SimulationSnapshot snapshot, double rotorRpm, double pitchAngle)
    {
        if (_server.CheckAndClearErrorReset())
        {
            _errorCode = 0;
            _operatingState = 2;
        }

        // Virtuelle Zeit als Unix-Timestamp (Sekunden) für Int32
        var virtualTimeSeconds = (int)snapshot.SimulatedAt.ToUnixTimeSeconds();

        // Gemeinsamen Registerkopf aktualisieren
        _server.UpdateCommonRegisters(
            operatingState: _operatingState,
            errorCode: _errorCode,
            activePower: (float)snapshot.ActivePowerKw,
            cumulativeEnergy: (float)snapshot.CumulativeEnergyKwh,
            virtualTime: virtualTimeSeconds,
            onOffState: 1);

        // Gerätespezifische Register aktualisieren
        WriteFloatRegister(WindTurbineRegisterMap.WindSpeed_High, (float)snapshot.WindSpeedMps);
        WriteFloatRegister(WindTurbineRegisterMap.RotorRpm_High, (float)rotorRpm);
        WriteFloatRegister(WindTurbineRegisterMap.PitchAngle_High, (float)pitchAngle);
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