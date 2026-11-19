namespace WeatherSimulator.Models;

public sealed class LocationWeatherSeries
{
    public double Latitude { get; }
    public double Longitude { get; }
    public IReadOnlyList<WeatherRecord> Records { get; }
    public DateTimeOffset MinTime { get; }
    public DateTimeOffset MaxTime { get; }
    public int ReferenceYear { get; }

    public LocationWeatherSeries(double latitude, double longitude, List<WeatherRecord> records)
    {
        Latitude = latitude;
        Longitude = longitude;
        records.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        Records = records;
        MinTime = records.Count > 0 ? records[0].Timestamp : DateTimeOffset.MinValue;
        MaxTime = records.Count > 0 ? records[^1].Timestamp : DateTimeOffset.MaxValue;
        ReferenceYear = MinTime != DateTimeOffset.MinValue ? MinTime.Year : DateTimeOffset.UtcNow.Year;
    }
}

public sealed class WeatherDataset
{
    public IReadOnlyList<LocationWeatherSeries> SeriesByLocation { get; }
    public int TotalRecords { get; }

    public WeatherDataset(IReadOnlyList<LocationWeatherSeries> series)
    {
        SeriesByLocation = series;
        TotalRecords = series.Sum(s => s.Records.Count);
    }
}

