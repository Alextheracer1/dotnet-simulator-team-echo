using Spectre.Console;
using Spectre.Console.Rendering;

namespace TeamConsoleShared.Components;

/// <summary>
/// Renders modern keyboard shortcuts / control legends with high-contrast key badges.
/// </summary>
public static class KeyBindingBar
{
    public static IRenderable Create(
        IEnumerable<(string Key, string Description)> bindings,
        string title = "Steuerung / Tasten")
    {
        var elements = bindings.Select(b =>
            $"[bold black on grey82] {Markup.Escape(b.Key)} [/] [grey85]{Markup.Escape(b.Description)}[/]");

        var content = string.Join("  ", elements);

        return new Panel(new Markup(content))
            .Header($"[grey62]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(foreground: Color.Grey35))
            .Expand();
    }

    public static string Inline(IEnumerable<(string Key, string Description)> bindings)
    {
        var elements = bindings.Select(b =>
            $"[bold black on grey82] {Markup.Escape(b.Key)} [/] [grey85]{Markup.Escape(b.Description)}[/]");

        return string.Join("  ", elements);
    }
}
