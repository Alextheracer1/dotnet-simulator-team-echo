using Spectre.Console.Rendering;

namespace TeamConsoleShared.Layout;

/// <summary>
/// Wraps an IRenderable and constrains the emitted output to a maximum number of lines,
/// preventing terminal buffer overflow and vertical scrolling.
/// </summary>
public sealed class CroppedRenderable : IRenderable
{
    private readonly IRenderable _inner;
    private readonly int _maxLines;

    public CroppedRenderable(IRenderable inner, int maxLines)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxLines = Math.Max(1, maxLines);
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return _inner.Measure(options, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var lineCount = 0;
        foreach (var segment in _inner.Render(options, maxWidth))
        {
            if (lineCount >= _maxLines)
            {
                yield break;
            }

            yield return segment;

            if (segment.IsLineBreak)
            {
                lineCount++;
            }
        }
    }
}
