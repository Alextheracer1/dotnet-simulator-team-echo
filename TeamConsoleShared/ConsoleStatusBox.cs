namespace TeamConsoleShared;

public sealed class ConsoleStatusBox
{
    private readonly int _boxWidth;

    public ConsoleStatusBox(int boxWidth = 84)
    {
        if (boxWidth < 20)
        {
            throw new ArgumentOutOfRangeException(nameof(boxWidth));
        }

        _boxWidth = boxWidth;
    }

    public void WriteBorder()
    {
        Console.WriteLine("+" + new string('-', _boxWidth - 2) + "+");
    }

    public void WriteLine(string text)
    {
        var value = text ?? string.Empty;
        var contentWidth = _boxWidth - 4;

        while (value.Length > contentWidth)
        {
            var splitAt = value.LastIndexOf(' ', contentWidth);
            if (splitAt <= 0)
            {
                splitAt = contentWidth;
            }

            Console.WriteLine("| " + value[..splitAt].PadRight(contentWidth) + " |");
            value = value[splitAt..].TrimStart();
        }

        Console.WriteLine("| " + value.PadRight(contentWidth) + " |");
    }
}
