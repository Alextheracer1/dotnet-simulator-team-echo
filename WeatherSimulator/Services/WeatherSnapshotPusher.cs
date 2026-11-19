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
    IWeatherActivityLogger? activityLogger = null,
    ILogger<WeatherSnapshotPusher>? logger = null) : IWeatherSnapshotPusher
{
    public async Task PushSnapshotsAsync(DateTime virtualTime, CancellationToken cancellationToken = default)
    {
        var clients = clientRegistry.GetAll();
        if (clients.Count == 0)
            return;

        var client = httpClientFactory.CreateClient("WeatherPusher");
        client.Timeout = TimeSpan.FromSeconds(3);
        var utcTime = DateTime.SpecifyKind(virtualTime, DateTimeKind.Utc);

        var successCount = 0;
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
                    successCount++;
                    clientRegistry.RecordPush(dev.DeviceId, true);
                    logger?.LogDebug("Wetter-Push an {DeviceId} ({CallbackUrl}) erfolgreich übertragen.", dev.DeviceId, dev.CallbackUrl);
                }
                else
                {
                    clientRegistry.RecordPush(dev.DeviceId, false, $"HTTP {(int)response.StatusCode}");
                    logger?.LogWarning("Wetter-Push an {DeviceId} ({CallbackUrl}) gab Status {StatusCode} zurück.", dev.DeviceId, dev.CallbackUrl, response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                clientRegistry.RecordPush(dev.DeviceId, false, ex.Message);
                logger?.LogWarning("Wetter-Push an {DeviceId} ({CallbackUrl}) fehlgeschlagen: {Message}", dev.DeviceId, dev.CallbackUrl, ex.Message);
            }
        }

        activityLogger?.IncrementPushes(successCount);
        if (successCount > 0)
        {
            activityLogger?.Log($"[chartreuse3]Wetter-Push:[/] {successCount}/{clients.Count} Feldgeräte aktualisiert @ {virtualTime:HH:mm:ss}");
        }
        else if (clients.Count > 0)
        {
            activityLogger?.Log($"[red]Wetter-Push:[/] 0/{clients.Count} Feldgeräte erfolgreich erreicht");
        }
    }
}
