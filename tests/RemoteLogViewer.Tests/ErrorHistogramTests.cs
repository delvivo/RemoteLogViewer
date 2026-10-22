using RemoteLogViewer;

public class ErrorHistogramTests
{
    private static readonly DateTime Day = new(2026, 10, 1);

    private static LogLine Err(string text, bool history = false, bool entry = true) => new(text, LogLevel.Error, isEntry: entry, isHistory: history);
    private static LogLine Info(string text) => new(text, LogLevel.Info, isEntry: true);

    private static ErrorHistogram Fed(params LogLine[] lines)
    {
        var h = new ErrorHistogram();
        foreach (var l in lines) h.Add(l, Day);
        return h;
    }

    [Fact]
    public void Counts_error_entries_per_minute_and_zero_fills_the_gaps()
    {
        var h = Fed(Err("14:31:05 e"), Err("14:31:50 e"), Info("14:32:00 i"), Err("14:34:10 e"));

        Assert.Equal([2, 0, 0, 1], h.Window(4).Select(b => b.Count));
        Assert.Equal(new DateTime(2026, 10, 1, 14, 34, 0), h.Window(4)[3].Minute);
    }

    [Fact]
    public void Window_ends_at_the_latest_minute_of_any_line()
    {
        var h = Fed(Err("14:31:05 e"), Info("14:33:00 i"));

        Assert.Equal([1, 0, 0], h.Window(3).Select(b => b.Count));
    }

    [Fact]
    public void Alert_lines_and_history_lines_count()
    {
        var h = Fed(Err("14:31:05 e", history: true), new LogLine("14:31:06 deadlock", LogLevel.Info, isAlert: true));

        Assert.Equal([2], h.Window(1).Select(b => b.Count));
    }

    [Fact]
    public void Stack_trace_lines_and_other_levels_do_not_count()
    {
        var h = Fed(Err("14:31:05 e"), Err("   at Foo()", entry: false), new LogLine("14:31:07 w", LogLevel.Warn, isEntry: true));

        Assert.Equal([1], h.Window(1).Select(b => b.Count));
    }

    [Fact]
    public void Lines_without_timestamp_inherit_the_previous_minute_and_markers_are_ignored()
    {
        var h = Fed(Err("no time yet"), Info("14:31:00 i"), Err("continuation"), new LogLine("--- new day ---", LogLevel.None, isMarker: true));

        Assert.Equal([1], h.Window(1).Select(b => b.Count)); // the first error came before any timestamp
    }

    [Fact]
    public void Window_is_empty_without_timestamps_or_bars_and_Reset_clears()
    {
        Assert.Empty(Fed(Err("no time")).Window(5));
        var h = Fed(Err("14:31:05 e"));
        Assert.Empty(h.Window(0));
        h.Reset();
        Assert.Empty(h.Window(5));
    }
}
