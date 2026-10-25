using System;
using System.IO;

namespace WS26_Dual_Port_Charger
{
    class Program
    {
        static void Main(string[] args)
        {
            var timeStep = TimeSpan.FromMinutes(1); 
            var logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "simulation.csv");
            
            var device = new Device();
            var simulator = new SimulatorEngine(device, timeStep, logFilePath);
            var ui = new ConsoleUI(device, simulator);
            
            // Start simulation and pass the render method to be called every tick
            simulator.Start(ui.Render);
        }
    }
}