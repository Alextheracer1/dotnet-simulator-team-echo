using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public interface IWeatherCsvParser
{
    WeatherDataset Parse(string filePath);
}

