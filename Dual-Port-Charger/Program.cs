using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeamConsoleShared;

namespace Dual_Port_Charger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            SimulationTheme.InitializeConsole();

            var timeStep = TimeSpan.FromMinutes(1); 
            var logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "simulation.csv");

            var masterHost = Environment.GetEnvironmentVariable("SIMULATION_MASTER_HOST") ?? "127.0.0.1";
            var masterPortText = Environment.GetEnvironmentVariable("SIMULATION_MASTER_PORT");
            var masterPort = string.IsNullOrWhiteSpace(masterPortText)
                ? 1510
                : int.TryParse(masterPortText, out var parsedPort) && parsedPort is >= 1 and <= 65535
                    ? parsedPort
                    : throw new InvalidOperationException("SIMULATION_MASTER_PORT must contain a TCP port from 1 through 65535.");
            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            await using var simulationMaster = new TeamShared.SimulationMasterClient(masterHost, masterPort, "charger-1");
            var device = new Device();
            var simulator = new SimulatorEngine(device, timeStep, logFilePath, simulationMaster);
            var ui = new ConsoleUI(device, simulator);

            try
            {
                await simulator.StartAsync(ui.Render, cancellationTokenSource.Token);
            }
            finally
            {
                cancellationTokenSource.Cancel();
                Console.CancelKeyPress -= cancelHandler;
            }
        }
    }
}
