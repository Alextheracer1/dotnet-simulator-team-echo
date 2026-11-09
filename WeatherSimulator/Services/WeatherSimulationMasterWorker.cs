using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamShared.Simulation;

namespace WeatherSimulator.Services;

public sealed class WeatherSimulationMasterWorker(
    ITimeMasterClient masterClient,
    WeatherSimulationClock clock,
    IWeatherSnapshotPusher weatherPusher,
    IWeatherActivityLogger? activityLogger = null,
    ILogger<WeatherSimulationMasterWorker>? logger = null) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var fallbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var fallbackTick = fallbackTimer.WaitForNextTickAsync(stoppingToken).AsTask();
        Task<SimulationMessage?>? messageTask = masterClient.IsConnected ? masterClient.WaitForNextMessageAsync(stoppingToken) : null;
        Task? connectionTask = masterClient.IsConnected ? null : masterClient.ConnectWithRetryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var tasks = new List<Task> { fallbackTick };
            if (messageTask is not null)
                tasks.Add(messageTask);
            if (connectionTask is not null)
                tasks.Add(connectionTask);

            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);

            if (connectionTask is not null && completed == connectionTask)
            {
                await connectionTask.ConfigureAwait(false);
                connectionTask = null;
                clock.SetMasterConnected(true);
                var applied = await ApplyCurrentEnvironmentAsync(stoppingToken).ConfigureAwait(false);
                if (applied)
                {
                    activityLogger?.Log($"[chartreuse3]Master-Sync:[/] Verbunden. Virtuelle Zeit: {clock.VirtualTime:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    activityLogger?.Log("[yellow]Master-Sync:[/] Verbunden, warte auf Initial-Umgebung...");
                }
                messageTask = masterClient.WaitForNextMessageAsync(stoppingToken);
                continue;
            }

            if (messageTask is not null && completed == messageTask)
            {
                var message = await messageTask.ConfigureAwait(false);
                messageTask = null;
                if (message is null)
                {
                    clock.SetMasterConnected(false);
                    activityLogger?.Log("[red]Master-Sync:[/] Verbindung verloren! Reconnect läuft...");
                    logger?.LogWarning("Verbindung zum Simulation Master verloren; virtuelle Zeit läuft mit dem letzten Zeitschritt weiter.");
                    connectionTask ??= masterClient.ConnectWithRetryAsync(stoppingToken);
                }
                else
                {
                    clock.ApplyMasterMessage(message);
                    if (message.Type == SimulationMessageTypes.TimeStep)
                    {
                        try
                        {
                            await weatherPusher.PushSnapshotsAsync(message.VirtualTime, stoppingToken).ConfigureAwait(false);

                            await masterClient.SendAcknowledgementAsync(
                                message.Sequence,
                                message.VirtualTime,
                                stoppingToken).ConfigureAwait(false);
                        }
                        catch (IOException exception)
                        {
                            clock.SetMasterConnected(false);
                            activityLogger?.Log($"[red]Master-Sync:[/] ACK fehlgeschlagen ({exception.Message})");
                            logger?.LogWarning(exception, "ACK an den Simulation Master fehlgeschlagen.");
                            connectionTask ??= masterClient.ConnectWithRetryAsync(stoppingToken);
                        }
                    }

                    if (masterClient.IsConnected)
                        messageTask = masterClient.WaitForNextMessageAsync(stoppingToken);
                }
            }

            if (completed == fallbackTick)
            {
                if (!await fallbackTick.ConfigureAwait(false))
                    break;

                if (!masterClient.IsConnected)
                    clock.AdvanceFallback();

                fallbackTick = fallbackTimer.WaitForNextTickAsync(stoppingToken).AsTask();
            }
        }
    }

    private async Task<bool> ApplyCurrentEnvironmentAsync(CancellationToken cancellationToken)
    {
        var environment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false);
        if (environment is null || environment.VirtualTime == default)
            return false;

        clock.ApplyMasterMessage(new SimulationMessage
        {
            Type = SimulationMessageTypes.Environment,
            VirtualTime = environment.VirtualTime,
            Sequence = environment.Sequence,
            IsRunning = environment.IsRunning,
            TimeStep = environment.TimeStep
        });
        return clock.Current is not null;
    }
}
