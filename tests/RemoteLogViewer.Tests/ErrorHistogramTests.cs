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

        var (first, counts) = h.Series();
        Assert.Equal([2, 0, 0, 1], counts);
        Assert.Equal(new DateTime(2026, 10, 1, 14, 31, 0), first);
    }

    [Fact]
    public void Series_ends_at_the_latest_minute_of_any_line()
    {
        var h = Fed(Err("14:31:05 e"), Info("14:33:00 i"));

        Assert.Equal([1, 0, 0], h.Series().Counts);
    }

    [Fact]
    public void Alert_lines_and_history_lines_count()
    {
        var h = Fed(Err("14:31:05 e", history: true), new LogLine("14:31:06 deadlock", LogLevel.Info, isAlert: true));

        Assert.Equal([2], h.Series().Counts);
    }

    [Fact]
    public void Stack_trace_lines_and_other_levels_do_not_count()
    {
        var h = Fed(Err("14:31:05 e"), Err("   at Foo()", entry: false), new LogLine("14:31:07 w", LogLevel.Warn, isEntry: true));

        Assert.Equal([1], h.Series().Counts);
    }

    [Fact]
    public void Lines_without_timestamp_inherit_the_previous_minute_and_markers_are_ignored()
    {
        var h = Fed(Err("no time yet"), Info("14:31:00 i"), Err("continuation"), new LogLine("--- new day ---", LogLevel.None, isMarker: true));

        Assert.Equal([1], h.Series().Counts); // the first error came before any timestamp
    }

    [Fact]
    public void Series_is_empty_without_timestamps_and_Reset_clears()
    {
        Assert.Empty(Fed(Err("no time")).Series().Counts);
        var h = Fed(Err("14:31:05 e"));
        h.Reset();
        Assert.Empty(h.Series().Counts);
    }

    [Fact]
    public void Series_spans_from_the_first_error_and_is_capped_to_the_most_recent_minutes()
    {
        var h = Fed(Err("2026-01-01 00:00:00 old"), Err("2026-10-01 00:00:00 new"));

        var (first, counts) = h.Series();
        Assert.Equal(ErrorHistogram.MaxMinutes, counts.Length);
        Assert.Equal(1, counts[^1]);
        Assert.Equal(new DateTime(2026, 10, 1).AddMinutes(-(ErrorHistogram.MaxMinutes - 1)), first);
    }
}
