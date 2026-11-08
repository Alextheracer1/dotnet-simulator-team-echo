using System.Globalization;
using Spectre.Console;

namespace TeamConsoleShared.Components;

/// <summary>
/// Represents a single telemetry or state metric to be displayed in the dashboard.
/// </summary>
public sealed record MetricItem(
    string Label,
    string Value,
    string Unit = "",
    string ValueColor = "bold white",
    string? Icon = null,
    bool IsRawMarkup = false)
{
    public static MetricItem Number(
        string label,
        double value,
        string unit = "",
        string valueColor = "bold white",
        string format = "0.0",
        string? icon = null) =>
        new(label, value.ToString(format, CultureInfo.InvariantCulture), unit, valueColor, icon, IsRawMarkup: false);

    public static MetricItem Text(
        string label,
        string text,
        string valueColor = "bold white",
        string? icon = null) =>
        new(label, text, string.Empty, valueColor, icon, IsRawMarkup: false);

    public static MetricItem MarkupValue(
        string label,
        string markup,
        string unit = "",
        string? icon = null) =>
        new(label, markup, unit, string.Empty, icon, IsRawMarkup: true);
}
