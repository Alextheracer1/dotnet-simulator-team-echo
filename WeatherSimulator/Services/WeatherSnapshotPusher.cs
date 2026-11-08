using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamShared.Weather;

namespace WeatherSimulator.Services;

public interface IWeatherSnapshotPusher
{
    Task PushSnapshotsAsync(DateTime virtualTime, CancellationToken cancellationToken = default);
}

public sealed class WeatherSnapshotPusher(
    IWeatherService weatherService,
    IWeatherClientRegistry clientRegistry,
    IHttpClientFactory httpClientFactory,
    ILogger<WeatherSnapshotPusher> logger) : IWeatherSnapshotPusher
{
    public async Task PushSnapshotsAsync(DateTime virtualTime, CancellationToken cancellationToken = default)
    {
        var clients = clientRegistry.GetAll();
        if (clients.Count == 0)
            return;

        var client = httpClientFactory.CreateClient("WeatherPusher");
        client.Timeout = TimeSpan.FromSeconds(3);
        var utcTime = DateTime.SpecifyKind(virtualTime, DateTimeKind.Utc);

        foreach (var dev in clients)
        {
            try
            {
                var weather = weatherService.GetWeather(dev.Latitude, dev.Longitude, utcTime);
                var dto = new WeatherSnapshotDto(
                    weather.Latitude,
                    weather.Longitude,
                    weather.Timestamp,
                    weather.TemperatureC,
                    weather.PrecipitationMm,
                    weather.CloudCoverPercent,
                    weather.WindSpeedMps,
                    weather.Azimuth,
                    weather.Elevation);

                var response = await client.PostAsJsonAsync(dev.CallbackUrl, dto, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Wetter-Push an {DeviceId} ({CallbackUrl}) erfolgreich übertragen.", dev.DeviceId, dev.CallbackUrl);
                }
                else
                {
                    logger.LogWarning("Wetter-Push an {DeviceId} ({CallbackUrl}) gab Status {StatusCode} zurück.", dev.DeviceId, dev.CallbackUrl, response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Wetter-Push an {DeviceId} ({CallbackUrl}) fehlgeschlagen: {Message}", dev.DeviceId, dev.CallbackUrl, ex.Message);
            }
        }
    }
}

