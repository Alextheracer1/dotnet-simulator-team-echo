using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public sealed class WeatherService : IWeatherService
{
    private const double EarthRadiusKm = 6371.0;
    private readonly WeatherDataset _dataset;
    private readonly ISolarPositionCalculator _solarCalculator;
    private readonly ILogger<WeatherService>? _logger;

    public WeatherDataset Dataset => _dataset;

    public WeatherService(
        WeatherDataset dataset,
        ISolarPositionCalculator solarCalculator,
        ILogger<WeatherService>? logger = null)
    {
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        _solarCalculator = solarCalculator ?? throw new ArgumentNullException(nameof(solarCalculator));
        _logger = logger;

        if (_dataset.SeriesByLocation.Count == 0)
        {
            throw new ArgumentException("Das Wetter-Dataset enthält keine Standorte.", nameof(dataset));
        }
    }

    public WeatherResponse GetWeather(double latitude, double longitude, DateTimeOffset requestedTime)
    {
        // 1. Finde den geodätisch nächstgelegenen Standort via Haversine
        var series = FindNearestSeries(latitude, longitude);

        // 2. Zeitstempel auf das Referenzjahr mappen (Jahres-Wrapping / Modulo)
        var mappedTime = MapToDatasetTime(requestedTime, series);

        // 3. Lineare Interpolation der Wetterdaten zwischen den Stützstellen
        var (temp, precip, cloud, wind) = InterpolateWeather(series, mappedTime);

        // 4. Exakter Sonnenstand für die tatsächliche angefragte Zeit und Koordinaten
        var sun = _solarCalculator.Calculate(latitude, longitude, requestedTime);

        return new WeatherResponse(
            Latitude: series.Latitude,
            Longitude: series.Longitude,
            Timestamp: requestedTime,
            TemperatureC: Math.Round(temp, 2),
            PrecipitationMm: Math.Round(precip, 2),
            CloudCoverPercent: Math.Round(cloud, 2),
            WindSpeedMps: Math.Round(wind, 2),
            Azimuth: Math.Round(sun.Azimuth, 2),
            Elevation: Math.Round(sun.Elevation, 2)
        );
    }

    private LocationWeatherSeries FindNearestSeries(double targetLat, double targetLon)
    {
        LocationWeatherSeries? bestSeries = null;
        var minDistanceKm = double.MaxValue;

        foreach (var series in _dataset.SeriesByLocation)
        {
            var dist = CalculateHaversineDistanceKm(targetLat, targetLon, series.Latitude, series.Longitude);
            if (dist < minDistanceKm)
            {
                minDistanceKm = dist;
                bestSeries = series;
            }
        }

        return bestSeries ?? _dataset.SeriesByLocation[0];
    }

    public static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var lat1Rad = DegreesToRadians(lat1);
        var lat2Rad = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusKm * c;
    }

    private static DateTimeOffset MapToDatasetTime(DateTimeOffset requestedTime, LocationWeatherSeries series)
    {
        var utc = requestedTime.UtcDateTime;
        var targetYear = series.ReferenceYear;

        var day = utc.Day;
        // Schaltjahr-Korrektur: Falls der 29. Feb angefragt wurde, das Referenzjahr aber kein Schaltjahr ist
        if (utc.Month == 2 && day == 29 && !DateTime.IsLeapYear(targetYear))
        {
            day = 28;
        }

        var mappedUtc = new DateTime(targetYear, utc.Month, day, utc.Hour, utc.Minute, utc.Second, utc.Millisecond, DateTimeKind.Utc);
        var mappedOffset = new DateTimeOffset(mappedUtc, TimeSpan.Zero);

        // Clamping an Randgrenzen der Datenreihe
        if (mappedOffset < series.MinTime)
        {
            mappedOffset = series.MinTime;
        }
        else if (mappedOffset > series.MaxTime)
        {
            mappedOffset = series.MaxTime;
        }

        return mappedOffset;
    }

    private static (double Temp, double Precip, double Cloud, double Wind) InterpolateWeather(
        LocationWeatherSeries series,
        DateTimeOffset mappedTime)
    {
        var records = series.Records;
        if (records.Count == 1 || mappedTime <= records[0].Timestamp)
        {
            var first = records[0];
            return (first.TemperatureC, first.PrecipitationMm, first.CloudCoverPercent, first.WindSpeedMps);
        }

        if (mappedTime >= records[^1].Timestamp)
        {
            var last = records[^1];
            return (last.TemperatureC, last.PrecipitationMm, last.CloudCoverPercent, last.WindSpeedMps);
        }

        // Binary Search für die beiden umgebenden Stützstellen
        int low = 0;
        int high = records.Count - 1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var midTime = records[mid].Timestamp;

            if (midTime == mappedTime)
            {
                var exact = records[mid];
                return (exact.TemperatureC, exact.PrecipitationMm, exact.CloudCoverPercent, exact.WindSpeedMps);
            }

            if (midTime < mappedTime)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        var prev = records[high];
        var next = records[low];

        var totalSeconds = (next.Timestamp - prev.Timestamp).TotalSeconds;
        var fraction = totalSeconds > 0
            ? (mappedTime - prev.Timestamp).TotalSeconds / totalSeconds
            : 0.0;
        fraction = Math.Clamp(fraction, 0.0, 1.0);

        var temp = prev.TemperatureC + fraction * (next.TemperatureC - prev.TemperatureC);
        var precip = prev.PrecipitationMm + fraction * (next.PrecipitationMm - prev.PrecipitationMm);
        var cloud = prev.CloudCoverPercent + fraction * (next.CloudCoverPercent - prev.CloudCoverPercent);
        var wind = prev.WindSpeedMps + fraction * (next.WindSpeedMps - prev.WindSpeedMps);

        return (temp, precip, cloud, wind);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

