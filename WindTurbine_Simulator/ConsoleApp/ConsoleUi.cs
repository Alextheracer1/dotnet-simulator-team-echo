using System.Globalization;
using Spectre.Console;
using TeamConsoleShared;
using WindTurbine_Simulator.Configuration;

namespace WindTurbine_Simulator.ConsoleApp;

public sealed class ConsoleUi
{
    public SimulationOptions ReadOptions()
    {
        AnsiConsole.Write(
            new Panel(new Markup("[bold deepskyblue1]🌀 Windturbinen-Digitalzwilling[/]\n[grey62]Digitaler Zwilling für Windenergieanlagen (Vestas V90)[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(foreground: SimulationTheme.WindAccent))
                .Expand());

        AnsiConsole.Markup("[bold cyan]Standardwerte für Physik und Zeit verwenden (j/n)?[/] [grey][[j]]: [/]");
        var input = Console.ReadLine()?.Trim().ToLower();

        TimeSpan? duration = null;
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
            AnsiConsole.MarkupLine("[grey62]Standardwerte für Physik und Zeit werden geladen...[/]");
        }

        AnsiConsole.MarkupLine("\n[bold deepskyblue1]--- Netzwerkeinstellungen ---[/]");
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

    public void PrintStart(SimulationOptions options, string? csvPath = null)
    {
        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            AnsiConsole.MarkupLine($"[grey62]CSV-Protokoll:[/] [cyan]{Markup.Escape(csvPath)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey62]CSV-Protokoll:[/] [dim grey]Deaktiviert[/]");
        }
        AnsiConsole.MarkupLine(options.Duration.HasValue
            ? $"[green]Simulation startet[/] ([grey]{options.Duration.Value.TotalHours:0.##} h, Schritt {options.StepInterval.TotalSeconds:0.##} s, Zeitraffer x{options.SpeedMultiplier:0.##}[/])."
            : $"[green]Simulation startet[/] ([grey]unbegrenzt, Schritt {options.StepInterval.TotalSeconds:0.##} s, Zeitraffer x{options.SpeedMultiplier:0.##}[/]).");
        AnsiConsole.MarkupLine("[dim grey]Live-Dashboard wird geladen...[/]");
    }

    public void PrintCompleted()
    {
        AnsiConsole.MarkupLine("[bold green]Simulation abgeschlossen.[/]");
    }

    private static TimeSpan? ReadDuration()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Simulationsdauer in Stunden oder 'inf' für unbegrenzt [grey][[inf]][/]: [/]");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input) || string.Equals(input, "inf", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours)
                && double.IsFinite(hours)
                && hours > 0)
            {
                return TimeSpan.FromHours(hours);
            }

            AnsiConsole.MarkupLine("[yellow]Bitte eine positive Zahl oder 'inf' eingeben.[/]");
        }
    }

    private static TimeSpan ReadStepInterval()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Zeitschritt in Sekunden [grey][[300]][/]: [/]");
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

            AnsiConsole.MarkupLine("[yellow]Bitte eine positive Zahl eingeben.[/]");
        }
    }

    private static double ReadRatedPower()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Nennleistung in kW [grey][[3000]][/]: [/]");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 3000.0;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var power) && power > 0)
            {
                return power;
            }

            AnsiConsole.MarkupLine("[yellow]Bitte eine positive Zahl eingeben.[/]");
        }
    }

    private static double ReadRotorArea()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Rotorfläche in m² [grey][[8000]][/]: [/]");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 8000.0;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var area) && area > 0)
            {
                return area;
            }

            AnsiConsole.MarkupLine("[yellow]Bitte eine positive Zahl eingeben.[/]");
        }
    }

    private static int ReadModbusPort()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Modbus TCP Port [grey][[1505]][/]: [/]");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 1505;
            }

            if (int.TryParse(input, out var port) && port >= 1024 && port <= 65535)
            {
                return port;
            }

            AnsiConsole.MarkupLine("[yellow]Bitte einen gültigen Port zwischen 1024 und 65535 eingeben.[/]");
        }
    }

    private static byte ReadModbusUnitId()
    {
        while (true)
        {
            AnsiConsole.Markup("[bold grey85]Modbus Unit-ID [grey][[1]][/]: [/]");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return 1;
            }

            if (byte.TryParse(input, out var unitId) && unitId >= 1 && unitId <= 247)
            {
                return unitId;
            }

            AnsiConsole.MarkupLine("[yellow]Bitte eine gültige Unit-ID zwischen 1 und 247 eingeben.[/]");
        }
    }
}
