using System.Globalization;
using WindTurbine_Simulator.Configuration;

namespace WindTurbine_Simulator.ConsoleApp;

public sealed class ConsoleUi
{
    public SimulationOptions ReadOptions()
    {
        Console.WriteLine("Konfiguration des Windturbinen-Digitalzwillings");
        Console.Write("Standardwerte für Physik und Zeit verwenden (j/n)? [j]: ");

        var input = Console.ReadLine()?.Trim().ToLower();

        // Lokale Variablen mit den Standardwerten vorbelegen
        TimeSpan? duration = TimeSpan.FromHours(24);
        TimeSpan stepInterval = TimeSpan.FromSeconds(300);
        double ratedPower = 3000.0;
        double rotorArea = 8000.0;

        if (input == "n")
        {
            duration = ReadDuration();
            stepInterval = ReadStepInterval();
            ratedPower = ReadRatedPower();
            rotorArea = ReadRotorArea();
        }
        else
        {
            Console.WriteLine("Standardwerte für Physik und Zeit werden geladen...");
        }

        // Netzwerkparameter werden nun IMMER abgefragt:
        Console.WriteLine("\n--- Netzwerkeinstellungen ---");
        var modbusPort = ReadModbusPort();
        var modbusUnitId = ReadModbusUnitId();

        return new SimulationOptions
        {
            Duration = duration,
            StepInterval = stepInterval,
            RatedPowerKw = ratedPower,
            RotorAreaM2 = rotorArea,
            ModbusPort = modbusPort,
            ModbusUnitId = modbusUnitId
        };
    }

    public void PrintStart(SimulationOptions options, string csvPath)
    {
        Console.WriteLine();
        Console.WriteLine($"CSV-Protokoll: {csvPath}");
        Console.WriteLine(options.Duration.HasValue
            ? $"Simulation startet ({options.Duration.Value.TotalHours:0.##} h, Schrittweite {options.StepInterval.TotalSeconds:0.##} s, Zeitraffer x{options.SpeedMultiplier:0.##})."
            : $"Simulation startet (unbegrenzt, Schrittweite {options.StepInterval.TotalSeconds:0.##} s, Zeitraffer x{options.SpeedMultiplier:0.##}). Abbruch mit Ctrl+C.");
        Console.WriteLine("Live-Ausgabe startet...");
    }

    public void PrintCompleted()
    {
        Console.WriteLine("Simulation abgeschlossen.");
    }

    private static TimeSpan? ReadDuration()
    {
        while (true)
        {
            Console.Write("Simulationsdauer in Stunden [24] oder 'inf' für unbegrenzt: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return TimeSpan.FromHours(24);
            }

            if (string.Equals(input, "inf", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours)
                && double.IsFinite(hours)
                && hours > 0)
            {
                return TimeSpan.FromHours(hours);
            }

            Console.WriteLine("Bitte eine positive Zahl oder 'inf' eingeben.");
        }
    }

    private static TimeSpan ReadStepInterval()
    {
        while (true)
        {
            Console.Write("Zeitschritt in Sekunden [300]: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return TimeSpan.FromSeconds(300);
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var seconds)
                && double.IsFinite(seconds)
                && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            Console.WriteLine("Bitte eine positive Zahl eingeben.");
        }
    }

    private static double ReadRatedPower()
    {
        while (true)
        {
            Console.Write("Nennleistung in kW [3000]: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 3000.0;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var power) && power > 0)
            {
                return power;
            }

            Console.WriteLine("Bitte eine positive Zahl eingeben.");
        }
    }

    private static double ReadRotorArea()
    {
        while (true)
        {
            Console.Write("Rotorfläche in m² [8000]: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 8000.0;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var area) && area > 0)
            {
                return area;
            }

            Console.WriteLine("Bitte eine positive Zahl eingeben.");
        }
    }

    private static int ReadModbusPort()
    {
        while (true)
        {
            Console.Write("Modbus TCP Port (unprivilegiert, z.B. ab 1502) [1504]: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 1504;
            }

            if (int.TryParse(input, out var port) && port >= 1024 && port <= 65535)
            {
                return port;
            }

            Console.WriteLine("Bitte einen gültigen Port zwischen 1024 und 65535 eingeben.");
        }
    }

    private static byte ReadModbusUnitId()
    {
        while (true)
        {
            Console.Write("Modbus Unit-ID (Gerätekennung 1-247) [1]: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 1;
            }

            if (byte.TryParse(input, out var unitId) && unitId >= 1 && unitId <= 247)
            {
                return unitId;
            }

            Console.WriteLine("Bitte eine gültige Unit-ID zwischen 1 und 247 eingeben.");
        }
    }
}