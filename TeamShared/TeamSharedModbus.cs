using System;
using EasyModbus;

//namespace TeamShared.Modbus

namespace Modbus_Server
{
    /// <summary>
    /// Defines the common register layout required for all field devices according to LB02.
    /// </summary>
    public static class CommonRegisterMap
    {
        public const int OperatingState = 1;      // Int16 (0=Off, 1=Booting, 2=Running, 3=Error)
        public const int ErrorCode = 2;           // Int16 (0=None, >0=Error)
        public const int ActivePower_High = 3;    // Float32 (High Word)
        public const int ActivePower_Low = 4;     // Float32 (Low Word)
        public const int CumulativeEnergy_High = 5;// Float32 (High Word)
        public const int CumulativeEnergy_Low = 6; // Float32 (Low Word)
        public const int VirtualTime_Low = 7;     // Int32 (Low Word)
        public const int VirtualTime_High = 8;    // Int32 (High Word)
        public const int OnOffState = 9;          // Int16 (0=Off, 1=On)
        public const int Setpoint_High = 10;      // Float32 (High Word)
        public const int Setpoint_Low = 11;       // Float32 (Low Word)
        public const int ResetError = 12;         // Int16 (Write 1 to reset)
        
        // Device specific registers start from 13
        public const int DeviceSpecificStart = 13;
    }

    /// <summary>
    /// Shared Modbus Server wrapper for all field devices (PV, Battery, Charger, etc.)
    /// </summary>
    public class FieldDeviceModbusServer
    {
        private readonly ModbusServer _server;

        public FieldDeviceModbusServer(int port, byte unitId)
        {
            _server = new ModbusServer();
            _server.Port = port;
            _server.UnitIdentifier = unitId;
        }

        public void Start() => _server.Listen();
        public void Stop() => _server.StopListening();

        /// <summary>
        /// Updates the mandatory common register head.
        /// Call this continuously in your simulator's main loop.
        /// </summary>
        public void UpdateCommonRegisters(short operatingState, short errorCode, float activePower, float cumulativeEnergy, int virtualTime, short onOffState)
        {
            _server.holdingRegisters[CommonRegisterMap.OperatingState] = operatingState;
            _server.holdingRegisters[CommonRegisterMap.ErrorCode] = errorCode;

            int[] powerRegs = ModbusClient.ConvertFloatToRegisters(activePower);
            _server.holdingRegisters[CommonRegisterMap.ActivePower_High] = (short)powerRegs[0];
            _server.holdingRegisters[CommonRegisterMap.ActivePower_Low] = (short)powerRegs[1];

            int[] energyRegs = ModbusClient.ConvertFloatToRegisters(cumulativeEnergy);
            _server.holdingRegisters[CommonRegisterMap.CumulativeEnergy_High] = (short)energyRegs[0];
            _server.holdingRegisters[CommonRegisterMap.CumulativeEnergy_Low] = (short)energyRegs[1];

            _server.holdingRegisters[CommonRegisterMap.VirtualTime_Low] = (short)(virtualTime & 0xFFFF);
            _server.holdingRegisters[CommonRegisterMap.VirtualTime_High] = (short)((virtualTime >> 16) & 0xFFFF);

            _server.holdingRegisters[CommonRegisterMap.OnOffState] = onOffState;
        }

        /// <summary>
        /// Checks if a client (e.g. Diagnose-Client) has requested an error reset.
        /// </summary>
        public bool CheckAndClearErrorReset()
        {
            if (_server.holdingRegisters[CommonRegisterMap.ResetError] == 1)
            {
                _server.holdingRegisters[CommonRegisterMap.ResetError] = 0; // Acknowledge and clear
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the currently configured setpoint written by the client.
        /// </summary>
        public float ReadSetpoint()
        {
            int high = _server.holdingRegisters[CommonRegisterMap.Setpoint_High];
            int low = _server.holdingRegisters[CommonRegisterMap.Setpoint_Low];
            return ModbusClient.ConvertRegistersToFloat(new int[] { high, low });
        }

        /// <summary>
        /// Exposes the internal register collection so devices can write their specific values starting at index 13.
        /// </summary>
        public ModbusServer.HoldingRegisters HoldingRegisters => _server.holdingRegisters;
    }
}
