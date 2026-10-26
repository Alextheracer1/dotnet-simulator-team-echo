using System;
using PV_Simulator.Domain;

namespace PV_Simulator.ConsoleApp;

public sealed class ConsoleStatusRenderer
{
    private const int BoxWidth = 84;

    public void Render(
        SimulationSnapshot snapshot,
        Location location,
        PvArray array,
        Inverter inverter,
        SimulationSettings settings,
        TimeSpan duration,
        double speedMultiplier,
        int totalSteps,
        string selectedWeatherControl)
    {
        Console.Clear();
        WriteBoxBorder();
        WriteBoxLine($"PV-SIMULATION | {location.Name} ({location.Latitude:F4}, {location.Longitude:F4})");
        WriteBoxBorder();
        WriteBoxLine($"Zeit: {snapshot.SimulatedAt:dd.MM.yyyy HH:mm zzz}    Schritt: {snapshot.StepNumber}/{totalSteps}    Laufzeit: {duration:hh\\:mm}");
        WriteBoxLine($"Anlage: {array.DeviceId}    PV-Nennleistung: {array.ModuleNominalPowerKilowattsPeak:0.###} kWp    Wechselrichter max.: {inverter.RatedPowerKilowatts:0.###} kW");
        WriteBoxBorder();
        WriteBoxLine("LEISTUNG");
        WriteBoxLine(CreatePowerBar(snapshot.PowerKilowatts, array.ModuleNominalPowerKilowattsPeak));
        WriteBoxLine($"Tagesenergie: {snapshot.DailyEnergyKilowattHours:0.000} kWh    Einstrahlung: {snapshot.IrradianceWattsPerSquareMeter:0} W/m2");
        WriteBoxBorder();
        WriteBoxLine("ZUSTAND");
        WriteBoxLine($"Wechselrichter: {snapshot.InverterStatus}    Manuelle Stoerung: {(snapshot.ManualFaultActive ? "aktiv" : "aus")}    Module: {(snapshot.SnowCoverActive ? "verschneit" : "schneefrei")}");
        WriteBoxLine($"Bewoelkung: {snapshot.Weather.CloudCoverFraction:P0} ({(snapshot.Weather.CloudCoverManuallyOverridden ? "manuell" : "automatisch")})    Temperatur: {snapshot.Weather.AmbientTemperatureCelsius:0.0} C ({(snapshot.Weather.TemperatureManuallyOverridden ? "manuell" : "automatisch")})");
        WriteBoxLine($"Niederschlag: {snapshot.Weather.Precipitation}    Manueller Regen: {(snapshot.Weather.ManualRainActive ? "aktiv" : "aus")}    Wettersteuerung: {selectedWeatherControl}");
        WriteBoxLine($"Zeitraffer: {speedMultiplier:0.##}x");
        WriteBoxBorder();
        WriteBoxLine("TASTEN");
        WriteBoxLine("C Wolken | T Temperatur | +/- Wert oder Geschwindigkeit | A Automatik | R Regen | F Stoerung");
        WriteBoxLine("S Schnee umschalten | P Pause/Fortsetzen");
        WriteBoxBorder();
    }

    private static string CreatePowerBar(double currentPowerKilowatts, double maxPowerKilowatts, int barWidth = 28)
    {
        if (barWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barWidth));
        }

        var safeMax = maxPowerKilowatts > 0 ? maxPowerKilowatts : 1;
        var ratio = Math.Clamp(currentPowerKilowatts / safeMax, 0, 1);
        var percentage = (int)(ratio * 100);
        var filled = (int)(ratio * barWidth);
        var bar = new string('█', filled) + new string('░', barWidth - filled);

        return $"PV-Leistung: {currentPowerKilowatts,7:F2} kW [{bar}] {percentage,3}% von {maxPowerKilowatts:0.###} kWp";
    }

    private static void WriteBoxBorder() => Console.WriteLine("+" + new string('-', BoxWidth - 2) + "+");

    private static void WriteBoxLine(string text)
    {
        var contentWidth = BoxWidth - 4;
        while (text.Length > contentWidth)
        {
            var splitAt = text.LastIndexOf(' ', contentWidth);
            if (splitAt <= 0)
            {
                splitAt = contentWidth;
            }

            Console.WriteLine("| " + text[..splitAt].PadRight(contentWidth) + " |");
            text = text[splitAt..].TrimStart();
        }

        Console.WriteLine("| " + text.PadRight(contentWidth) + " |");
    }
}