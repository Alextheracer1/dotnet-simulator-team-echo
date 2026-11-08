using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;
using TeamShared;
using TeamShared.Simulation;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

namespace PV_Simulator.Simulation;

public sealed class SimulationRunner(ConsoleStatusRenderer renderer)
{
    private WeatherControlSelection _selectedWeatherControl;

    public async Task RunAsync(
        SimulationEngine engine,
        Location location,
        PvArray array,
        Inverter inverter,
        SimulationSettings settings,
        CsvSimulationLogger csvLogger,
        CancellationToken cancellationToken = default,
        SimulationMasterClient? simulationMasterClient = null)
    {
        var elapsed = TimeSpan.Zero;
        var speedMultiplier = settings.SpeedMultiplier;
        var totalSteps = (int)Math.Ceiling(settings.Duration.TotalSeconds / settings.TimeStep.TotalSeconds);
        var localAccumulator = TimeSpan.Zero;
        var lastClockTimestamp = Stopwatch.GetTimestamp();
        var locallyPaused = false;
        var masterConnected = false;
        var hasProcessedMasterStep = false;
        DateTimeOffset? masterStartTime = null;
        DateTimeOffset? currentSimulationTime = null;
        SimulationSnapshot? latestSnapshot = null;
        var rabbitCommands = Channel.CreateUnbounded<DeviceCommandMessage>();
        var rabbitTelemetry = Channel.CreateBounded<DeviceTelemetryMessage>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
        var rabbitConfiguration = RabbitMqConfiguration.CreateDefault();
        await using var rabbitPublisher = new RabbitMqPublisher(rabbitConfiguration);
        await using var rabbitConsumer = new RabbitMqConsumer(rabbitConfiguration);
        using var rabbitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var rabbitWorkerTask = Task.Run(
            () => RunRabbitMqAsync(
                rabbitTelemetry.Reader,
                rabbitCommands.Writer,
                rabbitPublisher,
                rabbitConsumer,
                rabbitConfiguration.BuildCommandRoutingKey("*", array.DeviceId),
                rabbitCancellation.Token),
            CancellationToken.None);

        Task? connectionTask = simulationMasterClient?.ConnectWithRetryAsync(cancellationToken);
        Task<SimulationMessage?>? masterMessageTask = null;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();

        try
        {
            while (elapsed < settings.Duration && !cancellationToken.IsCancellationRequested)
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
                masterConnected = true;
                simulationMasterClient!.UpdateStationTime(currentSimulationTime?.UtcDateTime ?? settings.StartTime.UtcDateTime);
                masterMessageTask = simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                continue;
            }

            if (masterMessageTask is not null && completed == masterMessageTask)
            {
                var message = await masterMessageTask.ConfigureAwait(false);
                masterMessageTask = null;
                if (message is null)
                {
                    masterConnected = false;
                    localAccumulator = TimeSpan.Zero;
                    lastClockTimestamp = Stopwatch.GetTimestamp();
                    if (connectionTask is null && simulationMasterClient is not null)
                        connectionTask = simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                }
                else
                {
                    if (message.Type == SimulationMessageTypes.Environment)
                    {
                        if (!hasProcessedMasterStep && message.VirtualTime != default && currentSimulationTime is null)
                        {
                            currentSimulationTime = AsUtcDateTimeOffset(message.VirtualTime);
                            masterStartTime = currentSimulationTime;
                            simulationMasterClient!.UpdateStationTime(message.VirtualTime);
                        }
                    }
                    else if (message.Type == SimulationMessageTypes.TimeStep)
                    {
                        var targetTime = AsUtcDateTimeOffset(message.VirtualTime);
                        if (currentSimulationTime is null)
                        {
                            currentSimulationTime = message.PreviousVirtualTime == default
                                ? targetTime - message.TimeStep
                                : AsUtcDateTimeOffset(message.PreviousVirtualTime);
                            masterStartTime = currentSimulationTime;
                        }

                        var masterTimeStep = targetTime - currentSimulationTime.Value;
                        if (masterTimeStep > TimeSpan.Zero)
                        {
                            latestSnapshot = engine.Advance(targetTime, masterTimeStep);
                            currentSimulationTime = targetTime;
                            masterStartTime ??= currentSimulationTime - masterTimeStep;
                            elapsed = currentSimulationTime.Value - masterStartTime.Value;
                            await csvLogger.WriteAsync(latestSnapshot, location.Name, cancellationToken).ConfigureAwait(false);
                            QueueRabbitTelemetry(rabbitTelemetry.Writer, latestSnapshot, array.DeviceId);
                            Render(latestSnapshot);
                        }

                        hasProcessedMasterStep = true;
                        simulationMasterClient!.UpdateStationTime(targetTime.UtcDateTime);
                        await simulationMasterClient.SendStateAsync(new SimulationMessage
                        {
                            VirtualTime = targetTime.UtcDateTime,
                            Sequence = message.Sequence,
                            IsOn = latestSnapshot?.InverterStatus == InverterStatus.Running,
                            Status = latestSnapshot?.InverterStatus.ToString(),
                            ActivePowerKW = latestSnapshot?.PowerKilowatts,
                            CumulativeEnergyKWh = latestSnapshot?.DailyEnergyKilowattHours
                        }, cancellationToken).ConfigureAwait(false);
                        await simulationMasterClient.SendAcknowledgementAsync(
                            message.Sequence,
                            targetTime.UtcDateTime,
                            cancellationToken).ConfigureAwait(false);
                    }

                    if (simulationMasterClient!.IsConnected)
                        masterMessageTask = simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                    else
                    {
                        masterConnected = false;
                        localAccumulator = TimeSpan.Zero;
                        lastClockTimestamp = Stopwatch.GetTimestamp();
                        if (connectionTask is null)
                            connectionTask = simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                    }
                }
            }

            if (completed != timerTask)
                continue;

            if (!await timerTask.ConfigureAwait(false))
                break;

            var now = Stopwatch.GetTimestamp();
            var wallElapsed = Stopwatch.GetElapsedTime(lastClockTimestamp, now);
            lastClockTimestamp = now;
            ProcessRuntimeControls(engine, ref speedMultiplier, ref locallyPaused, masterConnected);
            ProcessRabbitCommands(rabbitCommands.Reader, engine, array.DeviceId);

            if (!masterConnected && !locallyPaused)
            {
                localAccumulator += TimeSpan.FromTicks((long)(wallElapsed.Ticks * speedMultiplier));
                while (elapsed < settings.Duration
                    && localAccumulator >= (settings.TimeStep <= settings.Duration - elapsed
                        ? settings.TimeStep
                        : settings.Duration - elapsed))
                {
                    var stepDuration = settings.TimeStep <= settings.Duration - elapsed
                        ? settings.TimeStep
                        : settings.Duration - elapsed;
                    latestSnapshot = engine.Advance(elapsed);
                    elapsed += stepDuration;
                    currentSimulationTime = latestSnapshot.SimulatedAt;
                    masterStartTime ??= settings.StartTime;
                    localAccumulator -= stepDuration;
                    simulationMasterClient?.UpdateStationTime(currentSimulationTime.Value.UtcDateTime);
                    await csvLogger.WriteAsync(latestSnapshot, location.Name, cancellationToken).ConfigureAwait(false);
                    QueueRabbitTelemetry(rabbitTelemetry.Writer, latestSnapshot, array.DeviceId);
                    Render(latestSnapshot);
                }
            }

            timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
            }
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
        }

        void Render(SimulationSnapshot snapshot) => renderer.Render(
            snapshot,
            location,
            array,
            inverter,
            settings,
            settings.Duration,
            speedMultiplier,
            totalSteps,
            GetSelectedWeatherControlName());
    }

    private void ProcessRuntimeControls(
        SimulationEngine engine,
        ref double speedMultiplier,
        ref bool locallyPaused,
        bool masterConnected)
    {
        while (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.P)
            {
                if (masterConnected)
                {
                    Console.WriteLine("Simulation master controls the clock; use the master to pause or resume.");
                }
                else
                    locallyPaused = !locallyPaused;
            }
            else
            {
                speedMultiplier = HandleRuntimeKey(key, engine, speedMultiplier);
            }
        }
    }

    private static DateTimeOffset AsUtcDateTimeOffset(DateTime dateTime) =>
        new(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));

    private static void ProcessRabbitCommands(
        ChannelReader<DeviceCommandMessage> commands,
        SimulationEngine engine,
        string deviceId)
    {
        while (commands.TryRead(out var command))
        {
            if ((!string.Equals(command.TargetDeviceId, deviceId, StringComparison.OrdinalIgnoreCase) && command.TargetDeviceId != "*") ||
                (!string.Equals(command.TargetDeviceType, "pv", StringComparison.OrdinalIgnoreCase) && command.TargetDeviceType != "*"))
            {
                continue;
            }

            switch (command.CommandType.ToLowerInvariant())
            {
                case "triggerfault":
                    engine.SetManualFault(true);
                    break;
                case "resetfault":
                    engine.SetManualFault(false);
                    break;
                case "togglemanualfault":
                    engine.ToggleManualFault();
                    break;
                case "setsnowcover":
                    engine.SetSnowCover(command.Value > 0);
                    break;
                case "togglesnowcover":
                    engine.ToggleSnowCover();
                    break;
                case "togglemanualrain":
                case "togglerain":
                    engine.ToggleManualRain();
                    break;
                case "adjustcloudcover" when double.IsFinite(command.Value) && command.Value != 0:
                    engine.AdjustCloudCover(command.Value > 0 ? 1 : -1);
                    break;
                case "adjusttemperature" when double.IsFinite(command.Value) && command.Value != 0:
                    engine.AdjustTemperature(command.Value > 0 ? 1 : -1);
                    break;
            }
        }
    }

    private static void QueueRabbitTelemetry(
        ChannelWriter<DeviceTelemetryMessage> telemetryWriter,
        SimulationSnapshot snapshot,
        string deviceId)
    {
        var isFaulted = snapshot.InverterStatus == InverterStatus.Faulted;
        var telemetry = new DeviceTelemetryMessage
        {
            DeviceId = deviceId,
            DeviceType = "pv",
            Timestamp = DateTime.UtcNow,
            VirtualTime = snapshot.SimulatedAt.ToUnixTimeSeconds(),
            OperatingState = isFaulted ? (short)3 : snapshot.InverterStatus == InverterStatus.Running ? (short)2 : (short)0,
            ErrorCode = isFaulted ? (short)1 : (short)0,
            ActivePowerWatts = snapshot.PowerKilowatts * 1000d,
            CumulativeEnergyKwh = snapshot.DailyEnergyKilowattHours,
            IsOn = snapshot.InverterStatus == InverterStatus.Running
        };

        telemetry.SetMetric("IrradianceWm2", snapshot.IrradianceWattsPerSquareMeter);
        telemetry.SetMetric("CloudCoverFraction", snapshot.Weather.CloudCoverFraction);
        telemetry.SetMetric("AmbientTemperatureCelsius", snapshot.Weather.AmbientTemperatureCelsius);
        telemetry.SetMetric("DailyEnergyKwh", snapshot.DailyEnergyKilowattHours);
        telemetry.SetMetric("SnowCoverActive", snapshot.SnowCoverActive ? 1 : 0);
        telemetry.SetMetric("ManualFaultActive", snapshot.ManualFaultActive ? 1 : 0);
        telemetry.SetMetric("Precipitation", (double)snapshot.Weather.Precipitation);

        telemetryWriter.TryWrite(telemetry);
    }

    private static async Task RunRabbitMqAsync(
        ChannelReader<DeviceTelemetryMessage> telemetry,
        ChannelWriter<DeviceCommandMessage> commands,
        IRabbitMqPublisher publisher,
        IRabbitMqConsumer consumer,
        string commandRoutingKey,
        CancellationToken cancellationToken)
    {
        var subscriptionRegistered = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!publisher.IsConnected)
                    await TryStartRabbitComponentAsync(() => publisher.StartAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

                if (!consumer.IsConnected)
                    await TryStartRabbitComponentAsync(() => consumer.StartAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

                if (!subscriptionRegistered)
                {
                    await consumer.SubscribeCommandsAsync(
                        commandRoutingKey,
                        command =>
                        {
                            commands.TryWrite(command);
                            return Task.CompletedTask;
                        },
                        cancellationToken).ConfigureAwait(false);
                    subscriptionRegistered = true;
                }

                if (publisher.IsConnected && telemetry.TryRead(out var message))
                {
                    await publisher.PublishTelemetryAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task TryStartRabbitComponentAsync(Func<Task> start, CancellationToken cancellationToken)
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
    }

    private double HandleRuntimeKey(ConsoleKey key, SimulationEngine engine, double speedMultiplier)
    {
        switch (key)
        {
            case ConsoleKey.C:
                _selectedWeatherControl = WeatherControlSelection.CloudCover;
                Console.WriteLine("Wolkenbedeckung ausgewählt; +/- ändert sie in 10-Prozentpunkt-Schritten.");
                break;
            case ConsoleKey.T:
                _selectedWeatherControl = WeatherControlSelection.Temperature;
                Console.WriteLine("Temperatur ausgewählt; +/- ändert sie in 2-°C-Schritten.");
                break;
            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    Console.WriteLine($"Wolkenbedeckung: {engine.AdjustCloudCover(1):P0}");
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(1):0.0} °C");
                }
                else
                {
                    speedMultiplier = Math.Min(speedMultiplier * 2, 1_000_000);
                }

                break;
            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    Console.WriteLine($"Wolkenbedeckung: {engine.AdjustCloudCover(-1):P0}");
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(-1):0.0} °C");
                }
                else
                {
                    speedMultiplier = Math.Max(speedMultiplier / 2, 0.01);
                }

                break;
            case ConsoleKey.A:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    engine.ResetCloudCoverOverride();
                    Console.WriteLine("Wolkenbedeckung folgt wieder der Automatik.");
                    _selectedWeatherControl = WeatherControlSelection.None;
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    engine.ResetTemperatureOverride();
                    Console.WriteLine("Temperatur folgt wieder der Automatik.");
                    _selectedWeatherControl = WeatherControlSelection.None;
                }

                break;
            case ConsoleKey.R:
                Console.WriteLine($"Manueller Regen {(engine.ToggleManualRain() ? "aktiviert" : "deaktiviert")}.");
                break;
            case ConsoleKey.F:
                Console.WriteLine($"Manuelle Wechselrichterstörung {(engine.ToggleManualFault() ? "aktiviert" : "deaktiviert")}.");
                break;
            case ConsoleKey.S:
                Console.WriteLine($"PV-Module {(engine.ToggleSnowCover() ? "verschneit" : "schneefrei")}.");
                break;
        }

        return speedMultiplier;
    }

    private string GetSelectedWeatherControlName() => _selectedWeatherControl switch
    {
        WeatherControlSelection.CloudCover => "Wolken",
        WeatherControlSelection.Temperature => "Temperatur",
        _ => "keine ( +/− regeln die Geschwindigkeit)"
    };

    private enum WeatherControlSelection
    {
        None,
        CloudCover,
        Temperature
    }
}