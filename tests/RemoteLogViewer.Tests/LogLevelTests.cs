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
}
