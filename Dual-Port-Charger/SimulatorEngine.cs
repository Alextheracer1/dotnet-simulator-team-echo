using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WS26_Dual_Port_Charger
{
    public class SimulatorEngine
    {
        private readonly Device _device;
        private readonly TimeSpan _timeStep;
        private readonly string _logFilePath;
        private bool _isRunning = true;
        
        public bool AutoMode { get; private set; } = true;
        public DateTime SimulatedTime { get; private set; }
        public int DelayMs { get; private set; } = 1000; 

        public SimulatorEngine(Device device, TimeSpan timeStep, string logFilePath)
        {
            _device = device;
            _timeStep = timeStep;
            _logFilePath = logFilePath;
            
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

        public void Start(Action renderUi)
        {
            Console.Clear();
            var endTime = SimulatedTime.AddHours(24);

            while (_isRunning && SimulatedTime < endTime)
            {
                // Process all buffered inputs immediately to prevent lag
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    HandleUserInput(key.Key);
                }

                SimulatedTime += _timeStep;
                UpdateSimulation();
                LogData();
                renderUi();
                
                Thread.Sleep(DelayMs); 
            }
            
            Console.SetCursorPosition(0, 24);
            Console.WriteLine("Simulation beendet (24h erreicht oder manuell abgebrochen).");
        }

        private void UpdateSimulation()
        {
            if (AutoMode)
            {
                _device.UpdateAutoMode(_timeStep);
            }
            else
            {
                _device.UpdateManualMode(_timeStep);
            }
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

        private void HandleUserInput(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.P:
                    while (true)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.P) break;
                        Thread.Sleep(100);
                    }
                    break;
                case ConsoleKey.A:
                    AutoMode = true;
                    break;
                case ConsoleKey.M: 
                    AutoMode = false;
                    // Fulfill requirement: Enforce the 2 Teslas to be assigned when switching to Manual
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