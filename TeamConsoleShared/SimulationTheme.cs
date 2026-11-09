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
    public static readonly Color WeatherAccent = Color.SkyBlue1;

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
                ResetConsole();
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
    /// Completely clears the console screen and scrollback buffer, resets the cursor to (0, 0),
    /// and resets the window scroll position on Windows to guarantee a 100% clean canvas.
    /// </summary>
    public static void ResetConsole()
    {
        if (Console.IsOutputRedirected) return;

        try
        {
            // \x1b[3J clears the entire scrollback history buffer in VT-compliant terminals
            // \x1b[2J clears the visible screen
            // \x1b[H moves cursor to row 1, col 1
            Console.Write("\x1b[3J\x1b[2J\x1b[H");
        }
        catch
        {
            // Fallback for redirected or constrained console environments
        }

        try
        {
            AnsiConsole.Clear();
        }
        catch
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // Fallback for redirected or constrained console environments
            }
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Console.SetWindowPosition(0, 0);
            }
            Console.SetCursorPosition(0, 0);
        }
        catch
        {
            // Fallback for redirected or constrained console environments
        }
    }

    /// <summary>
    /// Checks whether the console window dimensions have changed since the last frame.
    /// </summary>
    public static bool CheckResize()
    {
        if (Console.IsOutputRedirected) return false;

        try
        {
            int currentWidth = Console.WindowWidth;
            int currentHeight = Console.WindowHeight;

            return _lastWidth != -1 && (currentWidth != _lastWidth || currentHeight != _lastHeight);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets the cursor to (0, 0) for flicker-free, clean in-place re-rendering.
    /// Performs a full console reset if clearConsole is true or if a terminal resize is detected.
    /// </summary>
    public static void PrepareFrame(bool clearConsole = false)
    {
        if (!Console.IsOutputRedirected)
        {
            lock (_renderLock)
            {
                try
                {
                    int currentWidth = Console.WindowWidth;
                    int currentHeight = Console.WindowHeight;

                    if (clearConsole || _lastWidth == -1 || currentWidth != _lastWidth || currentHeight != _lastHeight)
                    {
                        ResetConsole();
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
