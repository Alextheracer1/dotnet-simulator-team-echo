using WindTurbine_Simulator.Configuration;
using WindTurbine_Simulator.Domain;
using WindTurbine_Simulator.Modbus;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;
using TeamShared.Simulation;
using TeamShared.Weather;

namespace WindTurbine_Simulator.Simulation;

public sealed class WindTurbineSimulationRunner
{
    private readonly WindTurbineModel _model;
    private readonly SimulationOptions _options;
    private readonly WindTurbineModbusService _modbusService;
    private readonly IRabbitMqPublisher? _rabbitPublisher;
    private readonly IRabbitMqConsumer? _rabbitConsumer;
    private double? _rabbitPowerLimitKw;
    private bool _isFaulted;

    public bool RabbitMqConnected => _rabbitPublisher?.IsConnected ?? false;
    public long RabbitMqPublishedCount => _rabbitPublisher?.PublishedCount ?? 0;

    public WindTurbineSimulationRunner(
        WindTurbineModel model,
        SimulationOptions options,
        WindTurbineModbusService modbusService,
        IRabbitMqPublisher? rabbitPublisher = null,
        IRabbitMqConsumer? rabbitConsumer = null)
    {
        _model = model;
        _options = options;
        _modbusService = modbusService;
        _rabbitPublisher = rabbitPublisher;
        _rabbitConsumer = rabbitConsumer;
    }

    public async IAsyncEnumerable<SimulationSnapshot> RunAsync(
        ITimeMasterClient masterClient,
        WeatherCallbackReceiver? weatherReceiver,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_rabbitPublisher != null)
        {
            await _rabbitPublisher.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_rabbitConsumer != null)
        {
            await _rabbitConsumer.StartAsync(cancellationToken).ConfigureAwait(false);
            await _rabbitConsumer.SubscribeCommandsAsync("sim.commands.wind.#", HandleRemoteCommandAsync, cancellationToken).ConfigureAwait(false);
            await _rabbitConsumer.SubscribeCommandsAsync("sim.commands.*.wind-1", HandleRemoteCommandAsync, cancellationToken).ConfigureAwait(false);
        }

        var environment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("Der Simulation Master hat keine Startzeit geliefert.");
        var simulatedAt = ToUtcOffset(environment.VirtualTime);
        var startVirtualTime = simulatedAt;
        var masterTimeStep = environment.TimeStep > TimeSpan.Zero ? environment.TimeStep : _options.StepInterval;
        var isRunning = environment.IsRunning;
        var elapsed = TimeSpan.Zero;
        var step = 0;
        var cumulativeEnergyKwh = 0.0;
        var speedMultiplier = _options.SpeedMultiplier;
        Task<SimulationMessage?>? messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
        Task? connectionTask = null;

        using var controlTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        var controlTask = controlTimer.WaitForNextTickAsync(cancellationToken).AsTask();
        using var fallbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var fallbackTask = fallbackTimer.WaitForNextTickAsync(cancellationToken).AsTask();

        async Task<SimulationSnapshot> AdvanceAsync(DateTimeOffset targetTime, TimeSpan timeStep, WeatherSnapshotDto? weatherSnapshot = null)
        {
            step++;
            var windSpeedMps = weatherSnapshot != null
                ? CalculateWindSpeedFromSnapshot(weatherSnapshot)
                : GenerateSimulatedWindSpeedMps(targetTime);
            var rawPowerKw = await CalculateActivePowerKwAsync(windSpeedMps, cancellationToken).ConfigureAwait(false);
            var modbusLimit = _modbusService.ReadPowerLimitSetpoint();
            if (modbusLimit > 10000)
            {
                modbusLimit = modbusLimit / 1000f;
            }
            var limit = _rabbitPowerLimitKw.HasValue ? Math.Min(modbusLimit, (float)_rabbitPowerLimitKw.Value) : modbusLimit;
            var activePowerKw = _isFaulted ? 0.0 : Math.Min(rawPowerKw, limit);
            cumulativeEnergyKwh += activePowerKw * timeStep.TotalHours;
            var rotorRpm = _isFaulted ? 0.0 : windSpeedMps * 1.5;
            var pitchAngle = _isFaulted ? 90.0 : (activePowerKw >= _model.RatedPowerKw ? 12.0 : 0.0);
            var snapshot = new SimulationSnapshot(step, targetTime, windSpeedMps, activePowerKw, cumulativeEnergyKwh, speedMultiplier);
            _modbusService.Update(snapshot, rotorRpm, pitchAngle);

            if (_rabbitPublisher != null)
            {
                var telemetry = new DeviceTelemetryMessage
                {
                    DeviceId = "wind-1",
                    DeviceType = "wind",
                    Timestamp = DateTime.UtcNow,
                    VirtualTime = targetTime.ToUnixTimeSeconds(),
                    OperatingState = (short)(_isFaulted ? 3 : (snapshot.ActivePowerKw > 0 ? 2 : (snapshot.WindSpeedMps < 3.0 ? 0 : 2))),
                    ErrorCode = (short)(_isFaulted ? 1 : 0),
                    ActivePowerWatts = snapshot.ActivePowerKw * 1000.0,
                    CumulativeEnergyKwh = snapshot.CumulativeEnergyKwh,
                    IsOn = !_isFaulted,
                    ModbusPort = _options.ModbusPort,
                    ModbusUnitId = _options.ModbusUnitId
                };
                telemetry.SetMetric("WindSpeed", snapshot.WindSpeedMps);
                telemetry.SetMetric("RotorRpm", rotorRpm);
                telemetry.SetMetric("PitchAngle", pitchAngle);
                if (_rabbitPowerLimitKw.HasValue)
                {
                    telemetry.SetMetric("PowerLimitKw", _rabbitPowerLimitKw.Value);
                }

                await _rabbitPublisher.PublishTelemetryAsync(telemetry, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return snapshot;
        }

        while (!_options.Duration.HasValue || elapsed < _options.Duration.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var waitTasks = new List<Task> { controlTask, fallbackTask };
            if (messageTask is not null)
                waitTasks.Add(messageTask);
            if (connectionTask is not null)
                waitTasks.Add(connectionTask);

            var completed = await Task.WhenAny(waitTasks).ConfigureAwait(false);

            if (connectionTask is not null && completed == connectionTask)
            {
                await connectionTask.ConfigureAwait(false);
                connectionTask = null;
                var reconnectedEnvironment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false);
                if (reconnectedEnvironment is not null)
                {
                    simulatedAt = ToUtcOffset(reconnectedEnvironment.VirtualTime);
                    elapsed = Max(TimeSpan.Zero, simulatedAt - startVirtualTime);
                    masterTimeStep = reconnectedEnvironment.TimeStep > TimeSpan.Zero
                        ? reconnectedEnvironment.TimeStep
                        : masterTimeStep;
                    isRunning = reconnectedEnvironment.IsRunning;
                }
                messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
                continue;
            }

            if (completed == controlTask)
            {
                if (!await controlTask.ConfigureAwait(false))
                    yield break;
                speedMultiplier = ReadRuntimeControls(speedMultiplier);
                controlTask = controlTimer.WaitForNextTickAsync(cancellationToken).AsTask();
            }

            if (messageTask is not null && completed == messageTask)
            {
                var message = await messageTask.ConfigureAwait(false);
                messageTask = null;

                if (message is null)
                {
                    connectionTask ??= masterClient.ConnectWithRetryAsync(cancellationToken);
                }
                else if (message.Type == SimulationMessageTypes.Environment)
                {
                    simulatedAt = ToUtcOffset(message.VirtualTime);
                    elapsed = Max(TimeSpan.Zero, simulatedAt - startVirtualTime);
                    masterTimeStep = message.TimeStep > TimeSpan.Zero ? message.TimeStep : masterTimeStep;
                    isRunning = message.IsRunning;
                }
                else if (message.Type == SimulationMessageTypes.TimeStep)
                {
                    isRunning = message.IsRunning;
                    var targetTime = ToUtcOffset(message.VirtualTime);
                    var timeStep = message.TimeStep > TimeSpan.Zero
                        ? message.TimeStep
                        : targetTime - ToUtcOffset(message.PreviousVirtualTime);
                    if (timeStep <= TimeSpan.Zero)
                        timeStep = masterTimeStep;

                    masterTimeStep = timeStep;
                    simulatedAt = targetTime;
                    elapsed = Max(TimeSpan.Zero, simulatedAt - startVirtualTime);

                    WeatherSnapshotDto? weatherSnapshot = null;
                    if (weatherReceiver != null)
                    {
                        weatherSnapshot = await weatherReceiver.WaitForSnapshotAsync(
                            message.VirtualTime,
                            TimeSpan.FromSeconds(3),
                            cancellationToken).ConfigureAwait(false);
                    }

                    var snapshot = await AdvanceAsync(targetTime, timeStep, weatherSnapshot).ConfigureAwait(false);

                    try
                    {
                        await masterClient.SendAcknowledgementAsync(
                            message.Sequence,
                            message.VirtualTime,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException exception)
                    {
                        Console.Error.WriteLine($"[Simulation Master] ACK fehlgeschlagen: {exception.Message}");
                        connectionTask ??= masterClient.ConnectWithRetryAsync(cancellationToken);
                    }

                    yield return snapshot;
                }

                if (masterClient.IsConnected)
                    messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
            }

            if (completed == fallbackTask)
            {
                if (!await fallbackTask.ConfigureAwait(false))
                    yield break;

                if (!masterClient.IsConnected && isRunning)
                {
                    simulatedAt += masterTimeStep;
                    elapsed = Max(TimeSpan.Zero, simulatedAt - startVirtualTime);
                    var snapshot = await AdvanceAsync(simulatedAt, masterTimeStep, weatherReceiver?.LatestSnapshot).ConfigureAwait(false);
                    yield return snapshot;
                }

                fallbackTask = fallbackTimer.WaitForNextTickAsync(cancellationToken).AsTask();
            }
        }
    }

    private static double ReadRuntimeControls(double speedMultiplier)
    {
        while (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.P)
            {
                Console.WriteLine("Start/Pause wird über den Simulation Master gesteuert.");
            }
            else
            {
                speedMultiplier = HandleRuntimeKey(key, speedMultiplier);
            }
        }
        return speedMultiplier;
    }

    private static double HandleRuntimeKey(ConsoleKey key, double speedMultiplier)
    {
        return key switch
        {
            ConsoleKey.Add or ConsoleKey.OemPlus => Math.Min(speedMultiplier * 2, 1_000_000),
            ConsoleKey.Subtract or ConsoleKey.OemMinus => Math.Max(speedMultiplier / 2, 0.01),
            _ => speedMultiplier
        };
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first >= second ? first : second;

    private double CalculateWindSpeedFromSnapshot(WeatherSnapshotDto snapshot)
    {
        var span = _options.MaximumWindSpeedMps - _options.MinimumWindSpeedMps;
        var noise = (Random.Shared.NextDouble() * 2.0 - 1.0) * (span * 0.15);
        return Math.Clamp(snapshot.WindSpeedMps + noise, _options.MinimumWindSpeedMps, _options.MaximumWindSpeedMps);
    }

    private double GenerateSimulatedWindSpeedMps(DateTimeOffset simulatedAt)
    {
        var dayFactor = Math.Sin(((simulatedAt.TimeOfDay.TotalHours - 6.0) / 24.0) * 2.0 * Math.PI);
        var normalizedProfile = (dayFactor + 1.0) / 2.0;
        var span = _options.MaximumWindSpeedMps - _options.MinimumWindSpeedMps;
        var baseline = _options.MinimumWindSpeedMps + (span * normalizedProfile);
        var noise = (Random.Shared.NextDouble() * 2.0 - 1.0) * (span * 0.15);
        return Math.Clamp(baseline + noise, _options.MinimumWindSpeedMps, _options.MaximumWindSpeedMps);
    }

    private async Task<double> CalculateActivePowerKwAsync(double windSpeedMps, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return _model.CalculateActivePowerKw(windSpeedMps);
    }

    private Task HandleRemoteCommandAsync(DeviceCommandMessage cmd)
    {
        if (cmd.CommandType.Equals("SetPowerLimit", StringComparison.OrdinalIgnoreCase) ||
            cmd.CommandType.Equals("Setpoint", StringComparison.OrdinalIgnoreCase))
        {
            if (cmd.Value > 0)
            {
                var limitKw = cmd.Value > 1000 ? cmd.Value / 1000.0 : cmd.Value;
                _rabbitPowerLimitKw = limitKw;
            }
            else
            {
                _rabbitPowerLimitKw = null;
            }
        }
        else if (cmd.CommandType.Equals("ResetFault", StringComparison.OrdinalIgnoreCase))
        {
            _isFaulted = false;
        }
        else if (cmd.CommandType.Equals("TriggerFault", StringComparison.OrdinalIgnoreCase))
        {
            _isFaulted = true;
        }

        return Task.CompletedTask;
    }
}


public readonly record struct SimulationSnapshot(
    int Step,
    DateTimeOffset SimulatedAt,
    double WindSpeedMps,
    double ActivePowerKw,
    double CumulativeEnergyKwh,
    double SpeedMultiplier);