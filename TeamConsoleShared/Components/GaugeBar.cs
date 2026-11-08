using System.Globalization;
using Spectre.Console;

namespace TeamConsoleShared.Components;

/// <summary>
/// Renders visual gauge/progress bars using Unicode block glyphs and Spectre.Console color markup.
/// </summary>
public static class GaugeBar
{
    private const char FullBlock = '█';
    private const char EmptyBlock = '░';

    /// <summary>
    /// Creates a formatted power/load bar with automatic color thresholds (green -> yellow -> red).
    /// </summary>
    public static string Power(
        double currentKw,
        double maxKw,
        string unit = "kW",
        int barWidth = 24,
        string? label = null)
    {
        var safeMax = maxKw > 0 ? maxKw : 1.0;
        var ratio = Math.Clamp(currentKw / safeMax, 0.0, 1.0);
        var percent = (int)Math.Round(ratio * 100);

        var color = ratio switch
        {
            >= 0.95 => "red",
            >= 0.80 => "gold1",
            >= 0.60 => "chartreuse3",
            _ => "green3"
        };

        return RenderBar(ratio, barWidth, color, label, $"{currentKw,6:F1} / {maxKw:0.###} {unit}", $"{percent,3}%");
    }

    /// <summary>
    /// Creates a formatted State-of-Charge (SOC) bar (0.0 - 1.0 or 0 - 100%).
    /// </summary>
    public static string StateOfCharge(
        double socFraction,
        int barWidth = 22,
        string? label = null,
        double? targetSoc = null)
    {
        var ratio = Math.Clamp(socFraction > 1.0 ? socFraction / 100.0 : socFraction, 0.0, 1.0);
        var percent = (int)Math.Round(ratio * 100);

        var color = ratio switch
        {
            < 0.15 => "red",
            < 0.30 => "darkorange",
            < 0.80 => "green3",
            _ => "cyan1"
        };

        var extraInfo = targetSoc.HasValue
            ? $"{percent,3}% [grey58]→ {targetSoc.Value:P0}[/]"
            : $"{percent,3}%";

        return RenderBar(ratio, barWidth, color, label, null, extraInfo);
    }

    /// <summary>
    /// Generic visual bar with explicit color and customizable labels.
    /// </summary>
    public static string Create(
        double ratio,
        int barWidth = 20,
        string fillColor = "green",
        string emptyColor = "grey27",
        string? prefix = null,
        string? suffix = null)
    {
        var clamped = Math.Clamp(ratio, 0.0, 1.0);
        var filled = (int)Math.Round(clamped * barWidth);
        var empty = barWidth - filled;

        var filledStr = filled > 0 ? $"[{fillColor}]{new string(FullBlock, filled)}[/]" : string.Empty;
        var emptyStr = empty > 0 ? $"[{emptyColor}]{new string(EmptyBlock, empty)}[/]" : string.Empty;

        var prefixPart = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix} ";
        var suffixPart = string.IsNullOrEmpty(suffix) ? string.Empty : $" {suffix}";

        return $"{prefixPart}[grey35][[[/]{filledStr}{emptyStr}[grey35]]][/]{suffixPart}";
    }

    private static string RenderBar(
        double ratio,
        int barWidth,
        string fillColor,
        string? label,
        string? values,
        string? percentage)
    {
        var clamped = Math.Clamp(ratio, 0.0, 1.0);
        var filled = (int)Math.Round(clamped * barWidth);
        var empty = barWidth - filled;

        var filledStr = filled > 0 ? $"[{fillColor}]{new string(FullBlock, filled)}[/]" : string.Empty;
        var emptyStr = empty > 0 ? $"[grey27]{new string(EmptyBlock, empty)}[/]" : string.Empty;

        var labelPart = string.IsNullOrWhiteSpace(label) ? string.Empty : $"[silver]{Markup.Escape(label)}:[/] ";
        var valuesPart = string.IsNullOrWhiteSpace(values) ? string.Empty : $" [bold white]{values}[/]";
        var percentPart = string.IsNullOrWhiteSpace(percentage) ? string.Empty : $" [bold {fillColor}]{percentage}[/]";

        return $"{labelPart}[grey35][[[/]{filledStr}{emptyStr}[grey35]]][/]{percentPart}{valuesPart}";
    }
}
