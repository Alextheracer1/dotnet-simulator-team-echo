using Spectre.Console;

namespace TeamConsoleShared;

/// <summary>
/// Central theme configuration for consistent console appearance across all Energy Twin projects.
/// </summary>
public static class SimulationTheme
{
    public static readonly Color ChargerAccent = Color.Teal;
    public static readonly Color BatteryAccent = Color.Green3;
    public static readonly Color PvAccent = Color.Gold1;
    public static readonly Color WindAccent = Color.DeepSkyBlue1;
    public static readonly Color MasterAccent = Color.MediumPurple;
    public static readonly Color GatewayAccent = Color.Cyan1;
    public static readonly Color ViewerAccent = Color.DodgerBlue1;
    public static readonly Color DiagnoseAccent = Color.DarkOrange;

    /// <summary>
    /// Configures console output encoding and hides the cursor if supported.
    /// </summary>
    public static void InitializeConsole()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.CursorVisible = false;
            }
            catch
            {
                // Terminal environment might not support cursor visibility modification
            }
        }
    }

    /// <summary>
    /// Resets the cursor to (0, 0) for flicker-free in-place re-rendering.
    /// </summary>
    public static void PrepareFrame()
    {
        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.SetCursorPosition(0, 0);
            }
            catch
            {
                // Fallback for redirected or constrained console environments
            }
        }
    }
}
