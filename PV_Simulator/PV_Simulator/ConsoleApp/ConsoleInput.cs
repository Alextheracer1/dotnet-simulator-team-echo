using System.Globalization;
using PV_Simulator.Domain;

namespace PV_Simulator.ConsoleApp;

public sealed class ConsoleInput
{
    public SimulationConfiguration ReadConfiguration()
    {
        Console.WriteLine("Startprofil wählen: 1 = Standardwerte, 2 = Parameter manuell eingeben (Enter = Standardwerte)");
        SimulationConfiguration configuration;
        while (true)
        {
            Console.Write("Auswahl: ");
            var choice = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(choice) || choice == "1")
            {
                var pvModulePower = ReadRequiredDouble("PV-Modul-Nennleistung in kWp", 0.01, 1_000_000);
                var inverterPower = ReadRequiredDouble("Maximale Wechselrichter-Ausgangsleistung in kW", 0.01, 1_000_000);
                configuration = SimulationDefaults.Create(pvModulePower, inverterPower);
                break;
            }

            if (choice == "2")
            {
                configuration = ReadManualConfiguration();
                break;
            }

            Console.WriteLine("Bitte 1 oder 2 auswählen.");
        }

        var startTime = new SimulationDateSelector().ReadStartTime();
        var settings = new SimulationSettings(
            startTime,
            configuration.Settings.TimeStep,
            configuration.Settings.SpeedMultiplier);
        var weatherSettings = ReadWeatherSettings();
        configuration = new SimulationConfiguration(
            configuration.Location,
            configuration.Array,
            configuration.Inverter,
            settings,
            weatherSettings);

        DisplayConfiguration(configuration);
        return configuration;
    }

    private static SimulationConfiguration ReadManualConfiguration()
    {
        var defaultLocation = SimulationDefaults.CreateLocation();
        var defaultSettings = SimulationDefaults.CreateSettings();
        var name = ReadText("Standortname", defaultLocation.Name);
        var latitude = ReadDouble("Breitengrad (-90 bis 90)", -90, 90, defaultLocation.Latitude);
        var longitude = ReadDouble("Längengrad (-180 bis 180)", -180, 180, defaultLocation.Longitude);
        var elevation = ReadDouble("Höhe über Meer in Metern", -500, 9000, defaultLocation.ElevationMeters);
        var deviceId = ReadRequiredText("Eindeutige Geräte-ID der PV-Anlage");
        var peakPower = ReadRequiredDouble("PV-Modul-Nennleistung in kWp", 0.01, 1_000_000);
        var tilt = ReadDouble("Modulneigung in Grad (0 bis 90)", 0, 90, SimulationDefaults.PvArrayTiltDegrees);
        var azimuth = ReadDouble("Modul-Azimut in Grad (0 = Norden, 90 = Osten, 180 = Süden)", 0, 359.999, SimulationDefaults.PvArrayAzimuthDegrees);
        var inverterPower = ReadRequiredDouble("Maximale Wechselrichter-Ausgangsleistung in kW", 0.01, 1_000_000);
        var failureProbability = ReadDouble("Wechselrichter-Ausfallwahrscheinlichkeit in % pro Stunde", 0, 100, SimulationDefaults.InverterFailureProbabilityPerHour * 100) / 100;
        var repairMinutes = ReadDouble("Mittlere Reparaturdauer in Minuten", double.Epsilon, 1_000_000, SimulationDefaults.InverterRepairDuration.TotalMinutes);
        var stepMinutes = ReadDouble("Simulationsschritt in Minuten", double.Epsilon, 1440, SimulationDefaults.SimulationTimeStep.TotalMinutes);
        var speed = ReadDouble("Anfängliche Zeitraffer-Geschwindigkeit (simulierte Sekunden pro Echtsekunde)", double.Epsilon, 1_000_000, SimulationDefaults.SimulationSpeedMultiplier);
        return new SimulationConfiguration(
            new Location(name, latitude, longitude, elevation),
            new PvArray(deviceId, peakPower, tilt, azimuth),
            new Inverter(inverterPower, failureProbability, TimeSpan.FromMinutes(repairMinutes)),
            new SimulationSettings(defaultSettings.StartTime, TimeSpan.FromMinutes(stepMinutes), speed),
            new WeatherSettings(null));
    }

    private static WeatherSettings ReadWeatherSettings()
    {
        while (true)
        {
            Console.Write("Wetter-Zufallsseed [Enter = zufällig]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return new WeatherSettings(null);
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out var seed))
            {
                return new WeatherSettings(seed);
            }

            Console.WriteLine("Bitte eine ganze Zahl eingeben oder Enter für einen zufälligen Verlauf drücken.");
        }
    }

    private static string ReadRequiredText(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            var value = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            Console.WriteLine("Dieses Feld ist erforderlich.");
        }
    }

    private static double ReadRequiredDouble(string prompt, double minimum, double maximum)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            if (double.TryParse(Console.ReadLine(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
                && double.IsFinite(value)
                && value >= minimum
                && value <= maximum)
            {
                return value;
            }

            Console.WriteLine($"Bitte eine Zahl zwischen {minimum} und {maximum} eingeben.");
        }
    }

    private static string ReadText(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim();
        }
    }

    private static double ReadDouble(string prompt, double minimum, double maximum, double defaultValue)
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
                && double.IsFinite(value)
                && value >= minimum
                && value <= maximum)
            {
                return value;
            }

            Console.WriteLine($"Bitte eine Zahl zwischen {minimum} und {maximum} eingeben.");
        }
    }

    private static void DisplayConfiguration(SimulationConfiguration configuration)
    {
        Console.WriteLine("Finale Konfiguration vor Simulationsstart:");
        Console.WriteLine($"Standort: {configuration.Location.Name}, {configuration.Location.Latitude}°, {configuration.Location.Longitude}°, {configuration.Location.ElevationMeters} m");
        Console.WriteLine($"PV-Anlage {configuration.Array.DeviceId}: Modul-Nennleistung {configuration.Array.ModuleNominalPowerKilowattsPeak} kWp, Neigung {configuration.Array.TiltDegrees}°, Azimut {configuration.Array.AzimuthDegrees}°");
        Console.WriteLine($"Wechselrichter: maximale Ausgangsleistung {configuration.Inverter.RatedPowerKilowatts} kW, Ausfallwahrscheinlichkeit {configuration.Inverter.FailureProbabilityPerHour:P0}/h, Reparatur {configuration.Inverter.RepairDuration.TotalMinutes:0} min");
        Console.WriteLine($"Simulationsdatum: {configuration.Settings.StartTime:dd.MM.yyyy}, Startzeit: {configuration.Settings.StartTime:HH:mm} Uhr Ortszeit");
        Console.WriteLine($"Simulationsschritt: {configuration.Settings.TimeStep.TotalMinutes:0} min, Zeitraffer: {configuration.Settings.SpeedMultiplier}x");
        Console.WriteLine($"Wetterverlauf: automatisch für Datum und Standort | Seed: {configuration.WeatherSettings.RandomSeed?.ToString(CultureInfo.CurrentCulture) ?? "zufällig"}");
    }

}