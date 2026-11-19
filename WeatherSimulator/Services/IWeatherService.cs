using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public interface IWeatherService
{
    WeatherResponse GetWeather(double latitude, double longitude, DateTimeOffset requestedTime);
    WeatherDataset Dataset { get; }
}

