using Battery.Simulation;
using TeamConsoleShared;

namespace Battery
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            SimulationTheme.InitializeConsole();

            var options = new SimulationOptions
            {
                CapacityKWh = 200,
                MaximumChargePowerKW = 50,
                MaximumDischargePowerKW = 50,
                InitialStateOfChargePercent = 0,
                StepSize = TimeSpan.FromMinutes(15),
                StartTime = DateTime.Today,
                Duration = SimulationOptions.DefaultDuration,
                CsvPath = Path.Combine("data", "battery-run.csv"),
                ModbusPort = GetPortFromEnvironment("BATTERY_MODBUS_PORT", 1502)
            };

            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                // The BatterySimulator runs the connection retry loop in the background. This allows its
                // dashboard and local fallback clock to remain active while the master is unavailable.
                var masterHost = Environment.GetEnvironmentVariable("SIMULATION_MASTER_HOST") ?? "127.0.0.1";
                var masterPort = GetPortFromEnvironment("SIMULATION_MASTER_PORT", 1510);
                await using var simulationMaster = new SimulationMasterClient(masterHost, masterPort);
                await new BatterySimulator(options, simulationMaster).RunAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Simulation cancelled.");
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static int GetPortFromEnvironment(string variableName, int defaultPort)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
                return defaultPort;

            if (int.TryParse(value, out var port) && port is >= 1 and <= 65535)
                return port;

            throw new InvalidOperationException($"Environment variable {variableName} must contain a TCP port from 1 through 65535.");
        }
    }
}
