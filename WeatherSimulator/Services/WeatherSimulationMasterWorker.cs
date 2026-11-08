using TeamShared.Simulation;

namespace WeatherSimulator.Services;

public sealed class WeatherSimulationMasterWorker(
    ITimeMasterClient masterClient,
    WeatherSimulationClock clock,
    ILogger<WeatherSimulationMasterWorker> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await masterClient.ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
        await ApplyCurrentEnvironmentAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var fallbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var fallbackTick = fallbackTimer.WaitForNextTickAsync(stoppingToken).AsTask();
        var messageTask = masterClient.WaitForNextMessageAsync(stoppingToken);
        Task? connectionTask = null;

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
                await ApplyCurrentEnvironmentAsync(stoppingToken).ConfigureAwait(false);
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
                    logger.LogWarning("Verbindung zum Simulation Master verloren; virtuelle Zeit läuft mit dem letzten Zeitschritt weiter.");
                    connectionTask ??= masterClient.ConnectWithRetryAsync(stoppingToken);
                }
                else
                {
                    clock.ApplyMasterMessage(message);
                    if (message.Type == SimulationMessageTypes.TimeStep)
                    {
                        try
                        {
                            await masterClient.SendAcknowledgementAsync(
                                message.Sequence,
                                message.VirtualTime,
                                stoppingToken).ConfigureAwait(false);
                        }
                        catch (IOException exception)
                        {
                            clock.SetMasterConnected(false);
                            logger.LogWarning(exception, "ACK an den Simulation Master fehlgeschlagen.");
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

    private async Task ApplyCurrentEnvironmentAsync(CancellationToken cancellationToken)
    {
        var environment = await masterClient.RequestEnvironmentAsync(cancellationToken).ConfigureAwait(false);
        if (environment is null)
            return;

        clock.ApplyMasterMessage(new SimulationMessage
        {
            Type = SimulationMessageTypes.Environment,
            VirtualTime = environment.VirtualTime,
            Sequence = environment.Sequence,
            IsRunning = environment.IsRunning,
            TimeStep = environment.TimeStep
        });
    }
}
