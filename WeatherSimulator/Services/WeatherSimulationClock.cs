using TeamShared.Simulation;

namespace WeatherSimulator.Services;

public sealed class WeatherSimulationClock
{
    private readonly object _sync = new();
    private DateTime _virtualTime;
    private TimeSpan _timeStep = TimeSpan.FromSeconds(1);
    private bool _isRunning;
    private bool _hasVirtualTime;
    private bool _isMasterConnected;

    public SimulationEnvironment? Current
    {
        get
        {
            lock (_sync)
            {
                return _hasVirtualTime
                    ? new SimulationEnvironment(_virtualTime, 0, _isRunning, _timeStep)
                    : null;
            }
        }
    }

    public bool IsMasterConnected
    {
        get
        {
            lock (_sync)
                return _isMasterConnected;
        }
    }

    public DateTime? VirtualTime
    {
        get
        {
            lock (_sync)
                return _hasVirtualTime ? _virtualTime : null;
        }
    }

    public TimeSpan TimeStep
    {
        get
        {
            lock (_sync)
                return _timeStep;
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _isRunning;
        }
    }

    public void ApplyMasterMessage(SimulationMessage message)
    {
        if (message.Type is not (SimulationMessageTypes.Environment or SimulationMessageTypes.TimeStep))
            return;

        lock (_sync)
        {
            _virtualTime = message.VirtualTime;
            if (message.TimeStep > TimeSpan.Zero)
                _timeStep = message.TimeStep;
            _isRunning = message.IsRunning;
            _hasVirtualTime = true;
            _isMasterConnected = true;
        }
    }

    public void SetMasterConnected(bool connected)
    {
        lock (_sync)
            _isMasterConnected = connected;
    }

    public void AdvanceFallback()
    {
        lock (_sync)
        {
            if (_hasVirtualTime && !_isMasterConnected && _isRunning)
                _virtualTime += _timeStep;
        }
    }
}
