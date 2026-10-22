using RemoteLogViewer;

public class LogLevelTests
{
    [Theory]
    [InlineData("2026-09-29 10:00:00 ERROR boom", LogLevel.Error)]
    [InlineData("[FATAL] db down", LogLevel.Error)]
    [InlineData("2026-09-29 WARN slow", LogLevel.Warn)]
    [InlineData("level=Warning x", LogLevel.Warn)]
    [InlineData("INFO started", LogLevel.Info)]
    [InlineData("DEBUG x=1", LogLevel.Debug)]
    [InlineData("trace enter", LogLevel.Debug)]
    [InlineData("INFO request failed with ERROR", LogLevel.Info)] // leftmost wins
    public void Detects_level(string line, LogLevel expected) => Assert.Equal(expected, LogLevels.Detect(line, LogLevel.None));

    [Theory]
    [InlineData("ERRORS count 0")]
    [InlineData("INFORMATION")]
    [InlineData("plain text")]
    public void Word_boundary_required(string line) => Assert.Equal(LogLevel.None, LogLevels.Detect(line, LogLevel.None));

    [Fact]
    public void Continuation_line_inherits_previous_level() =>
        Assert.Equal(LogLevel.Error, LogLevels.Detect("   at Foo.Bar() in Baz.cs:line 3", LogLevel.Error));

    [Fact]
    public void Stack_trace_is_one_entry()
    {
        var last = LogLevel.None;
        var lines = new[] { "INFO ok", "ERROR boom", "   at A.B()", "   at C.D()", "WARN slow" }
            .Select(l => LogLevels.Make(l, ref last, history: false)).ToList();
        Assert.Equal([true, true, false, false, true], lines.Select(l => l.IsEntry));
        Assert.Equal([LogLevel.Info, LogLevel.Error, LogLevel.Error, LogLevel.Error, LogLevel.Warn], lines.Select(l => l.Level));
    }

    [Fact]
    public void Leading_lines_without_level_are_not_entries()
    {
        var last = LogLevel.None;
        var l = LogLevels.Make("plain", ref last, history: true);
        Assert.False(l.IsEntry);
        Assert.True(l.IsHistory);
        Assert.Equal(LogLevel.None, l.Level);
    }

    private static List<LogLine> MakeAll(SessionConfig c, bool history, params string[] lines)
    {
        var rules = new LineRules(c);
        var last = LogLevel.None;
        return lines.Select(l => LogLevels.Make(l, ref last, history, rules)).ToList();
    }

    [Fact]
    public void Custom_patterns_classify_unknown_formats()
    {
        var c = new SessionConfig { ErrorPattern = @"\[E\]", InfoPattern = @"\[I\]" };
        var lines = MakeAll(c, false, "[E] boom", "   at A.B()", "[i] ok", "WARN slow", "plain");
        Assert.Equal([LogLevel.Error, LogLevel.Error, LogLevel.Info, LogLevel.Warn, LogLevel.Warn], lines.Select(l => l.Level)); // case-insensitive, built-in fallback, inheritance
        Assert.Equal([true, false, true, true, false], lines.Select(l => l.IsEntry));
    }

    [Fact]
    public void Most_severe_custom_pattern_wins() =>
        Assert.Equal(LogLevel.Error, new LineRules(new SessionConfig { WarnPattern = "x", ErrorPattern = "x" }).Detect("x"));

    [Fact]
    public void Custom_pattern_wins_over_built_in() =>
        Assert.Equal(LogLevel.Debug, new LineRules(new SessionConfig { DebugPattern = "level=10" }).Detect("ERROR level=10"));

    [Fact]
    public void Invalid_pattern_is_ignored()
    {
        var rules = new LineRules(new SessionConfig { ErrorPattern = "[", AlertPattern = "(" });
        Assert.Equal(LogLevel.Info, rules.Detect("INFO [ x"));
        Assert.False(rules.IsAlert("( x"));
    }

    [Fact]
    public void Alert_only_for_new_matching_lines()
    {
        var c = new SessionConfig { AlertPattern = "deadlock|timeout" };
        Assert.Equal([false, true, true], MakeAll(c, false, "INFO ok", "INFO Deadlock found", "   timeout").Select(l => l.IsAlert));
        Assert.All(MakeAll(c, true, "INFO deadlock"), l => Assert.False(l.IsAlert));
        Assert.All(MakeAll(new SessionConfig(), false, "INFO deadlock"), l => Assert.False(l.IsAlert));
    }
}
