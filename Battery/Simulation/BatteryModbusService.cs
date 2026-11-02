using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Channels;
using Battery.Battery;
using TeamShared;

namespace Battery.Simulation;

/// <summary>Immutable view of the battery values exposed through Modbus holding registers.</summary>
public sealed record BatteryModbusSnapshot(
    DateTime VirtualTime,
    long Sequence,
    BatteryState State,
    bool IsOn,
    double ActivePowerKW,
    double CumulativeEnergyKWh,
    double StateOfChargePercent,
    double VoltageV,
    double SetpointKW);

/// <summary>
/// Hosts the EasyModbus TCP server independently of the simulation clock. Register updates continue while
/// paused, so Modbus clients can read the last committed state and write new setpoints or fault resets.
/// </summary>
public sealed class BatteryModbusService : IAsyncDisposable
{
    private static readonly TimeSpan RegisterRefreshInterval = TimeSpan.FromMilliseconds(100);

    private readonly FieldDeviceModbusServer _server;
    private readonly Func<BatteryModbusSnapshot> _getSnapshot;
    private readonly Action<float> _setSetpoint;
    private readonly Action _resetFault;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _linkedLifetime;
    private Task? _listenerTask;
    private Task? _registerTask;
    private float _lastObservedSetpoint = float.NaN;

    public BatteryModbusService(
        int port,
        byte unitId,
        Func<BatteryModbusSnapshot> getSnapshot,
        Action<float> setSetpoint,
        Action resetFault)
    {
        _server = new FieldDeviceModbusServer(port, unitId);
        _getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
        _setSetpoint = setSetpoint ?? throw new ArgumentNullException(nameof(setSetpoint));
        _resetFault = resetFault ?? throw new ArgumentNullException(nameof(resetFault));
    }

    /// <summary>Starts the synchronous EasyModbus listener on its own long-running worker and updates registers asynchronously.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listenerTask is not null)
            throw new InvalidOperationException("The Modbus service has already been started.");

        _linkedLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = _linkedLifetime.Token;
        var initial = _getSnapshot();
        WriteSnapshot(initial, includeSetpoint: true);
        _lastObservedSetpoint = (float)initial.SetpointKW;

        // EasyModbus exposes a synchronous Listen API. Isolating it on a dedicated long-running task keeps
        // its blocking implementation away from the simulation/UI async control loop.
        _listenerTask = Task.Factory.StartNew(
            _server.Start,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
        if (_listenerTask.IsFaulted)
            await _listenerTask.ConfigureAwait(false);

        _registerTask = UpdateRegistersAsync(token);
    }

    /// <summary>Stops the listener and awaits both the listener worker and register-poll loop.</summary>
    public async Task StopAsync()
    {
        _lifetime.Cancel();
        _linkedLifetime?.Cancel();
        try
        {
            _server.Stop();
        }
        catch (SocketException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        if (_registerTask is not null)
        {
            try
            {
                await _registerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _linkedLifetime?.Dispose();
        _linkedLifetime = null;

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask.ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private async Task UpdateRegistersAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RegisterRefreshInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (_server.CheckAndClearErrorReset())
                    _resetFault();

                var setpoint = _server.ReadSetpoint();
                var externalSetpointChanged = false;
                if (float.IsFinite(setpoint) && Math.Abs(setpoint - _lastObservedSetpoint) >= 0.001f)
                {
                    _lastObservedSetpoint = setpoint;
                    externalSetpointChanged = true;
                    _setSetpoint(setpoint);
                }

                var snapshot = _getSnapshot();
                if (!externalSetpointChanged && Math.Abs(snapshot.SetpointKW - _lastObservedSetpoint) >= 0.001f)
                {
                    _lastObservedSetpoint = (float)snapshot.SetpointKW;
                    _server.UpdateFloatRegisters(CommonRegisterMap.Setpoint_High, _lastObservedSetpoint);
                }

                WriteSnapshot(snapshot);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void WriteSnapshot(BatteryModbusSnapshot snapshot, bool includeSetpoint = false)
    {
        var operatingState = snapshot.State == BatteryState.Fault ? (short)3 : snapshot.IsOn ? (short)2 : (short)0;
        var errorCode = snapshot.State == BatteryState.Fault ? (short)1 : (short)0;
        _server.UpdateCommonRegisters(
            operatingState,
            errorCode,
            (float)snapshot.ActivePowerKW,
            (float)snapshot.CumulativeEnergyKWh,
            unchecked((int)snapshot.Sequence),
            snapshot.IsOn ? (short)1 : (short)0);

        if (includeSetpoint)
            _server.UpdateFloatRegisters(CommonRegisterMap.Setpoint_High, (float)snapshot.SetpointKW);
        _server.UpdateFloatRegisters(CommonRegisterMap.BatteryStateOfCharge_High, (float)snapshot.StateOfChargePercent);
        _server.UpdateFloatRegisters(CommonRegisterMap.BatteryVoltage_High, (float)snapshot.VoltageV);
        var currentAmps = snapshot.VoltageV == 0 ? 0 : snapshot.ActivePowerKW * 1000d / snapshot.VoltageV;
        _server.UpdateFloatRegisters(CommonRegisterMap.BatteryCurrent_High, (float)currentAmps);
        _server.UpdateInt64Registers(CommonRegisterMap.BatteryVirtualTime_Tick0, snapshot.VirtualTime.Ticks);
    }
}
