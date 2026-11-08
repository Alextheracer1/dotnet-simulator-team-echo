using System;

namespace SimulationsMaster;

/// <summary>
/// Thread-safe virtual time engine that advances only by explicitly committed simulation steps.
/// </summary>
public sealed class SimulationEngine
{
    private readonly object _syncRoot = new();
    private static readonly TimeSpan[] AvailableTimeSteps =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
        TimeSpan.FromDays(7)
    ];

    private DateTime _currentVirtualTime;
    private DateTime _startVirtualTime;
    private bool _isRunning;
    private TimeSpan _timeStep = TimeSpan.FromSeconds(1);

    public SimulationEngine(DateTime? initialVirtualTime = null)
    {
        var initialValue = initialVirtualTime ?? DateTime.UtcNow;
        _currentVirtualTime = initialValue;
        _startVirtualTime = initialValue;
        _isRunning = false;
    }

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRunning;
            }
        }
    }

    public DateTime CurrentVirtualTime
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentVirtualTime;
            }
        }
    }

    public DateTime StartVirtualTime
    {
        get
        {
            lock (_syncRoot)
            {
                return _startVirtualTime;
            }
        }
    }

    public TimeSpan TimeStep
    {
        get
        {
            lock (_syncRoot)
            {
                return _timeStep;
            }
        }
    }

    public SimulationStatusSnapshot GetStatusSnapshot()
    {
        lock (_syncRoot)
        {
            return new SimulationStatusSnapshot(
                _isRunning,
                _currentVirtualTime,
                _startVirtualTime,
                _timeStep);
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                return;
            }

            _startVirtualTime = _currentVirtualTime;
            _isRunning = true;
        }
    }

    public void Pause()
    {
        lock (_syncRoot)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
        }
    }

    public void Resume()
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
        }
    }

    public void ChangeTimeStep(TimeSpan timeStep)
    {
        if (timeStep <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeStep), "Time step must be greater than zero.");

        lock (_syncRoot)
            _timeStep = timeStep;
    }

    public void Restart(DateTime? newVirtualTime = null)
    {
        // Restart is intentionally not available per the required master control model.
        throw new NotSupportedException("Restart is not supported for this simulation master.");
    }

    public TimeSpan[] GetAvailableTimeSteps() => [.. AvailableTimeSteps];

    public TimeSpan GetNearestTimeStep(TimeSpan requestedTimeStep)
    {
        var nearest = AvailableTimeSteps[0];
        var bestDelta = double.MaxValue;

        foreach (var option in AvailableTimeSteps)
        {
            var delta = Math.Abs((option - requestedTimeStep).Ticks);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                nearest = option;
            }
        }

        return nearest;
    }

    public DateTime GetCurrentVirtualTime()
    {
        lock (_syncRoot)
            return _currentVirtualTime;
    }

    public bool CommitTimeStep(DateTime expectedPreviousTime, TimeSpan timeStep)
    {
        if (timeStep <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeStep), "Time step must be greater than zero.");

        lock (_syncRoot)
        {
            if (_currentVirtualTime != expectedPreviousTime)
                return false;

            _currentVirtualTime += timeStep;
            return true;
        }
    }

    /// <summary>
    /// Reconciles the master clock with a station's last known time without moving time backwards.
    /// </summary>
    public void ReconcileFromStation(DateTime stationVirtualTime)
    {
        lock (_syncRoot)
        {
            if (stationVirtualTime <= _currentVirtualTime)
                return;

            _currentVirtualTime = stationVirtualTime;
        }
    }
}

public sealed record SimulationStatusSnapshot(
    bool IsRunning,
    DateTime CurrentVirtualTime,
    DateTime StartVirtualTime,
    TimeSpan TimeStep);
