using System.Globalization;

namespace PV_Simulator.ConsoleApp;

public sealed class SimulationDateSelector
{
    public DateTimeOffset ReadStartTime()
    {
        var year = DateTime.Today.Year;
        Console.WriteLine("Datum der 24-Stunden-Simulation wählen:");
        Console.WriteLine("1 = eigenes Datum eingeben");
        Console.WriteLine($"2 = Frühling ({new DateOnly(year, 3, 20):dd.MM.yyyy})");
        Console.WriteLine($"3 = Sommer ({new DateOnly(year, 6, 21):dd.MM.yyyy})");
        Console.WriteLine($"4 = Herbst ({new DateOnly(year, 9, 22):dd.MM.yyyy})");
        Console.WriteLine($"5 = Winter ({new DateOnly(year, 12, 21):dd.MM.yyyy})");

        while (true)
        {
            Console.Write("Auswahl: ");
            var choice = Console.ReadLine()?.Trim();
            DateOnly date;

            switch (choice)
            {
                case "1":
                    date = ReadManualDate();
                    break;
                case "2":
                    date = new DateOnly(year, 3, 20);
                    break;
                case "3":
                    date = new DateOnly(year, 6, 21);
                    break;
                case "4":
                    date = new DateOnly(year, 9, 22);
                    break;
                case "5":
                    date = new DateOnly(year, 12, 21);
                    break;
                default:
                    Console.WriteLine("Bitte eine Auswahl von 1 bis 5 eingeben.");
                    continue;
            }

            return AtLocalMidnight(date);
        }
    }

    private static DateOnly ReadManualDate()
    {
        while (true)
        {
            Console.Write("Simulationsdatum (z. B. 15.04.2026): ");
            if (DateOnly.TryParse(Console.ReadLine(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            Console.WriteLine("Bitte ein gültiges Datum eingeben.");
        }
    }

    private static DateTimeOffset AtLocalMidnight(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localMidnight, TimeZoneInfo.Local.GetUtcOffset(localMidnight));
    }
}
