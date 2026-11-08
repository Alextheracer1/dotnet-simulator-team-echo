using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TeamShared.Weather;

namespace WeatherSimulator.Services;

public interface IWeatherClientRegistry
{
    void Register(DeviceWeatherRegistration registration);
    IReadOnlyCollection<DeviceWeatherRegistration> GetAll();
    void Unregister(string deviceId);
}

public sealed class WeatherClientRegistry : IWeatherClientRegistry
{
    private readonly ConcurrentDictionary<string, DeviceWeatherRegistration> _clients = new(StringComparer.OrdinalIgnoreCase);

    public void Register(DeviceWeatherRegistration registration)
    {
        _clients[registration.DeviceId] = registration;
    }

    public IReadOnlyCollection<DeviceWeatherRegistration> GetAll() => _clients.Values.ToList();

    public void Unregister(string deviceId)
    {
        _clients.TryRemove(deviceId, out _);
    }
}

