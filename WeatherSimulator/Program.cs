using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using WeatherSimulator.Models;
using WeatherSimulator.Services;

var builder = WebApplication.CreateBuilder(args);

// Port konfigurieren (Standard 5005)
var port = builder.Configuration.GetValue<int?>("WeatherSimulator:Port") ?? 5005;
var configuredUrls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls($"http://localhost:{port}");
}

// CSV-Dateipfad ermitteln
var csvConfigPath = builder.Configuration.GetValue<string>("WeatherSimulator:CsvFilePath") ?? "Data/wetterdaten_salzburg.csv";
var resolvedCsvPath = ResolveCsvPath(csvConfigPath);

if (resolvedCsvPath == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[FEHLER] Die Wetter-CSV-Datei konnte nicht gefunden werden (gesucht nach '{csvConfigPath}').");
    Console.ResetColor();
    Environment.Exit(1);
}

// Services registrieren
builder.Services.AddSingleton<ISolarPositionCalculator, SolarPositionCalculator>();
builder.Services.AddSingleton<IWeatherCsvParser, WeatherCsvParser>();

var csvParser = new WeatherCsvParser();
var dataset = csvParser.Parse(resolvedCsvPath);
builder.Services.AddSingleton(dataset);
builder.Services.AddSingleton<IWeatherService, WeatherService>();

var app = builder.Build();

// Startup-Info ausgeben
PrintStartupBanner(port, resolvedCsvPath, dataset);

// Root Info-Endpunkt
app.MapGet("/", () => Results.Ok(new
{
    service = "WeatherSimulator - Digital Twin Weather Provider",
    status = "Running",
    endpoint = "/api/weather?lat={latitude}&lon={longitude}&time={timestamp}",
    example = $"/api/weather?lat=47.77&lon=13.09&time={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}",
    locationsCount = dataset.SeriesByLocation.Count,
    totalRecords = dataset.TotalRecords
}));

// Health-Endpunkt
app.MapGet("/api/weather/health", () => Results.Ok(new
{
    status = "Healthy",
    records = dataset.TotalRecords,
    locations = dataset.SeriesByLocation.Count,
    serverTime = DateTimeOffset.UtcNow
}));

// Hauptendpunkt /api/weather
app.MapGet("/api/weather", (
    [FromQuery] double? lat,
    [FromQuery] double? lon,
    [FromQuery] string? time,
    IWeatherService weatherService,
    ILogger<Program> logger) =>
{
    if (!lat.HasValue || !lon.HasValue || string.IsNullOrWhiteSpace(time))
    {
        return Results.BadRequest(new
        {
            error = "Fehlende Parameter. Bitte 'lat', 'lon' und 'time' übergeben.",
            example = "/api/weather?lat=47.77&lon=13.09&time=2026-10-29T12:00:00Z"
        });
    }

    if (lat.Value is < -90.0 or > 90.0)
    {
        return Results.BadRequest(new { error = "Ungültige Latitude. Wert muss zwischen -90 und 90 liegen." });
    }

    if (lon.Value is < -180.0 or > 180.0)
    {
        return Results.BadRequest(new { error = "Ungültige Longitude. Wert muss zwischen -180 und 180 liegen." });
    }

    if (!DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime) &&
        !DateTimeOffset.TryParse(time, out parsedTime))
    {
        return Results.BadRequest(new
        {
            error = $"Ungültiges Zeitformat '{time}'. Erwartet wird ISO 8601 (z.B. 2026-10-29T12:00:00Z)."
        });
    }

    var result = weatherService.GetWeather(lat.Value, lon.Value, parsedTime);

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Weather GET: ({lat.Value:F2}, {lon.Value:F2}) @ {parsedTime:s} -> {result.TemperatureC}°C, Sun: Az={result.Azimuth}°, El={result.Elevation}°");

    return Results.Ok(result);
});

app.Run();

// Helper-Methoden
static string? ResolveCsvPath(string relativeOrAbsolutePath)
{
    var candidates = new List<string>
    {
        relativeOrAbsolutePath,
        Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath),
        Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolutePath),
        Path.Combine(AppContext.BaseDirectory, "Data", "wetterdaten_salzburg.csv"),
        Path.Combine(Directory.GetCurrentDirectory(), "Data", "wetterdaten_salzburg.csv"),
        Path.Combine(Directory.GetCurrentDirectory(), "WeatherSimulator", "Data", "wetterdaten_salzburg.csv"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "wetterdaten_salzburg.csv"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "wetterdaten_salzburg.csv"),
        "wetterdaten_salzburg.csv"
    };

    foreach (var candidate in candidates)
    {
        try
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        catch
        {
            // Ungültiger Pfad, weiter suchen
        }
    }

    return null;
}

static void PrintStartupBanner(int port, string csvPath, WeatherDataset dataset)
{
    Console.WriteLine();
    Console.WriteLine("================================================================================");
    Console.WriteLine("                  WeatherSimulator - Digital Twin Weather Provider              ");
    Console.WriteLine("================================================================================");
    Console.WriteLine($" Local Endpoint : http://localhost:{port}/api/weather");
    Console.WriteLine($" CSV Data Source: {csvPath}");
    Console.WriteLine($" Total Records  : {dataset.TotalRecords:N0} Zeilen");
    Console.WriteLine($" Locations      : {dataset.SeriesByLocation.Count} Standort(e)");
    foreach (var s in dataset.SeriesByLocation)
    {
        Console.WriteLine($"   - Lat: {s.Latitude:F2}, Lon: {s.Longitude:F2} ({s.Records.Count} Stützstellen von {s.MinTime:yyyy-MM-dd} bis {s.MaxTime:yyyy-MM-dd})");
    }
    Console.WriteLine("================================================================================");
    Console.WriteLine($" Beispielabfrage: http://localhost:{port}/api/weather?lat=47.77&lon=13.09&time={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
    Console.WriteLine("================================================================================");
    Console.WriteLine();
}

