using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TeamShared;
using EasyModbus;


namespace Dual_Port_Charger
{
    public class SimulatorEngine
    {
        private readonly Device _device;
        private readonly TimeSpan _timeStep;
        private readonly string _logFilePath;
        private bool _isRunning = true;
        
        private readonly FieldDeviceModbusServer _modbusServer;

        public bool AutoMode { get; private set; } = true;
        public DateTime SimulatedTime { get; private set; }
        public int DelayMs { get; private set; } = 1000; 

        public SimulatorEngine(Device device, TimeSpan timeStep, string logFilePath)
        {
            _device = device;
            _timeStep = timeStep;
            _logFilePath = logFilePath;
            _modbusServer = new FieldDeviceModbusServer(1502, 1);
            
            SimulatedTime = new DateTime(2026, 7, 15, 0, 0, 0); 
    
            var logsDirectory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(logsDirectory) && !Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
    
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, "Zeitstempel,StationMaxLeistung,Wirkleistung,Energie,SOC1,SOC2\n");
            }
        }

        public async Task StartAsync(Action renderUi)
        {
            Console.Clear();
            var endTime = SimulatedTime.AddHours(24);

            _modbusServer.Start();

            try 
            {
                while (_isRunning && SimulatedTime < endTime)
                {
                    while (!Console.IsInputRedirected && Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        await HandleUserInputAsync(key.Key);
                    }

                    if (_modbusServer.CheckAndClearErrorReset())
                    {
                        _device.ResetFault();
                    }

                    var setpoint = _modbusServer.ReadSetpoint();
                    if (setpoint > 0)
                    {
                        _device.SetPowerLimit((decimal)setpoint);
                    }

                    SimulatedTime += _timeStep;
                    UpdateSimulation();
                    UpdateModbusRegisters();
                    LogData();
                    renderUi();
                    
                    await Task.Delay(DelayMs); 
                }
            }
            finally 
            {
                _modbusServer.Stop();
            }
            
            Console.SetCursorPosition(0, 24);
            Console.WriteLine("Simulation beendet (24h erreicht oder manuell abgebrochen).");
        }

        private void UpdateModbusRegisters()
        {
            short opState = (short)(_device.Fault ? 3 : 2);
            short errCode = (short)(_device.Fault ? 1 : 0);
            int virtualTime = (int)((DateTimeOffset)SimulatedTime).ToUnixTimeSeconds();

            _modbusServer.UpdateCommonRegisters(
                opState, 
                errCode, 
                (float)_device.ActivePower, 
                (float)_device.Energy, 
                virtualTime, 
                1
            );

            // Gerätespezifische Register: SOC beider Ladepunkte
            var regs = _modbusServer.HoldingRegisters;
            int[] soc1 = ModbusClient.ConvertFloatToRegisters((float)_device.ChargingPoints[0].SOC);
            int[] soc2 = ModbusClient.ConvertFloatToRegisters((float)_device.ChargingPoints[1].SOC);

            regs[CommonRegisterMap.DeviceSpecificStart] = (short)soc1[0];
            regs[CommonRegisterMap.DeviceSpecificStart + 1] = (short)soc1[1];
            regs[CommonRegisterMap.DeviceSpecificStart + 2] = (short)soc2[0];
            regs[CommonRegisterMap.DeviceSpecificStart + 3] = (short)soc2[1];
        }

        private void UpdateSimulation()
        {
            if (AutoMode) _device.UpdateAutoMode(_timeStep);
            else _device.UpdateManualMode(_timeStep);
        }

        private void LogData()
        {
            var data = new List<string>
            {
                SimulatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                _device.Power.ToString("F1"),
                _device.ActivePower.ToString("F1"),
                _device.Energy.ToString("F1"),
                _device.ChargingPoints[0].SOC.ToString("F2"),
                _device.ChargingPoints[1].SOC.ToString("F2")
            };
            File.AppendAllLines(_logFilePath, new[] { string.Join(",", data) });
        }

        private async Task HandleUserInputAsync(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.P:
                    while (true)
                    {
                        if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.P) break;
                        await Task.Delay(100);
                    }
                    break;
                case ConsoleKey.A:
                    AutoMode = true;
                    break;
                case ConsoleKey.M: 
                    AutoMode = false;
                    _device.EnforceManualModeCars();
                    break;
                case ConsoleKey.F:
                    _device.TriggerFault();
                    break;
                case ConsoleKey.R:
                    _device.ResetFault();
                    break;
                case ConsoleKey.UpArrow:
                    if (!AutoMode) _device.AdjustManualPower(5000); 
                    break;
                case ConsoleKey.DownArrow:
                    if (!AutoMode) _device.AdjustManualPower(-5000);
                    break;
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    if (!AutoMode) _device.ToggleCar(1);
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    if (!AutoMode) _device.ToggleCar(2);
                    break;
                case ConsoleKey.Add:
                case ConsoleKey.OemPlus:
                    DelayMs = Math.Max(10, DelayMs - 20); 
                    break;
                case ConsoleKey.Subtract:
                case ConsoleKey.OemMinus:
                    DelayMs = Math.Min(1000, DelayMs + 20); 
                    break;
                case ConsoleKey.X: 
                    _isRunning = false;
                    break;
            }
        }
    }
}