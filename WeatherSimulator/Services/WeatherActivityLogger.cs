using System;
using System.Collections.Generic;

namespace WeatherSimulator.Services;

public interface IWeatherActivityLogger
{
    IReadOnlyList<string> RecentLogs { get; }
    void Log(string message);
    long TotalGetRequests { get; }
    long TotalPushes { get; }
    void IncrementGetRequests();
    void IncrementPushes(int count = 1);
}

public sealed class WeatherActivityLogger : IWeatherActivityLogger
{
    private const int MaxLogEntries = 15;
    private readonly object _sync = new();
    private readonly List<string> _recentLogs = new();
    private long _totalGetRequests;
    private long _totalPushes;

    public IReadOnlyList<string> RecentLogs
    {
        get
        {
            lock (_sync)
            {
                return _recentLogs.ToArray();
            }
        }
    }

    public long TotalGetRequests => Interlocked.Read(ref _totalGetRequests);

    public long TotalPushes => Interlocked.Read(ref _totalPushes);

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var timestamped = $"[grey][[{DateTime.Now:HH:mm:ss}]][/] {message}";

        lock (_sync)
        {
            _recentLogs.Add(timestamped);
            if (_recentLogs.Count > MaxLogEntries)
            {
                _recentLogs.RemoveAt(0);
            }
        }
    }

    public void IncrementGetRequests()
    {
        Interlocked.Increment(ref _totalGetRequests);
    }

    public void IncrementPushes(int count = 1)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _totalPushes, count);
        }
    }
}
