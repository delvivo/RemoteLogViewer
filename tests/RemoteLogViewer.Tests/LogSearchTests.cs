using RemoteLogViewer;

public class LogSearchTests
{
    private static readonly DateTime Day = new(2026, 10, 1);

    private static SearchSource Src(string name, params string[] lines)
    {
        var last = LogLevel.None;
        return new(name, lines.Select(l => LogLevels.Make(l, ref last, false)).ToList(), Day);
    }

    private static SearchResult Run(SearchCriteria c, params SearchSource[] sources) => LogSearch.Run(sources, c);

    private static string[] Texts(SearchResult r) => r.Hits.Select(h => h.Line.Text).ToArray();

    // ---- US1: text / regex / grouping ----

    [Fact]
    public void Text_is_case_insensitive_by_default_and_grouped_by_source_in_order()
    {
        var r = Run(new("timeout"), Src("A", "ok", "Timeout 1", "x"), Src("B", "nothing"), Src("C", "timeout 2"));
        Assert.Equal(["Timeout 1", "timeout 2"], Texts(r));
        Assert.Equal([0, 2], r.Hits.Select(h => h.Source));
        Assert.Equal((0, 7), (r.Hits[0].Start, r.Hits[0].Length));
    }

    [Fact]
    public void Plain_text_is_not_a_regex() =>
        Assert.Equal(["a.c"], Texts(Run(new("a.c"), Src("A", "abc", "a.c"))));

    [Fact]
    public void Regex_matches_and_reports_the_span()
    {
        var r = Run(new(@"id=\d+", IsRegex: true), Src("A", "x id=42 y"));
        Assert.Equal((2, 5), (r.Hits[0].Start, r.Hits[0].Length));
    }

    [Fact]
    public void Invalid_regex_throws() =>
        Assert.ThrowsAny<ArgumentException>(() => Run(new("(", IsRegex: true), Src("A", "x")));

    [Fact]
    public void No_match_gives_no_hits() => Assert.Empty(Run(new("zzz"), Src("A", "x")).Hits);

    [Fact]
    public void Empty_criteria_list_nothing() => Assert.Empty(Run(new(""), Src("A", "x", "y")).Hits);

    [Fact]
    public void Every_matching_line_of_a_stack_trace_is_a_hit() =>
        Assert.Equal(3, Run(new("Foo"), Src("A", "ERROR Foo failed", "   at Foo.Bar()", "   at Foo.Baz()", "   at Other()")).Hits.Count);

    [Fact]
    public void Markers_are_skipped()
    {
        var marker = new LogLine("— nuovo giorno: Foo —", LogLevel.None, isMarker: true);
        Assert.Empty(Run(new("Foo"), new SearchSource("A", [marker], Day)).Hits);
    }

    [Fact]
    public void Hits_are_capped_at_MaxHits()
    {
        var r = Run(new("x"), Src("A", Enumerable.Repeat("x", LogSearch.MaxHits + 1).ToArray()));
        Assert.Equal(LogSearch.MaxHits, r.Hits.Count);
        Assert.True(r.Truncated);
        Assert.False(Run(new("x"), Src("A", Enumerable.Repeat("x", LogSearch.MaxHits).ToArray())).Truncated);
    }

    [Fact]
    public void Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => LogSearch.Run([Src("A", "x")], new("x"), cts.Token));
    }

    // ---- US3: options, levels, time range ----

    [Fact]
    public void MatchCase_distinguishes_case() =>
        Assert.Equal(["Timeout"], Texts(Run(new("Timeout", MatchCase: true), Src("A", "timeout", "Timeout"))));

    [Fact]
    public void WholeWord_rejects_substrings()
    {
        Assert.Equal(["err here"], Texts(Run(new("err", WholeWord: true), Src("A", "error", "err here"))));
        Assert.Equal(["x [ERR] y"], Texts(Run(new("[ERR]", WholeWord: true), Src("A", "x [ERR] y"))));
    }

    [Fact]
    public void Level_filter_keeps_stack_trace_lines_and_drops_lines_without_level()
    {
        var src = Src("A", "preamble", "INFO a", "ERROR b", "   at X()", "WARN c");
        var errors = new HashSet<LogLevel> { LogLevel.Error };
        Assert.Equal(["ERROR b", "   at X()"], Texts(Run(new("", Levels: errors), src)));
        var all = new HashSet<LogLevel> { LogLevel.Error, LogLevel.Warn, LogLevel.Info, LogLevel.Debug };
        Assert.Equal(5, Run(new("a|b|c|preamble", IsRegex: true, Levels: all), src).Hits.Count);
    }

    [Fact]
    public void Level_and_text_combine() =>
        Assert.Equal(["ERROR disk"], Texts(Run(new("disk", Levels: new HashSet<LogLevel> { LogLevel.Error }), Src("A", "INFO disk", "ERROR disk", "ERROR net"))));

    [Fact]
    public void Time_range_is_inclusive()
    {
        var src = Src("A", "2026-10-01 10:00:00 INFO a", "2026-10-01 11:00:00 INFO b", "2026-10-01 12:00:00 INFO c", "2026-10-01 13:00:00 INFO d");
        var r = Run(new("", From: new(2026, 10, 1, 11, 0, 0), To: new(2026, 10, 1, 12, 0, 0)), src);
        Assert.Equal(["2026-10-01 11:00:00 INFO b", "2026-10-01 12:00:00 INFO c"], Texts(r));
    }

    [Fact]
    public void Lines_without_timestamp_inherit_the_previous_one_per_source()
    {
        var a = Src("A", "orphan", "2026-10-01 10:00:00 ERROR a", "   at X()", "2026-10-01 15:00:00 ERROR b", "   at Y()");
        var b = Src("B", "   at Z()");
        var r = Run(new("", From: new(2026, 10, 1, 9, 0, 0), To: new(2026, 10, 1, 11, 0, 0)), a, b);
        Assert.Equal(["2026-10-01 10:00:00 ERROR a", "   at X()"], Texts(r)); // orphan and B have no earlier timestamp: excluded
    }

    [Fact]
    public void Time_only_lines_use_the_default_date()
    {
        var src = Src("A", "10:00:00 INFO a", "12:00:00 INFO b");
        Assert.Equal(["12:00:00 INFO b"], Texts(Run(new("", From: new(2026, 10, 1, 11, 0, 0)), src)));
        Assert.Empty(Run(new("", From: new(2026, 10, 2, 0, 0, 0)), src).Hits);
    }

    [Fact]
    public void Filters_without_text_list_everything_that_passes() =>
        Assert.Equal(2, Run(new("", Levels: new HashSet<LogLevel> { LogLevel.Warn }), Src("A", "WARN a", "INFO b", "WARN c")).Hits.Count);

    // ---- US4: export ----

    [Fact]
    public void Export_groups_by_source_with_a_header()
    {
        var a = Src("A", "x 1", "y");
        var b = Src("B", "x 2");
        var sources = new[] { a, b };
        var text = LogSearch.FormatExport(sources, LogSearch.Run(sources, new("x")));
        var nl = Environment.NewLine;
        Assert.Equal($"# A (1){nl}x 1{nl}{nl}# B (1){nl}x 2", text);
    }

    [Fact]
    public void Export_of_nothing_is_empty() =>
        Assert.Equal("", LogSearch.FormatExport([Src("A", "x")], new SearchResult([], false)));
}
