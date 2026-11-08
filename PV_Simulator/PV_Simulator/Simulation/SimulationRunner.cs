using System;
using System.Threading;
using System.Threading.Tasks;
using PV_Simulator.ConsoleApp;
using PV_Simulator.Domain;
using PV_Simulator.Modbus;
using TeamShared.Simulation;
using TeamShared.Weather;

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
        ITimeMasterClient masterClient,
        WeatherCallbackReceiver? weatherReceiver = null,
        PvModbusService? modbusService = null,
        CancellationToken cancellationToken = default)
    {
        await masterClient.ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
        var initialEnvironment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("Der Simulation Master hat keine Startzeit geliefert.");
        var currentVirtualTime = ToUtcOffset(initialEnvironment.VirtualTime);
        var startVirtualTime = currentVirtualTime;
        var masterTimeStep = initialEnvironment.TimeStep > TimeSpan.Zero
            ? initialEnvironment.TimeStep
            : TimeSpan.FromSeconds(1);
        var isRunning = initialEnvironment.IsRunning;
        var elapsed = TimeSpan.Zero;
        Task<SimulationMessage?>? messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
        Task? connectionTask = null;

        using var fallbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var fallbackTick = fallbackTimer.WaitForNextTickAsync(cancellationToken).AsTask();

        async Task AdvanceAndLogAsync(DateTimeOffset targetTime, TimeSpan timeStep, WeatherSnapshotDto? externalWeather = null)
        {
            currentVirtualTime = targetTime;
            elapsed = targetTime - startVirtualTime;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            var snapshot = engine.AdvanceTo(targetTime, timeStep, externalWeather);
            modbusService?.Update(snapshot);
            var speedMultiplier = timeStep.TotalSeconds;
            var totalSteps = settings.Duration.HasValue ? GetTotalSteps(settings.Duration.Value, timeStep) : 0;
            renderer.Render(
                snapshot,
                location,
                array,
                inverter,
                settings.Duration,
                speedMultiplier,
                totalSteps,
                GetSelectedWeatherControlName(),
                timeStep);
            await csvLogger.WriteAsync(snapshot, location.Name, cancellationToken);
        }

        while (!settings.Duration.HasValue || elapsed < settings.Duration.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var waitTasks = new List<Task> { fallbackTick };
            if (messageTask is not null)
                waitTasks.Add(messageTask);
            if (connectionTask is not null)
                waitTasks.Add(connectionTask);

            var completed = await Task.WhenAny(waitTasks).ConfigureAwait(false);

            if (connectionTask is not null && completed == connectionTask)
            {
                await connectionTask.ConfigureAwait(false);
                connectionTask = null;
                var environment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false);
                if (environment is not null)
                {
                    currentVirtualTime = ToUtcOffset(environment.VirtualTime);
                    masterTimeStep = environment.TimeStep > TimeSpan.Zero ? environment.TimeStep : masterTimeStep;
                    isRunning = environment.IsRunning;
                    elapsed = Max(TimeSpan.Zero, currentVirtualTime - startVirtualTime);
                }
                messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
                continue;
            }

            ReadRuntimeControls(engine, cancellationToken);

            if (messageTask is not null && completed == messageTask)
            {
                var message = await messageTask.ConfigureAwait(false);
                messageTask = null;

                if (message is null)
                {
                    if (connectionTask is null)
                        connectionTask = masterClient.ConnectWithRetryAsync(cancellationToken);
                }
                else if (message.Type == SimulationMessageTypes.Environment)
                {
                    currentVirtualTime = ToUtcOffset(message.VirtualTime);
                    masterTimeStep = message.TimeStep > TimeSpan.Zero ? message.TimeStep : masterTimeStep;
                    isRunning = message.IsRunning;
                    elapsed = Max(TimeSpan.Zero, currentVirtualTime - startVirtualTime);
                    messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
                }
                else if (message.Type == SimulationMessageTypes.TimeStep)
                {
                    currentVirtualTime = ToUtcOffset(message.VirtualTime);
                    masterTimeStep = message.TimeStep > TimeSpan.Zero ? message.TimeStep : masterTimeStep;
                    isRunning = message.IsRunning;

                    WeatherSnapshotDto? snapshotDto = null;
                    if (weatherReceiver != null)
                    {
                        snapshotDto = await weatherReceiver.WaitForSnapshotAsync(
                            message.VirtualTime,
                            TimeSpan.FromSeconds(3),
                            cancellationToken).ConfigureAwait(false);
                    }

                    await AdvanceAndLogAsync(currentVirtualTime, masterTimeStep, snapshotDto).ConfigureAwait(false);

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
                        if (connectionTask is null)
                            connectionTask = masterClient.ConnectWithRetryAsync(cancellationToken);
                    }

                    if (masterClient.IsConnected)
                        messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
                }
                else
                {
                    messageTask = masterClient.WaitForNextMessageAsync(cancellationToken);
                }
            }

            if (completed == fallbackTick)
            {
                if (!await fallbackTick.ConfigureAwait(false))
                    break;

                if (!masterClient.IsConnected && isRunning)
                    await AdvanceAndLogAsync(currentVirtualTime + masterTimeStep, masterTimeStep, weatherReceiver?.LatestSnapshot).ConfigureAwait(false);

                fallbackTick = fallbackTimer.WaitForNextTickAsync(cancellationToken).AsTask();
            }
        }
    }

    private void ReadRuntimeControls(SimulationEngine engine, CancellationToken cancellationToken)
    {
        while (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.P)
            {
                Console.WriteLine("Pause und Fortsetzen werden vom Simulation Master gesteuert.");
            }
            else
            {
                HandleRuntimeKey(key, engine);
            }
        }
    }

    private void HandleRuntimeKey(ConsoleKey key, SimulationEngine engine)
    {
        switch (key)
        {
            case ConsoleKey.C:
                _selectedWeatherControl = WeatherControlSelection.CloudCover;
                Console.WriteLine("Wolkenbedeckung ausgewÃ¤hlt; +/- Ã¤ndert sie in 10-Prozentpunkt-Schritten.");
                break;
            case ConsoleKey.T:
                _selectedWeatherControl = WeatherControlSelection.Temperature;
                Console.WriteLine("Temperatur ausgewÃ¤hlt; +/- Ã¤ndert sie in 2-Â°C-Schritten.");
                break;
            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                if (_selectedWeatherControl == WeatherControlSelection.CloudCover)
                {
                    Console.WriteLine($"Wolkenbedeckung: {engine.AdjustCloudCover(1):P0}");
                }
                else if (_selectedWeatherControl == WeatherControlSelection.Temperature)
                {
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(1):0.0} Â°C");
                }
                else
                {
                    Console.WriteLine("Schrittweite und Tempo werden vom Simulation Master gesteuert.");
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
                    Console.WriteLine($"Temperatur: {engine.AdjustTemperature(-1):0.0} Â°C");
                }
                else
                {
                    Console.WriteLine("Schrittweite und Tempo werden vom Simulation Master gesteuert.");
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
                Console.WriteLine($"Manuelle WechselrichterstÃ¶rung {(engine.ToggleManualFault() ? "aktiviert" : "deaktiviert")}.");
                break;
            case ConsoleKey.S:
                Console.WriteLine($"PV-Module {(engine.ToggleSnowCover() ? "verschneit" : "schneefrei")}.");
                break;
        }
    }

    private static int GetTotalSteps(TimeSpan duration, TimeSpan timeStep) =>
        Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds / timeStep.TotalSeconds));

    private static DateTimeOffset ToUtcOffset(DateTime virtualTime) =>
        new(DateTime.SpecifyKind(virtualTime, DateTimeKind.Utc));

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

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
