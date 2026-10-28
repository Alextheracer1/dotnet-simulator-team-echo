using Spectre.Console;
using Spectre.Console.Rendering;
using TeamConsoleShared.Components;

namespace TeamConsoleShared.Layout;

/// <summary>
/// Fluent builder for composing full-screen simulator dashboards.
/// </summary>
public sealed class SimulationDashboard
{
    private SimulationHeader? _header;
    private readonly List<IRenderable> _sections = new();
    private string? _alertMessage;
    private string _alertStyle = "bold white on red";
    private readonly List<(string Key, string Description)> _keyBindings = new();

    public SimulationDashboard WithHeader(Action<SimulationHeader> configure)
    {
        _header = new SimulationHeader();
        configure(_header);
        return this;
    }

    public SimulationDashboard WithHeader(SimulationHeader header)
    {
        _header = header;
        return this;
    }

    public SimulationDashboard AddSection(string title, IRenderable content, Color? borderColor = null)
    {
        var panel = new Panel(content)
            .Header($"[bold white]{Markup.Escape(title)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: borderColor ?? Color.Grey42))
            .Expand();

        _sections.Add(panel);
        return this;
    }

    public SimulationDashboard AddMetricsSection(
        string title,
        IEnumerable<MetricItem> metrics,
        int columns = 2,
        Color? borderColor = null)
    {
        var grid = MetricGrid.Create(metrics, columns);
        return AddSection(title, grid, borderColor);
    }

    /// <summary>
    /// Adds two sections side-by-side in a responsive 2-column layout.
    /// </summary>
    public SimulationDashboard AddTwoColumns(
        (string Title, IRenderable Content) left,
        (string Title, IRenderable Content) right,
        Color? borderColor = null)
    {
        var leftPanel = new Panel(left.Content)
            .Header($"[bold white]{Markup.Escape(left.Title)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: borderColor ?? Color.Grey42))
            .Expand();

        var rightPanel = new Panel(right.Content)
            .Header($"[bold white]{Markup.Escape(right.Title)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: borderColor ?? Color.Grey42))
            .Expand();

        var table = new Table().NoBorder().HideHeaders().Expand();
        table.AddColumn(new TableColumn("Left"));
        table.AddColumn(new TableColumn("Right"));
        table.AddRow(leftPanel, rightPanel);

        _sections.Add(table);
        return this;
    }

    /// <summary>
    /// Adds a prominent banner when an alert or fault condition is active.
    /// </summary>
    public SimulationDashboard WithAlert(string? message, string style = "bold white on red")
    {
        _alertMessage = message;
        _alertStyle = style;
        return this;
    }

    public SimulationDashboard WithKeyBindings(params (string Key, string Description)[] bindings)
    {
        _keyBindings.Clear();
        _keyBindings.AddRange(bindings);
        return this;
    }

    public SimulationDashboard WithKeyBindings(IEnumerable<(string Key, string Description)> bindings)
    {
        _keyBindings.Clear();
        _keyBindings.AddRange(bindings);
        return this;
    }

    /// <summary>
    /// Compiles all dashboard elements into a single composite Spectre renderable.
    /// </summary>
    public IRenderable Build()
    {
        var layoutGrid = new Grid().Expand();
        layoutGrid.AddColumn(new GridColumn());

        // 1. Header
        if (_header != null)
        {
            layoutGrid.AddRow(_header.Build());
        }

        // 2. Alert banner if active
        if (!string.IsNullOrWhiteSpace(_alertMessage))
        {
            var alertPanel = new Panel(new Markup($"[{_alertStyle}]  ⚠️  {Markup.Escape(_alertMessage)}  [/]"))
                .Border(BoxBorder.Double)
                .BorderStyle(new Style(foreground: Color.Red))
                .Expand();
            layoutGrid.AddRow(alertPanel);
        }

        // 3. Main Sections
        foreach (var section in _sections)
        {
            layoutGrid.AddRow(section);
        }

        // 4. Keybinding footer
        if (_keyBindings.Count > 0)
        {
            layoutGrid.AddRow(KeyBindingBar.Create(_keyBindings));
        }

        return layoutGrid;
    }

    /// <summary>
    /// Directly writes the dashboard to the console.
    /// </summary>
    public void Render()
    {
        AnsiConsole.Write(Build());
    }
}
