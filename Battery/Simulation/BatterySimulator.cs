using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Battery.Battery;
using Battery.ConsoleUi;
using Battery.Persistence;
using Battery.Simulation.Meters;
using TeamShared.Simulation;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

namespace Battery.Simulation;

public sealed class BatterySimulator
{
    private const string RabbitMqDeviceId = "battery-1";

    private enum DeviceSetupStage
    {
        None,
        DeviceCount,
        DevicePower
    }

    private readonly SimulationOptions _options;
    private readonly BatteryModel _battery;
    private readonly ConsoleDashboard _dashboard;
    private readonly SimulationMasterClient? _simulationMasterClient;
    private readonly IRabbitMqPublisher _rabbitPublisher;
    private readonly IRabbitMqConsumer _rabbitConsumer;
    private readonly List<DeviceLoad> _deviceLoads;
    private readonly Random _random = new();
    private readonly List<IEnergyMeter> _meters;
    private readonly MeterReadingObservable _meterUpdates = new();
    private readonly SimulationHistory _history = new();
    private DateTime? _lastAutoDeviceSelectionHour;
    private DateTime _currentSimulationTime;
    private ControlMode _mode = ControlMode.Manual;
    private double _manualTargetPowerKW;
    private bool _paused;
    private bool _quit;
    private bool _pausedBeforeDeviceSetup;
    private DeviceSetupStage _deviceSetupStage;
    private int _devicesToConfigure;
    private int _nextDeviceNumber = 1;
    private string? _notice;
    private SimulationEnvironment? _masterEnvironment;
    private DateTime? _lastAutoFaultChanceHour;
    private DateTime? _automaticFaultResetTime;
    private bool _hasProcessedMasterStep;
    private bool _masterControlsPause;
    private BatteryModbusSnapshot? _modbusSnapshot;
    private long _modbusSequence;
    private ChannelWriter<DeviceTelemetryMessage>? _rabbitTelemetryWriter;

    public IObservable<MeterReading> MeterUpdates => _meterUpdates;
    public SimulationHistory History => _history;

    public BatterySimulator(SimulationOptions options, SimulationMasterClient? simulationMasterClient = null)
    {
        options.Validate();
        _options = options;
        _currentSimulationTime = options.StartTime;
        _deviceLoads = AssignSocketNumbers(options.DeviceLoads);
        _simulationMasterClient = simulationMasterClient;
        _battery = new BatteryModel(
            options.CapacityKWh,
            options.MaximumChargePowerKW,
            options.MaximumDischargePowerKW,
            options.InitialStateOfChargePercent);
        _meters =
        [
            new BatteryPowerMeter(_battery),
            new DeviceDemandMeter(() => TotalDeviceDemandKW),
            new NetPowerMeter(GetInflowPowerKW, () => TotalDeviceDemandKW)
        ];
        _dashboard = new ConsoleDashboard(options.ModbusPort, options.ModbusUnitId);
        var rabbitMqConfiguration = RabbitMqConfiguration.CreateDefault();
        _rabbitPublisher = new RabbitMqPublisher(rabbitMqConfiguration);
        _rabbitConsumer = new RabbitMqConsumer(rabbitMqConfiguration);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var commands = Channel.CreateUnbounded<string>();
        var rabbitTelemetry = Channel.CreateBounded<DeviceTelemetryMessage>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
        _rabbitTelemetryWriter = rabbitTelemetry.Writer;
        using var rabbitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var rabbitWorkerTask = Task.Run(
            () => RunRabbitMqAsync(rabbitTelemetry.Reader, commands.Writer, rabbitCancellation.Token),
            CancellationToken.None);
        var inputTask = Task.Run(async () =>
        {
            try
            {
                await _dashboard.ReadCommandsAsync(commands.Writer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                commands.Writer.TryComplete();
            }
        }, CancellationToken.None);

        try
        {
            await using var logger = new CsvSimulationLogger(_options.CsvPath);
            var elapsed = TimeSpan.Zero;
            var simulatedTime = _currentSimulationTime;
            var meterReadings = await ReadMetersAsync(simulatedTime, cancellationToken);
            _history.RecordStateOfCharge(simulatedTime, _battery.StateOfChargePercent);
            PublishMeterReadings(meterReadings);
            await logger.WriteAsync(simulatedTime, _battery, meterReadings, cancellationToken);

            QueueRabbitTelemetry();

            PublishModbusSnapshot();
            await using var modbusService = new BatteryModbusService(
                _options.ModbusPort,
                _options.ModbusUnitId,
                GetModbusSnapshot,
                setpoint => commands.Writer.TryWrite($"setpoint {setpoint.ToString(CultureInfo.InvariantCulture)}"),
                () => commands.Writer.TryWrite("reset"));
            await modbusService.StartAsync(cancellationToken).ConfigureAwait(false);
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine($"Battery Modbus TCP server listening on port {_options.ModbusPort}, unit {_options.ModbusUnitId}.");
            }

            Task? connectionTask = _simulationMasterClient?.ConnectWithRetryAsync(cancellationToken);
            Task<SimulationMessage?>? masterMessageTask = null;
            var lastFallbackTimestamp = Stopwatch.GetTimestamp();
            var fallbackElapsed = TimeSpan.Zero;
            if (_simulationMasterClient is not null)
                _masterEnvironment = await _simulationMasterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false);

            var masterStatus = _simulationMasterClient is null ? "FALLBACK (NO MASTER)" : "CONNECTING";
            _masterControlsPause = false;
            var lastRenderTimestamp = Stopwatch.GetTimestamp();

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            var timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
            RenderDashboard(simulatedTime, elapsed, _notice, masterStatus);

            while (!cancellationToken.IsCancellationRequested && !_quit)
            {
                var waitTasks = new List<Task> { timerTask };
                if (masterMessageTask is not null)
                    waitTasks.Add(masterMessageTask);
                if (connectionTask is not null)
                    waitTasks.Add(connectionTask);

                var completed = await Task.WhenAny(waitTasks).ConfigureAwait(false);

                if (connectionTask is not null && completed == connectionTask)
                {
                    await connectionTask.ConfigureAwait(false);
                    connectionTask = null;
                    masterStatus = "CONNECTED";
                    _masterControlsPause = true;
                    await _simulationMasterClient!.SendStateAsync(CreateBatteryState(), cancellationToken).ConfigureAwait(false);
                    masterMessageTask = _simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                    continue;
                }

                if (masterMessageTask is not null && completed == masterMessageTask)
                {
                    var message = await masterMessageTask.ConfigureAwait(false);
                    masterMessageTask = null;

                    if (message is null)
                    {
                        masterStatus = "DISCONNECTED - LOCAL FALLBACK";
                        _masterControlsPause = false;
                        lastFallbackTimestamp = Stopwatch.GetTimestamp();
                        fallbackElapsed = TimeSpan.Zero;
                        if (connectionTask is null && _simulationMasterClient is not null)
                            connectionTask = _simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                    }
                    else
                    {
                        await ProcessMasterMessageAsync(message, logger, cancellationToken).ConfigureAwait(false);
                        simulatedTime = _currentSimulationTime;
                        elapsed = simulatedTime - _options.StartTime;
                        masterStatus = message.Type == SimulationMessageTypes.Environment
                            ? (message.IsRunning ? "RUNNING" : "PAUSED")
                            : "RUNNING";
                        if (_simulationMasterClient!.IsConnected)
                            masterMessageTask = _simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                        else
                        {
                            masterStatus = "DISCONNECTED - LOCAL FALLBACK";
                            _masterControlsPause = false;
                            lastFallbackTimestamp = Stopwatch.GetTimestamp();
                            fallbackElapsed = TimeSpan.Zero;
                            if (connectionTask is null)
                                connectionTask = _simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                        }
                    }
                }

                if (completed == timerTask)
                {
                    if (!await timerTask.ConfigureAwait(false))
                        break;

                    ProcessPendingCommands(commands.Reader, _masterControlsPause);
                    PublishModbusSnapshot();
                    var now = Stopwatch.GetTimestamp();

                    if (_simulationMasterClient is null || !_simulationMasterClient.IsConnected)
                    {
                        fallbackElapsed += Stopwatch.GetElapsedTime(lastFallbackTimestamp, now);
                        lastFallbackTimestamp = now;
                        _masterEnvironment ??= new SimulationEnvironment(
                            _currentSimulationTime,
                            0,
                            true,
                            TimeSpan.FromMinutes(30));
                        if (_paused)
                        {
                            fallbackElapsed = TimeSpan.Zero;
                        }
                        else
                        {
                            while (fallbackElapsed >= _options.RealTimeStepInterval)
                            {
                                fallbackElapsed -= _options.RealTimeStepInterval;
                                simulatedTime = _currentSimulationTime + _masterEnvironment.TimeStep;
                                await AdvanceBatteryToAsync(simulatedTime, logger, cancellationToken).ConfigureAwait(false);
                                elapsed = simulatedTime - _options.StartTime;
                            }
                        }
                    }

                    if (Stopwatch.GetElapsedTime(lastRenderTimestamp, now) >= TimeSpan.FromSeconds(1))
                    {
                        RenderDashboard(simulatedTime, elapsed, _notice, masterStatus);
                        lastRenderTimestamp = now;
                    }

                    timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                }
            }

            _dashboard.Render(simulatedTime, elapsed, _options.Duration, _mode, _battery,
                GetTargetPower(), _deviceLoads.Count,
                TotalDeviceDemandKW, _paused, _history.GetRollingSummary(), _notice, masterStatus);
        }
        finally
        {
            rabbitCancellation.Cancel();
            rabbitTelemetry.Writer.TryComplete();
            try
            {
                await rabbitWorkerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (rabbitCancellation.IsCancellationRequested)
            {
            }
            commands.Writer.TryComplete();
            await _rabbitConsumer.DisposeAsync().ConfigureAwait(false);
            await _rabbitPublisher.DisposeAsync().ConfigureAwait(false);
            _meterUpdates.Dispose();
            _ = inputTask;
        }
    }

    private Task<MeterReading[]> ReadMetersAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        return Task.WhenAll(_meters.Select(meter => meter.ReadAsync(timestamp, cancellationToken)));
    }

    private void PublishMeterReadings(IEnumerable<MeterReading> readings)
    {
        foreach (var reading in readings)
            _meterUpdates.Publish(reading);
    }

    private void RenderDashboard(
        DateTime simulatedTime,
        TimeSpan elapsed,
        string? notice,
        string? masterStatus = null)
    {
        _dashboard.Render(simulatedTime, elapsed, _options.Duration, _mode, _battery,
            GetTargetPower(), _deviceLoads.Count,
            TotalDeviceDemandKW, _paused, _history.GetRollingSummary(), notice, masterStatus);
    }

    private string GetMasterStatusText()
    {
        if (_simulationMasterClient is null)
        {
            return "OFFLINE";
        }

        if (!_simulationMasterClient.IsConnected)
        {
            return "WAITING";
        }

        return "CONNECTED";
    }

    private double TotalDeviceDemandKW => _deviceLoads.Sum(device => device.PowerDemandKW);

    private void UpdateAutoScenario(DateTime simulatedTime)
    {
        if (_mode != ControlMode.Auto)
            return;

        var hourStart = GetHourStart(simulatedTime);
        if (_lastAutoDeviceSelectionHour == hourStart)
            return;

        _lastAutoDeviceSelectionHour = hourStart;
        var previouslyConnected = _deviceLoads.ToArray();
        var selectedProfiles = AutoScenario.SelectDeviceProfiles(simulatedTime, _random);
        _deviceLoads.Clear();

        var vehicleIsConnected = AutoScenario.IsVehicleConnected(simulatedTime);
        if (vehicleIsConnected)
            TryConnectAutoDevice(new DeviceLoad("Auto charging", 2.3));

        if (vehicleIsConnected && previouslyConnected.All(device => device.Name != "Auto charging"))
        {
            foreach (var device in previouslyConnected)
                TryConnectAutoDevice(device);
        }

        foreach (var profile in selectedProfiles)
        {
            if (_deviceLoads.Any(device => device.Name == profile.Name))
                continue;

            TryConnectAutoDevice(profile);
        }

        _notice = $"Auto mode: {_deviceLoads.Count} device(s), {TotalDeviceDemandKW:F1} kW demand";
    }

    private bool TryConnectAutoDevice(DeviceLoad device)
    {
        if (_deviceLoads.Count >= SimulationOptions.MaximumDeviceCount ||
            TotalDeviceDemandKW + device.PowerDemandKW > SimulationOptions.MaximumTotalDeviceDemandKW)
            return false;

        _deviceLoads.Add(device with { SocketNumber = _deviceLoads.Count + 1 });
        return true;
    }

    private static DateTime GetHourStart(DateTime timestamp)
    {
        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0, timestamp.Kind);
    }

    private static List<DeviceLoad> AssignSocketNumbers(IEnumerable<DeviceLoad> deviceLoads)
    {
        var assignedLoads = deviceLoads.ToList();
        var occupiedSockets = assignedLoads
            .Where(device => device.SocketNumber > 0)
            .Select(device => device.SocketNumber)
            .ToHashSet();
        var nextSocketNumber = 1;

        for (var index = 0; index < assignedLoads.Count; index++)
        {
            if (assignedLoads[index].SocketNumber > 0)
                continue;

            while (!occupiedSockets.Add(nextSocketNumber))
                nextSocketNumber++;

            assignedLoads[index] = assignedLoads[index] with { SocketNumber = nextSocketNumber++ };
        }

        return assignedLoads;
    }

    private void BeginDeviceConfiguration(string dischargePowerArgument)
    {
        CancelDeviceConfiguration();
        _deviceLoads.Clear();
        _pausedBeforeDeviceSetup = _paused;
        _paused = true;
        _mode = ControlMode.Manual;
        _manualTargetPowerKW = string.IsNullOrWhiteSpace(dischargePowerArgument)
            ? 0
            : -ParsePower(dischargePowerArgument, 0);
        _deviceSetupStage = DeviceSetupStage.DeviceCount;
        _notice = "DISCHARGE: enter device count; press P to cancel and resume";
    }

    private void CancelDeviceConfiguration()
    {
        if (_deviceSetupStage == DeviceSetupStage.None)
            return;

        _deviceSetupStage = DeviceSetupStage.None;
        _devicesToConfigure = 0;
        _deviceLoads.Clear();
        _paused = _pausedBeforeDeviceSetup;
    }

    private void CompleteDeviceConfiguration()
    {
        _deviceSetupStage = DeviceSetupStage.None;
        _devicesToConfigure = 0;
        _paused = _pausedBeforeDeviceSetup;
        _notice = null;
    }

    private void DisconnectAllDevices()
    {
        CancelDeviceConfiguration();
        var disconnectedCount = _deviceLoads.Count;
        _deviceLoads.Clear();
        _notice = disconnectedCount == 0
            ? "No connected devices to unplug."
            : $"Unplugged all {disconnectedCount} connected device(s).";
    }

    private void ProcessDeviceSetupInput(string input)
    {
        if (_deviceSetupStage == DeviceSetupStage.DeviceCount)
        {
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
                count is < 0 or > SimulationOptions.MaximumDeviceCount)
            {
                _notice = $"Enter a whole number from 0 to {SimulationOptions.MaximumDeviceCount}, then press Enter";
                return;
            }

            _devicesToConfigure = count;
            _nextDeviceNumber = 1;
            if (count == 0)
            {
                CompleteDeviceConfiguration();
                _manualTargetPowerKW = 0;
                return;
            }

            _deviceSetupStage = DeviceSetupStage.DevicePower;
            _notice = $"Enter power demand in kW for device {_nextDeviceNumber}, then press Enter";
            return;
        }

        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var powerKW) ||
            !double.IsFinite(powerKW) || powerKW <= 0 ||
            !double.IsFinite(TotalDeviceDemandKW + powerKW) ||
            TotalDeviceDemandKW + powerKW > SimulationOptions.MaximumTotalDeviceDemandKW)
        {
            _notice = $"Enter a positive finite demand for device {_nextDeviceNumber} that keeps total demand at or below {SimulationOptions.MaximumTotalDeviceDemandKW} kW";
            return;
        }

        _deviceLoads.Add(new DeviceLoad($"Device {_nextDeviceNumber}", powerKW, _nextDeviceNumber));
        _devicesToConfigure--;
        _nextDeviceNumber++;
        if (_devicesToConfigure == 0)
        {
            CompleteDeviceConfiguration();
            return;
        }

        _notice = $"Enter power demand in kW for device {_nextDeviceNumber}, then press Enter";
    }

    private static bool IsControlCommand(string command)
    {
        return command is "quit" or "exit" or "toggle-pause" or "p" or "pause" or "resume" or "help" or
            "power-up" or "power-down" or "auto" or "manual" or
            "idle" or "fault" or "reset" or "discharge" or "disconnect" or "charge";
    }

    private double GetTargetPower()
    {
        if (_mode == ControlMode.Manual)
            return _manualTargetPowerKW;

        return AutoScenario.GetTargetPowerKW(_currentSimulationTime);
    }

    private double GetTargetPower(DateTime simulatedTime)
    {
        return _mode == ControlMode.Manual
            ? _manualTargetPowerKW
            : AutoScenario.GetTargetPowerKW(simulatedTime);
    }

    private double GetInflowPowerKW() => Math.Max(0, GetTargetPower());

    private void ProcessPendingCommands(ChannelReader<string> reader, bool masterControlsPause)
    {
        while (reader.TryRead(out var line))
        {
            _notice = null;
            var separator = line.IndexOf(' ');
            var command = (separator < 0 ? line : line[..separator]).Trim().ToLowerInvariant();
            var argument = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();

            if (_deviceSetupStage != DeviceSetupStage.None && !IsControlCommand(command))
            {
                ProcessDeviceSetupInput(line);
                continue;
            }

            try
            {
                switch (command)
                {
                    case "":
                        break;
                    case "help":
                        break;
                    case "toggle-pause":
                    case "p":
                        if (_deviceSetupStage != DeviceSetupStage.None)
                        {
                            CancelDeviceConfiguration();
                            _paused = false;
                            _notice = "Setup aborted and entries discarded";
                        }
                        else
                            _paused = !_paused;
                        break;
                    case "pause":
                        _paused = true;
                        break;
                    case "resume":
                        if (_deviceSetupStage == DeviceSetupStage.None)
                            _paused = false;
                        break;
                    case "auto":
                        CancelDeviceConfiguration();
                        _mode = ControlMode.Auto;
                        _lastAutoDeviceSelectionHour = null;
                        _lastAutoFaultChanceHour = GetHourStart(_currentSimulationTime);
                        UpdateAutoScenario(_currentSimulationTime);
                        break;
                    case "manual":
                        CancelDeviceConfiguration();
                        _mode = ControlMode.Manual;
                        _manualTargetPowerKW = 0;
                        break;
                    case "charge":
                        CancelDeviceConfiguration();
                        _manualTargetPowerKW = ParsePower(argument, _battery.MaximumChargePowerKW);
                        _mode = ControlMode.Manual;
                        break;
                    case "setpoint":
                        CancelDeviceConfiguration();
                        _manualTargetPowerKW = Math.Clamp(
                            ParseSignedPower(argument),
                            -_battery.MaximumDischargePowerKW,
                            _battery.MaximumChargePowerKW);
                        _mode = ControlMode.Manual;
                        break;
                    case "power-up":
                    case "power-down":
                        AdjustPowerTarget(command == "power-up" ? 1 : -1);
                        break;
                    case "discharge":
                        BeginDeviceConfiguration(argument);
                        break;
                    case "disconnect":
                        DisconnectAllDevices();
                        break;
                    case "idle":
                        CancelDeviceConfiguration();
                        _mode = ControlMode.Manual;
                        _manualTargetPowerKW = 0;
                        _deviceLoads.Clear();
                        break;
                    case "fault":
                        _battery.InjectFault(string.IsNullOrWhiteSpace(argument) ? "Operator-injected fault" : argument);
                        break;
                    case "reset":
                        _battery.ResetFault();
                        _automaticFaultResetTime = null;
                        break;
                    case "quit":
                    case "exit":
                        _quit = true;
                        break;
                    default:
                        _notice = $"Unknown command '{command}'. Type 'help' for commands.";
                        break;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
            {
                _notice = exception.Message;
            }
        }
    }

    private async Task ProcessMasterMessageAsync(SimulationMessage message, CsvSimulationLogger logger, CancellationToken cancellationToken)
    {
        if (message.Type == SimulationMessageTypes.Environment)
        {
            _masterEnvironment = new SimulationEnvironment(
                message.VirtualTime,
                message.Sequence,
                message.IsRunning,
                message.TimeStep);
            _paused = !message.IsRunning;
            if (message.VirtualTime != default && !_hasProcessedMasterStep)
            {
                _currentSimulationTime = message.VirtualTime;
                PublishModbusSnapshot();
            }

            return;
        }

        if (message.Type != SimulationMessageTypes.TimeStep)
            return;

        _paused = !message.IsRunning;
        if (message.IsRunning && message.VirtualTime > _currentSimulationTime)
        {
            await AdvanceBatteryToAsync(message.VirtualTime, logger, cancellationToken).ConfigureAwait(false);
            _hasProcessedMasterStep = true;
        }

        _masterEnvironment = new SimulationEnvironment(
            message.VirtualTime,
            message.Sequence,
            message.IsRunning,
            message.TimeStep);

        PublishModbusSnapshot();
        await _simulationMasterClient!.SendStateAsync(CreateBatteryState(message.Sequence), cancellationToken).ConfigureAwait(false);
        await _simulationMasterClient.SendAcknowledgementAsync(message.Sequence, _currentSimulationTime, cancellationToken).ConfigureAwait(false);
    }

    private async Task AdvanceBatteryToAsync(DateTime targetVirtualTime, CsvSimulationLogger logger, CancellationToken cancellationToken)
    {
        var delta = targetVirtualTime - _currentSimulationTime;
        if (delta <= TimeSpan.Zero)
            return;

        while (_currentSimulationTime < targetVirtualTime)
        {
            ResetAutomaticFaultIfDue(_currentSimulationTime);
            UpdateAutoScenario(_currentSimulationTime);
            ProcessAutomaticFaultChance(_currentSimulationTime);
            var segmentEnd = Min(targetVirtualTime, GetHourStart(_currentSimulationTime).AddHours(1));
            if (_automaticFaultResetTime is { } resetTime && resetTime > _currentSimulationTime)
                segmentEnd = Min(segmentEnd, resetTime);
            var targetAtStart = GetTargetPower(_currentSimulationTime);
            var targetAtEnd = GetTargetPower(segmentEnd);
            var averageTargetPowerKW = (targetAtStart + targetAtEnd) / 2;
            _battery.Advance(averageTargetPowerKW - TotalDeviceDemandKW, segmentEnd - _currentSimulationTime);
            _currentSimulationTime = segmentEnd;
        }

        ResetAutomaticFaultIfDue(_currentSimulationTime);
        var meterReadings = await ReadMetersAsync(_currentSimulationTime, cancellationToken);
        PublishMeterReadings(meterReadings);
        _history.RecordStateOfCharge(_currentSimulationTime, _battery.StateOfChargePercent);
        await logger.WriteAsync(_currentSimulationTime, _battery, meterReadings, cancellationToken);
        QueueRabbitTelemetry();
        PublishModbusSnapshot();
    }

    private Task QueueRabbitMqCommandAsync(DeviceCommandMessage command, ChannelWriter<string> commands)
    {
        if ((!string.Equals(command.TargetDeviceId, RabbitMqDeviceId, StringComparison.OrdinalIgnoreCase) && command.TargetDeviceId != "*") ||
            (!string.Equals(command.TargetDeviceType, "battery", StringComparison.OrdinalIgnoreCase) && command.TargetDeviceType != "*"))
        {
            return Task.CompletedTask;
        }

        string? commandLine = command.CommandType.ToLowerInvariant() switch
        {
            "setpoint" or "setpowerlimit" when double.IsFinite(command.Value) =>
                $"setpoint {(command.Value / 1000d).ToString("R", CultureInfo.InvariantCulture)}",
            "setmode" when string.Equals(command.StringPayload, "auto", StringComparison.OrdinalIgnoreCase) => "auto",
            "setmode" when string.Equals(command.StringPayload, "manual", StringComparison.OrdinalIgnoreCase) => "manual",
            "setmode" => command.Value > 0 ? "auto" : "manual",
            "triggerfault" => "fault RabbitMQ command",
            "resetfault" => "reset",
            _ => null
        };

        if (commandLine is not null)
            commands.TryWrite(commandLine);

        return Task.CompletedTask;
    }

    private void QueueRabbitTelemetry()
    {
        var writer = _rabbitTelemetryWriter;
        if (writer is null)
            return;

        const double batteryVoltage = 400;
        var isFaulted = _battery.State == BatteryState.Fault;
        var telemetry = new DeviceTelemetryMessage
        {
            DeviceId = RabbitMqDeviceId,
            DeviceType = "battery",
            Timestamp = DateTime.UtcNow,
            VirtualTime = ((DateTimeOffset)_currentSimulationTime).ToUnixTimeSeconds(),
            OperatingState = isFaulted ? (short)3 : _battery.CurrentPowerKW == 0 ? (short)0 : (short)2,
            ErrorCode = isFaulted ? (short)1 : (short)0,
            ActivePowerWatts = _battery.CurrentPowerKW * 1000d,
            CumulativeEnergyKwh = _battery.ChargedEnergyKWh + _battery.DischargedEnergyKWh,
            IsOn = true,
            ModbusPort = _options.ModbusPort,
            ModbusUnitId = _options.ModbusUnitId
        };

        telemetry.SetMetric("BatterySOC", _battery.StateOfChargePercent);
        telemetry.SetMetric("BatteryVoltage", batteryVoltage);
        telemetry.SetMetric("BatteryCurrent", _battery.CurrentPowerKW * 1000d / batteryVoltage);
        telemetry.SetMetric("ChargeEnergyKwh", _battery.ChargedEnergyKWh);
        telemetry.SetMetric("DischargeEnergyKwh", _battery.DischargedEnergyKWh);
        telemetry.SetMetric("SetpointKW", GetTargetPower());

        writer.TryWrite(telemetry);
    }

    private async Task RunRabbitMqAsync(
        ChannelReader<DeviceTelemetryMessage> telemetry,
        ChannelWriter<string> commands,
        CancellationToken cancellationToken)
    {
        var subscriptionRegistered = false;
        var publisherStarted = false;
        var consumerStarted = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!publisherStarted || !_rabbitPublisher.IsConnected)
                {
                    publisherStarted = await TryStartRabbitComponentAsync(
                        () => _rabbitPublisher.StartAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
                }

                if (!consumerStarted || !_rabbitConsumer.IsConnected)
                {
                    consumerStarted = await TryStartRabbitComponentAsync(
                        () => _rabbitConsumer.StartAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
                    if (!subscriptionRegistered)
                    {
                        await _rabbitConsumer.SubscribeCommandsAsync(
                            $"sim.commands.*.{RabbitMqDeviceId}",
                            command => QueueRabbitMqCommandAsync(command, commands),
                            cancellationToken).ConfigureAwait(false);
                        subscriptionRegistered = true;
                    }
                }

                if (_rabbitPublisher.IsConnected && telemetry.TryRead(out var message))
                {
                    await _rabbitPublisher.PublishTelemetryAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<bool> TryStartRabbitComponentAsync(Func<Task> start, CancellationToken cancellationToken)
    {
        try
        {
            await start().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;

    private void ProcessAutomaticFaultChance(DateTime simulatedTime)
    {
        if (_mode != ControlMode.Auto)
            return;

        var hourStart = GetHourStart(simulatedTime);
        if (_lastAutoFaultChanceHour == hourStart)
            return;

        _lastAutoFaultChanceHour = hourStart;
        if (_battery.State != BatteryState.Fault && _random.NextDouble() < 0.01)
        {
            _battery.InjectFault("Automatic error (1% hourly chance)");
            _automaticFaultResetTime = simulatedTime.AddMinutes(30);
            _notice = "Automatic error; repair is scheduled in 30 virtual minutes";
        }
    }

    private void ResetAutomaticFaultIfDue(DateTime simulatedTime)
    {
        if (_automaticFaultResetTime is not { } resetTime || simulatedTime < resetTime)
            return;

        _battery.ResetFault();
        _automaticFaultResetTime = null;
        _notice = "Automatic error repaired";
    }

    private void PublishModbusSnapshot()
    {
        var snapshot = new BatteryModbusSnapshot(
            _currentSimulationTime,
            Interlocked.Increment(ref _modbusSequence),
            _battery.State,
            true,
            _battery.CurrentPowerKW,
            _battery.ChargedEnergyKWh + _battery.DischargedEnergyKWh,
            _battery.StateOfChargePercent,
            400d,
            _mode == ControlMode.Manual ? _manualTargetPowerKW : GetTargetPower());

        Volatile.Write(ref _modbusSnapshot, snapshot);
    }

    private BatteryModbusSnapshot GetModbusSnapshot()
    {
        return Volatile.Read(ref _modbusSnapshot)
            ?? throw new InvalidOperationException("Battery Modbus snapshot has not been initialized.");
    }

    private SimulationMessage CreateBatteryState(long sequence = 0)
    {
        return new SimulationMessage
        {
            Type = SimulationMessageTypes.State,
            StationId = Environment.MachineName + "-Battery",
            Sequence = sequence,
            VirtualTime = _currentSimulationTime,
            TimeStep = _simulationMasterClient?.LastKnownTimeStep ?? TimeSpan.FromSeconds(1),
            IsRunning = !_paused,
            IsOn = true,
            ActivePowerKW = _battery.CurrentPowerKW,
            CumulativeEnergyKWh = _battery.ChargedEnergyKWh - _battery.DischargedEnergyKWh,
            StateOfChargePercent = _battery.StateOfChargePercent,
            SetpointKW = GetTargetPower()
        };
    }

    private void AdjustPowerTarget(double adjustmentKW)
    {
        var currentTarget = GetTargetPower();
        _manualTargetPowerKW = Math.Clamp(
            currentTarget + adjustmentKW,
            -_battery.MaximumDischargePowerKW,
            _battery.MaximumChargePowerKW);
        _mode = ControlMode.Manual;
    }

    private static double ParsePower(string argument, double defaultValue)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return defaultValue;

        var power = ParsePositiveNumber(argument, "power");
        return power;
    }

    private static double ParseSignedPower(string argument)
    {
        if (!double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException("Provide a finite signed power value using invariant numeric format.");

        return value;
    }

    private static double ParsePositiveNumber(string argument, string name)
    {
        if (!double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value) || value <= 0)
            throw new ArgumentException($"Provide a positive finite {name} value using invariant numeric format.");

        return value;
    }
}
