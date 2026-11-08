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

    private static readonly object _renderLock = new();
    private static int _lastWidth = -1;
    private static int _lastHeight = -1;

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
                Console.Clear();
                _lastWidth = Console.WindowWidth;
                _lastHeight = Console.WindowHeight;
            }
            catch
            {
                // Terminal environment might not support cursor visibility modification
            }
        }
    }

    /// <summary>
    /// Resets the cursor to (0, 0) for flicker-free in-place re-rendering.
    /// Automatically clears the console screen buffer if a terminal resize is detected.
    /// </summary>
    public static void PrepareFrame()
    {
        if (!Console.IsOutputRedirected)
        {
            lock (_renderLock)
            {
                try
                {
                    int currentWidth = Console.WindowWidth;
                    int currentHeight = Console.WindowHeight;

                    if (_lastWidth == -1 || currentWidth != _lastWidth || currentHeight != _lastHeight)
                    {
                        Console.Clear();
                        _lastWidth = currentWidth;
                        _lastHeight = currentHeight;
                    }
                    else
                    {
                        Console.SetCursorPosition(0, 0);
                    }
                }
                catch
                {
                    // Fallback for redirected or constrained console environments
                }
            }
        }
    }

    /// <summary>
    /// Clears any leftover lines below the rendered frame to prevent ghosting or duplicated panels
    /// when the frame content height shrinks.
    /// </summary>
    public static void EndFrame()
    {
        if (!Console.IsOutputRedirected)
        {
            lock (_renderLock)
            {
                try
                {
                    Console.Write("\x1b[J");
                }
                catch
                {
                    // Fallback for redirected or constrained console environments
                }
            }
        }
    }
}
