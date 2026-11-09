using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TeamShared.Weather;

namespace WeatherSimulator.Services;

public sealed class ClientRegistrationDetail
{
    public DeviceWeatherRegistration Registration { get; }
    public DateTime RegisteredAt { get; } = DateTime.Now;
    public DateTime? LastPushAt { get; private set; }
    public bool? LastPushSuccess { get; private set; }
    public long PushCount { get; private set; }
    public string? LastError { get; private set; }

    public ClientRegistrationDetail(DeviceWeatherRegistration registration)
    {
        Registration = registration;
    }

    public void RecordPush(bool success, string? error = null)
    {
        LastPushAt = DateTime.Now;
        LastPushSuccess = success;
        if (success)
            PushCount++;
        else
            LastError = error;
    }
}

public interface IWeatherClientRegistry
{
    void Register(DeviceWeatherRegistration registration);
    IReadOnlyCollection<DeviceWeatherRegistration> GetAll();
    IReadOnlyCollection<ClientRegistrationDetail> GetDetails();
    void RecordPush(string deviceId, bool success, string? error = null);
    void Unregister(string deviceId);
}

public sealed class WeatherClientRegistry : IWeatherClientRegistry
{
    private readonly ConcurrentDictionary<string, ClientRegistrationDetail> _clients = new(StringComparer.OrdinalIgnoreCase);

    public void Register(DeviceWeatherRegistration registration)
    {
        _clients[registration.DeviceId] = new ClientRegistrationDetail(registration);
    }

    public IReadOnlyCollection<DeviceWeatherRegistration> GetAll() =>
        _clients.Values.Select(c => c.Registration).ToList();

    public IReadOnlyCollection<ClientRegistrationDetail> GetDetails() =>
        _clients.Values.ToList();

    public void RecordPush(string deviceId, bool success, string? error = null)
    {
        if (_clients.TryGetValue(deviceId, out var client))
        {
            client.RecordPush(success, error);
        }
    }

    public void Unregister(string deviceId)
    {
        _clients.TryRemove(deviceId, out _);
    }
}
