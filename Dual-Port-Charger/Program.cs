using System;
using System.IO;
using System.Threading.Tasks;

namespace Dual_Port_Charger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var timeStep = TimeSpan.FromMinutes(1); 
            var logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "simulation.csv");
            
            var device = new Device();
            var simulator = new SimulatorEngine(device, timeStep, logFilePath);
            var ui = new ConsoleUI(device, simulator);
            
            await simulator.StartAsync(ui.Render);
        }
    }
}