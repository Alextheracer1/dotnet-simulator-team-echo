using System;
using PV_Simulator.Domain;
using TeamConsoleShared;

namespace PV_Simulator.ConsoleApp;

public sealed class ConsoleStatusRenderer
{
    private readonly ConsoleStatusBox _box = new(84);

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
        _box.WriteBorder();
        _box.WriteLine($"PV-SIMULATION | {location.Name} ({location.Latitude:F4}, {location.Longitude:F4})");
        _box.WriteBorder();
        _box.WriteLine($"Zeit: {snapshot.SimulatedAt:dd.MM.yyyy HH:mm zzz}    Schritt: {snapshot.StepNumber}/{totalSteps}    Laufzeit: {duration:hh\\:mm}");
        _box.WriteLine($"Anlage: {array.DeviceId}    PV-Nennleistung: {array.ModuleNominalPowerKilowattsPeak:0.###} kWp    Wechselrichter max.: {inverter.RatedPowerKilowatts:0.###} kW");
        _box.WriteBorder();
        _box.WriteLine("LEISTUNG");
        _box.WriteLine(PowerBarFormatter.CreatePowerBar("PV-Leistung", snapshot.PowerKilowatts, array.ModuleNominalPowerKilowattsPeak, "kWp"));
        _box.WriteLine($"Tagesenergie: {snapshot.DailyEnergyKilowattHours:0.000} kWh    Einstrahlung: {snapshot.IrradianceWattsPerSquareMeter:0} W/m2");
        _box.WriteBorder();
        _box.WriteLine("ZUSTAND");
        _box.WriteLine($"Wechselrichter: {snapshot.InverterStatus}    Manuelle Stoerung: {(snapshot.ManualFaultActive ? "aktiv" : "aus")}    Module: {(snapshot.SnowCoverActive ? "verschneit" : "schneefrei")}");
        _box.WriteLine($"Bewoelkung: {snapshot.Weather.CloudCoverFraction:P0} ({(snapshot.Weather.CloudCoverManuallyOverridden ? "manuell" : "automatisch")})    Temperatur: {snapshot.Weather.AmbientTemperatureCelsius:0.0} C ({(snapshot.Weather.TemperatureManuallyOverridden ? "manuell" : "automatisch")})");
        _box.WriteLine($"Niederschlag: {snapshot.Weather.Precipitation}    Manueller Regen: {(snapshot.Weather.ManualRainActive ? "aktiv" : "aus")}    Wettersteuerung: {selectedWeatherControl}");
        _box.WriteLine($"Zeitraffer: {speedMultiplier:0.##}x");
        _box.WriteBorder();
        _box.WriteLine("TASTEN");
        _box.WriteLine("C Wolken | T Temperatur | +/- Wert oder Geschwindigkeit | A Automatik | R Regen | F Stoerung");
        _box.WriteLine("S Schnee umschalten | P Pause/Fortsetzen");
        _box.WriteBorder();
    }
}
