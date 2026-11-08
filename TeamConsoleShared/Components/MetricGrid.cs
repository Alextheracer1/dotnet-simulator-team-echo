using Spectre.Console;
using Spectre.Console.Rendering;

namespace TeamConsoleShared.Components;

/// <summary>
/// Renders a list of MetricItems formatted cleanly in single- or multi-column grids.
/// </summary>
public static class MetricGrid
{
    /// <summary>
    /// Renders metrics as a clean borderless table.
    /// </summary>
    public static Table Create(IEnumerable<MetricItem> items, int columns = 1)
    {
        var itemList = items.ToList();
        var table = new Table()
            .NoBorder()
            .HideHeaders()
            .Expand();

        if (columns <= 1)
        {
            table.AddColumn(new TableColumn("Label").PadRight(2));
            table.AddColumn(new TableColumn("Value").PadLeft(1));

            foreach (var item in itemList)
            {
                table.AddRow(FormatLabel(item), FormatValue(item));
            }
        }
        else
        {
            // Multi-column grid (e.g. 2 key-value pairs per row)
            for (var c = 0; c < columns; c++)
            {
                table.AddColumn(new TableColumn($"Label{c}").PadRight(1));
                table.AddColumn(new TableColumn($"Value{c}").PadRight(3));
            }

            var rowCount = (int)Math.Ceiling((double)itemList.Count / columns);
            for (var r = 0; r < rowCount; r++)
            {
                var cells = new List<IRenderable>();
                for (var c = 0; c < columns; c++)
                {
                    var idx = r + c * rowCount;
                    if (idx < itemList.Count)
                    {
                        var item = itemList[idx];
                        cells.Add(FormatLabel(item));
                        cells.Add(FormatValue(item));
                    }
                    else
                    {
                        cells.Add(Text.Empty);
                        cells.Add(Text.Empty);
                    }
                }
                table.AddRow(cells.ToArray());
            }
        }

        return table;
    }

    private static Markup FormatLabel(MetricItem item)
    {
        var iconPart = string.IsNullOrEmpty(item.Icon) ? string.Empty : $"{item.Icon} ";
        return new Markup($"{iconPart}[grey74]{Markup.Escape(item.Label)}[/]");
    }

    private static Markup FormatValue(MetricItem item)
    {
        string valueMarkup;
        if (item.IsRawMarkup)
        {
            valueMarkup = item.Value;
        }
        else
        {
            var color = string.IsNullOrWhiteSpace(item.ValueColor) ? "bold white" : item.ValueColor;
            valueMarkup = $"[{color}]{Markup.Escape(item.Value)}[/]";
        }

        var unitPart = string.IsNullOrEmpty(item.Unit) ? string.Empty : $" [grey58]{Markup.Escape(item.Unit)}[/]";
        return new Markup($"{valueMarkup}{unitPart}");
    }
}
