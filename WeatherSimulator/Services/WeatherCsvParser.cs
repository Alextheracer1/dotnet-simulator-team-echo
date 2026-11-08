using System.Globalization;
using WeatherSimulator.Models;

namespace WeatherSimulator.Services;

public sealed class WeatherCsvParser : IWeatherCsvParser
{
    private readonly ILogger<WeatherCsvParser>? _logger;

    public WeatherCsvParser(ILogger<WeatherCsvParser>? logger = null)
    {
        _logger = logger;
    }

    public WeatherDataset Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Die Wetter-CSV-Datei wurde nicht gefunden: {filePath}", filePath);
        }

        var recordsByLocation = new Dictionary<(double Lat, double Lon), List<WeatherRecord>>();

        using var reader = new StreamReader(filePath);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException($"Die CSV-Datei '{filePath}' ist leer.");
        }

        var delimiter = headerLine.Contains(';') ? ';' : ',';
        var headers = headerLine.Split(delimiter)
            .Select(h => h.Trim().ToLowerInvariant())
            .ToArray();

        var latIndex = Array.IndexOf(headers, "latitude");
        var lonIndex = Array.IndexOf(headers, "longitude");
        var timeIndex = Array.IndexOf(headers, "timestamp");
        var tempIndex = Array.IndexOf(headers, "temperaturec");
        var precipIndex = Array.IndexOf(headers, "precipitationmm");
        var cloudIndex = Array.IndexOf(headers, "cloudcoverpercent");
        var windIndex = Array.IndexOf(headers, "windspeedmps");

        if (latIndex == -1 || lonIndex == -1 || timeIndex == -1 ||
            tempIndex == -1 || precipIndex == -1 || cloudIndex == -1 || windIndex == -1)
        {
            throw new FormatException(
                $"Erforderliche Spalten fehlen im CSV-Header. Erwartet: Latitude, Longitude, Timestamp, TemperatureC, PrecipitationMm, CloudCoverPercent, WindSpeedMps. Gefunden: {headerLine}");
        }

        var maxRequiredIndex = new[] { latIndex, lonIndex, timeIndex, tempIndex, precipIndex, cloudIndex, windIndex }.Max();

        string? line;
        var lineNumber = 1;
        var parsedCount = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(delimiter);
            if (parts.Length <= maxRequiredIndex)
            {
                _logger?.LogWarning("Zeile {LineNumber} übersprungen (zu wenige Spalten): {Line}", lineNumber, line);
                continue;
            }

            try
            {
                var lat = double.Parse(parts[latIndex].Trim(), CultureInfo.InvariantCulture);
                var lon = double.Parse(parts[lonIndex].Trim(), CultureInfo.InvariantCulture);
                var time = DateTimeOffset.Parse(parts[timeIndex].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                var temp = double.Parse(parts[tempIndex].Trim(), CultureInfo.InvariantCulture);
                var precip = double.Parse(parts[precipIndex].Trim(), CultureInfo.InvariantCulture);
                var cloud = double.Parse(parts[cloudIndex].Trim(), CultureInfo.InvariantCulture);
                var wind = double.Parse(parts[windIndex].Trim(), CultureInfo.InvariantCulture);

                var record = new WeatherRecord(lat, lon, time, temp, precip, cloud, wind);

                // Round key to 5 decimal places for robust grouping
                var key = (Math.Round(lat, 5), Math.Round(lon, 5));
                if (!recordsByLocation.TryGetValue(key, out var list))
                {
                    list = new List<WeatherRecord>();
                    recordsByLocation[key] = list;
                }

                list.Add(record);
                parsedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Fehler beim Parsen von Zeile {LineNumber}: {Line}", lineNumber, line);
            }
        }

        if (recordsByLocation.Count == 0)
        {
            throw new InvalidOperationException($"Aus der CSV-Datei '{filePath}' konnten keine gültigen Datensätze geladen werden.");
        }

        var seriesList = new List<LocationWeatherSeries>(recordsByLocation.Count);
        foreach (var (key, list) in recordsByLocation)
        {
            seriesList.Add(new LocationWeatherSeries(key.Lat, key.Lon, list));
        }

        _logger?.LogInformation("CSV erfolgreich geladen: {ParsedCount} Datensätze an {LocationCount} Standorten.",
            parsedCount, seriesList.Count);

        return new WeatherDataset(seriesList);
    }
}

