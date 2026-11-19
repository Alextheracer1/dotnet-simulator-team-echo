using Spectre.Console;
using Spectre.Console.Rendering;

namespace TeamConsoleShared.Layout;

/// <summary>
/// Renders the standardized top header banner for all simulation projects.
/// </summary>
public sealed class SimulationHeader
{
    public string Title { get; set; } = "Energy Twin Simulator";
    public string? Subtitle { get; set; }
    public int? ModbusPort { get; set; }
    public byte? ModbusUnitId { get; set; }
    public DateTime? SimulatedTime { get; set; }
    public string? TimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    public string? SpeedDescription { get; set; }
    public string? StatusBadgeMarkup { get; set; }
    public string? ModeBadgeMarkup { get; set; }
    public Color AccentColor { get; set; } = Color.Teal;

    public IRenderable Build()
    {
        int consoleWidth = 120;
        if (!Console.IsOutputRedirected)
        {
            try
            {
                consoleWidth = Console.WindowWidth;
            }
            catch
            {
                // Fallback to default width
            }
        }

        var isCompact = consoleWidth < 125;
        var isVeryCompact = consoleWidth < 95;

        var grid = new Grid().Expand();
        grid.AddColumn(new GridColumn().LeftAligned().NoWrap());
        grid.AddColumn(new GridColumn().RightAligned().NoWrap());

        // Left side: Title + Network / Subtitle
        var titlePart = $"[bold white]{Markup.Escape(Title)}[/]";
        var leftLine2 = new List<string>();

        if (ModbusPort.HasValue)
        {
            leftLine2.Add($"[grey62]Port:[/] [bold cyan]{ModbusPort.Value}[/]");
        }

        if (ModbusUnitId.HasValue)
        {
            leftLine2.Add($"[grey62]Unit:[/] [bold cyan]{ModbusUnitId.Value}[/]");
        }

        if (!string.IsNullOrWhiteSpace(Subtitle) && !isVeryCompact)
        {
            var subtitleText = Subtitle;
            if (isCompact)
            {
                // Cleanly shorten verbose subtitles in compact view
                if (subtitleText.Contains("Lithium-Eisenphosphat", StringComparison.OrdinalIgnoreCase))
                    subtitleText = "BESS Speicher";
                else if (subtitleText.Contains("Schnellladestation", StringComparison.OrdinalIgnoreCase))
                    subtitleText = "DC-Lader";
                else if (subtitleText.Contains("Zeitsynchronisation", StringComparison.OrdinalIgnoreCase))
                    subtitleText = "Zentraler Taktgeber";
                else if (subtitleText.Contains("Wetter", StringComparison.OrdinalIgnoreCase) || subtitleText.Contains("Weather", StringComparison.OrdinalIgnoreCase))
                    subtitleText = "Wetter-Provider";
                else if (subtitleText.StartsWith("Feldgerät: ", StringComparison.OrdinalIgnoreCase))
                    subtitleText = subtitleText["Feldgerät: ".Length..];
            }

            leftLine2.Add($"[grey62]{Markup.Escape(subtitleText)}[/]");
        }

        var leftContent = leftLine2.Count > 0
            ? $"{titlePart}\n{string.Join(" [grey35]|[/] ", leftLine2)}"
            : titlePart;

        // Right side: Badges + Virtual Clock & Speed
        var rightLine1 = new List<string>();
        if (!string.IsNullOrWhiteSpace(ModeBadgeMarkup))
        {
            rightLine1.Add(ModeBadgeMarkup);
        }

        if (!string.IsNullOrWhiteSpace(StatusBadgeMarkup))
        {
            rightLine1.Add(StatusBadgeMarkup);
        }

        var rightLine2 = new List<string>();
        if (SimulatedTime.HasValue)
        {
            var timePattern = isVeryCompact ? "HH:mm:ss" : (TimeFormat ?? "yyyy-MM-dd HH:mm:ss");
            var formattedTime = SimulatedTime.Value.ToString(timePattern);
            rightLine2.Add($"[grey62]Sim-Zeit:[/] [bold gold1]{formattedTime}[/]");
        }

        if (!string.IsNullOrWhiteSpace(SpeedDescription))
        {
            var speedText = SpeedDescription;
            if (isVeryCompact && speedText.Contains("min Sim = 1 s Real", StringComparison.OrdinalIgnoreCase))
            {
                speedText = speedText.Replace(" min Sim = 1 s Real", "m/s", StringComparison.OrdinalIgnoreCase);
            }
            rightLine2.Add($"[grey62]Tempo:[/] [bold white]{Markup.Escape(speedText)}[/]");
        }

        var rightContent = $"{string.Join(" ", rightLine1)}\n{string.Join(" [grey35]|[/] ", rightLine2)}";

        grid.AddRow(new Markup(leftContent), new Markup(rightContent));

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: AccentColor))
            .Expand();
    }
}
