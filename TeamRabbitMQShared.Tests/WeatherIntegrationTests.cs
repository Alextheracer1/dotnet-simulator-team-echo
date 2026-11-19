using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TeamShared.Weather;
using Xunit;

namespace TeamRabbitMQShared.Tests;

public class WeatherIntegrationTests
{
    [Fact]
    public void WeatherSnapshotDto_SerializationAndDeserialization_MaintainsData()
    {
        var now = DateTimeOffset.UtcNow;
        var dto = new WeatherSnapshotDto(
            Latitude: 47.77,
            Longitude: 13.09,
            Timestamp: now,
            TemperatureC: 15.5,
            PrecipitationMm: 0.2,
            CloudCoverPercent: 65.0,
            WindSpeedMps: 4.8,
            Azimuth: 182.5,
            Elevation: 35.1);

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<WeatherSnapshotDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(47.77, deserialized.Latitude);
        Assert.Equal(13.09, deserialized.Longitude);
        Assert.Equal(15.5, deserialized.TemperatureC);
        Assert.Equal(0.2, deserialized.PrecipitationMm);
        Assert.Equal(65.0, deserialized.CloudCoverPercent);
        Assert.Equal(4.8, deserialized.WindSpeedMps);
        Assert.Equal(182.5, deserialized.Azimuth);
        Assert.Equal(35.1, deserialized.Elevation);
    }

    [Fact]
    public void DeviceWeatherRegistration_SerializationAndDeserialization_MaintainsData()
    {
        var reg = new DeviceWeatherRegistration
        {
            DeviceId = "pv-station-1",
            DeviceType = "pv",
            CallbackUrl = "http://localhost:6000/weather/",
            Latitude = 47.80,
            Longitude = 13.04
        };

        var json = JsonSerializer.Serialize(reg);
        var deserialized = JsonSerializer.Deserialize<DeviceWeatherRegistration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("pv-station-1", deserialized.DeviceId);
        Assert.Equal("pv", deserialized.DeviceType);
        Assert.Equal("http://localhost:6000/weather/", deserialized.CallbackUrl);
        Assert.Equal(47.80, deserialized.Latitude);
        Assert.Equal(13.04, deserialized.Longitude);
    }

    [Fact]
    public async Task WeatherCallbackReceiver_ReceivesAndMatchesSnapshotAsync()
    {
        // Start receiver on dynamic port starting at 6200
        await using var receiver = await WeatherCallbackReceiver.StartAsync(6200, "test");
        Assert.True(receiver.BoundPort >= 6200);

        var targetTime = DateTime.UtcNow;
        var dto = new WeatherSnapshotDto(
            Latitude: 47.77,
            Longitude: 13.09,
            Timestamp: targetTime,
            TemperatureC: 21.0,
            PrecipitationMm: 0.0,
            CloudCoverPercent: 20.0,
            WindSpeedMps: 7.2,
            Azimuth: 170.0,
            Elevation: 40.0);

        // Snapshot direkt erfassen und Abgleich mit Warte-Logik verifizieren
        receiver.RecordSnapshot(dto);

        // Wait for snapshot
        var received = await receiver.WaitForSnapshotAsync(targetTime, TimeSpan.FromSeconds(2));
        Assert.NotNull(received);
        Assert.Equal(21.0, received.TemperatureC);
        Assert.Equal(7.2, received.WindSpeedMps);
    }
}
