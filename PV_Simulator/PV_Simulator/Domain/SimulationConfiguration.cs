namespace PV_Simulator.Domain;

public sealed record SimulationConfiguration(
    Location Location,
    PvArray Array,
    Inverter Inverter,
    SimulationSettings Settings,
    WeatherSettings WeatherSettings);
