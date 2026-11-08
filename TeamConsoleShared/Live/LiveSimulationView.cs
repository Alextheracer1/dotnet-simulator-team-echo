using Spectre.Console;
using Spectre.Console.Rendering;
using TeamConsoleShared.Layout;

namespace TeamConsoleShared.Live;

/// <summary>
/// Provides flicker-free live rendering for simulation loops using Spectre.Console's LiveDisplay.
/// </summary>
public static class LiveSimulationView
{
    /// <summary>
    /// Executes an async simulation loop with live, flicker-free console rendering.
    /// </summary>
    /// <param name="simulationLoop">
    /// Callback receiving an updater action. Call `update(renderable)` on every simulation tick.
    /// </param>
    public static async Task StartAsync(
        Func<Action<IRenderable>, Task> simulationLoop)
    {
        var initial = new Text(string.Empty);

        await AnsiConsole.Live(initial)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Crop)
            .StartAsync(async ctx =>
            {
                void Update(IRenderable renderable)
                {
                    ctx.UpdateTarget(renderable);
                    ctx.Refresh();
                }

                await simulationLoop(Update);
            });
    }

    /// <summary>
    /// Executes an async simulation loop with live, flicker-free rendering using SimulationDashboard instances.
    /// </summary>
    public static async Task StartDashboardAsync(
        Func<Action<SimulationDashboard>, Task> simulationLoop)
    {
        await StartAsync(async update =>
        {
            await simulationLoop(dashboard => update(dashboard.Build()));
        });
    }

    /// <summary>
    /// Renders a dashboard once directly to the console (useful when live mode is not desired).
    /// </summary>
    public static void RenderOnce(SimulationDashboard dashboard)
    {
        AnsiConsole.Write(dashboard.Build());
    }
}
