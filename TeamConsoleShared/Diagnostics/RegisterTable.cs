using Spectre.Console;

namespace TeamConsoleShared.Diagnostics;

/// <summary>
/// Formats Modbus register dumps (01-12 Common + Device-Specific) into styled Spectre tables.
/// </summary>
public static class RegisterTable
{
    public sealed record RegisterEntry(
        int Register,
        string Name,
        string Type,
        string RawValue,
        string FormattedValue);

    public static Table Create(string title, IEnumerable<RegisterEntry> entries)
    {
        var table = new Table()
            .Title($"[bold cyan]{Markup.Escape(title)}[/]")
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(foreground: Color.Grey42))
            .Expand();

        table.AddColumn(new TableColumn("[bold grey85]Reg[/]").Centered());
        table.AddColumn(new TableColumn("[bold grey85]Bezeichnung[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold grey85]Typ[/]").Centered());
        table.AddColumn(new TableColumn("[bold grey85]Rohwert[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold grey85]Wert / Status[/]").LeftAligned());

        foreach (var entry in entries)
        {
            table.AddRow(
                $"[cyan]{entry.Register,2}[/]",
                $"[bold white]{Markup.Escape(entry.Name)}[/]",
                $"[grey62]{Markup.Escape(entry.Type)}[/]",
                $"[grey74]{Markup.Escape(entry.RawValue)}[/]",
                $"[bold green3]{Markup.Escape(entry.FormattedValue)}[/]"
            );
        }

        return table;
    }
}
