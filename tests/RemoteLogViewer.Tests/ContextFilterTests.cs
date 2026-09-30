using RemoteLogViewer;

public class ContextFilterTests
{
    private static List<LogLine> Buffer(int n, LogLevel level = LogLevel.Info) =>
        Enumerable.Range(1, n).Select(i => new LogLine($"riga {i}", level)).ToList();

    private static List<LogLine> Run(List<LogLine> all, int context, Func<string, bool>? text, Func<LogLine, bool>? levelOk = null)
    {
        var f = new ContextFilter(context, levelOk ?? (_ => true), text);
        return Enumerable.Range(0, all.Count).SelectMany(i => f.Add(all, i)).ToList();
    }

    private static Func<string, bool> Is(params int[] n) => s => n.Any(x => s == $"riga {x}");

    [Fact]
    public void Shows_n_lines_around_a_match()
    {
        var shown = Run(Buffer(100), 2, Is(50));
        Assert.Equal(["riga 48", "riga 49", "riga 50", "riga 51", "riga 52"], shown.Select(l => l.Text));
        Assert.Equal([true, true, false, true, true], shown.Select(l => l.IsContext));
    }

    [Fact]
    public void Close_matches_merge_without_duplicates()
    {
        var shown = Run(Buffer(100), 2, Is(50, 53));
        Assert.Equal(Enumerable.Range(48, 8).Select(i => $"riga {i}"), shown.Select(l => l.Text));
    }

    [Fact]
    public void Distant_matches_are_separated()
    {
        var shown = Run(Buffer(100), 1, Is(10, 50));
        Assert.Equal(["riga 9", "riga 10", "riga 11", "--", "riga 49", "riga 50", "riga 51"], shown.Select(l => l.Text));
        Assert.True(shown[3].IsMarker);
    }

    [Fact]
    public void Adjacent_groups_are_not_separated()
    {
        var shown = Run(Buffer(100), 1, Is(10, 13)); // 9-11 and 12-14 touch
        Assert.DoesNotContain(shown, l => l.Text == "--");
        Assert.Equal(6, shown.Count);
    }

    [Fact]
    public void Zero_context_is_plain_filter()
    {
        var all = Buffer(10);
        Assert.Equal(["riga 3"], Run(all, 0, Is(3)).Select(l => l.Text));
        Assert.Equal(10, Run(all, 5, null).Count); // no text filter: context has no effect
        Assert.All(Run(all, 5, null), l => Assert.False(l.IsContext));
    }

    [Fact]
    public void Level_filter_applies_to_context_but_positions_still_count()
    {
        var all = Buffer(10);
        all[3] = new LogLine("riga 4", LogLevel.Debug);
        var shown = Run(all, 2, Is(5), l => l.Level != LogLevel.Debug);
        Assert.Equal(["riga 3", "riga 5", "riga 6", "riga 7"], shown.Select(l => l.Text));
    }

    [Fact]
    public void Hidden_level_line_never_matches()
    {
        var all = Buffer(10, LogLevel.Debug);
        Assert.Empty(Run(all, 2, Is(5), l => l.Level != LogLevel.Debug));
    }

    [Fact]
    public void Markers_always_shown()
    {
        var all = Buffer(10);
        all.Insert(2, new LogLine("— riconnesso —", LogLevel.None, isMarker: true));
        var shown = Run(all, 1, Is(9));
        Assert.Equal(["— riconnesso —", "riga 8", "riga 9", "riga 10"], shown.Select(l => l.Text));
    }

    [Fact]
    public void Trailing_context_arrives_incrementally()
    {
        var all = new List<LogLine>();
        var f = new ContextFilter(2, _ => true, Is(1));
        var shown = new List<LogLine>();
        for (var i = 1; i <= 5; i++)
        {
            all.Add(new LogLine($"riga {i}", LogLevel.Info));
            shown.AddRange(f.Add(all, all.Count - 1));
        }
        Assert.Equal(["riga 1", "riga 2", "riga 3"], shown.Select(l => l.Text));
    }
}
