namespace TeamShared.Simulation;

/// <summary>Message types exchanged over the line-delimited JSON synchronization channel.</summary>
public static class SimulationMessageTypes
{
    public const string EnvironmentRequest = "EnvironmentRequest";
    public const string Environment = "Environment";
    public const string Register = "Register";
    public const string State = "State";
    public const string TimeStep = "TimeStep";
    public const string Acknowledgement = "Acknowledgement";
    public const string Error = "Error";
}

/// <summary>Common envelope for messages sent between stations and the Time Master.</summary>
public sealed class SimulationMessage
{
    public string Type { get; set; } = string.Empty;
    public string? StationId { get; set; }
    public long Sequence { get; set; }
    public DateTime VirtualTime { get; set; }
    public DateTime PreviousVirtualTime { get; set; }
    public TimeSpan TimeStep { get; set; } = TimeSpan.FromSeconds(1);
    public bool IsRunning { get; set; }
    public bool IsOn { get; set; }
    public string? Status { get; set; }
    public double? ActivePowerKW { get; set; }
    public double? CumulativeEnergyKWh { get; set; }
    public double? StateOfChargePercent { get; set; }
    public double? SetpointKW { get; set; }
}

/// <summary>Snapshot of the virtual clock and runtime state shared with all connected stations.</summary>
public sealed record SimulationEnvironment(
    DateTime VirtualTime,
    long Sequence,
    bool IsRunning,
    TimeSpan TimeStep);

/// <summary>Shared contract for a field-device connection to the Time Master.</summary>
public interface ITimeMasterClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task ConnectWithRetryAsync(CancellationToken cancellationToken = default);
    Task<SimulationEnvironment?> RequestEnvironmentAsync(CancellationToken cancellationToken = default);
    Task<SimulationMessage?> WaitForNextMessageAsync(CancellationToken cancellationToken = default);
    Task SendAcknowledgementAsync(long sequence, DateTime virtualTime, CancellationToken cancellationToken = default);
    Task SendStateAsync(SimulationMessage state, CancellationToken cancellationToken = default);
}
