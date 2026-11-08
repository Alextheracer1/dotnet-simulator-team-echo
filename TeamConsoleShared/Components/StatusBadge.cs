namespace TeamConsoleShared.Components;

/// <summary>
/// Provides styled status badges formatted for Spectre.Console.
/// </summary>
public static class StatusBadge
{
    public static string Running(string text = "RUNNING") => $"[bold white on green] {Escape(text)} [/]";
    public static string Paused(string text = "PAUSED") => $"[bold black on yellow] {Escape(text)} [/]";
    public static string Fault(string text = "FEHLER") => $"[bold white on red] {Escape(text)} [/]";
    public static string Stopped(string text = "STOPPED") => $"[dim white on grey23] {Escape(text)} [/]";
    public static string Warning(string text = "WARNUNG") => $"[bold black on darkorange3] {Escape(text)} [/]";
    public static string Info(string text = "INFO") => $"[bold white on dodgerblue2] {Escape(text)} [/]";

    public static string Mode(bool isAuto) => isAuto
        ? "[bold black on teal] AUTO [/]"
        : "[bold white on darkorange] MANUELL [/]";

    public static string Connection(bool isConnected, string? connectedName = null)
    {
        if (!isConnected)
        {
            return "[dim white on grey23] FREI [/]";
        }

        var label = string.IsNullOrWhiteSpace(connectedName) ? "BELEGT" : connectedName;
        return $"[bold black on deepskyblue1] {Escape(label)} [/]";
    }

    /// <summary>
    /// Maps the standard Modbus Common OperatingState (0=Off, 1=Booting, 2=Running, 3=Error).
    /// </summary>
    public static string ForOperatingState(short operatingState) => operatingState switch
    {
        0 => Stopped("OFF"),
        1 => Warning("BOOTING"),
        2 => Running("RUNNING"),
        3 => Fault("ERROR"),
        _ => Stopped($"STATE {operatingState}")
    };

    public static string Custom(string text, string foreground = "white", string background = "blue") =>
        $"[bold {foreground} on {background}] {Escape(text)} [/]";

    private static string Escape(string text) => Spectre.Console.Markup.Escape(text);
}
