using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public interface ISolarPositionCalculator
{
    SolarPosition Calculate(double latitude, double longitude, DateTimeOffset dateTime);
}

