using RemoteLogViewer;

public class LogTimestampTests
{
    private static readonly DateTime Default = new(2026, 9, 30);

    private static DateTime? Parse(string line) => LogTimestamp.TryParse(line, Default, out var v) ? v : null;

    [Theory]
    [InlineData("2026-10-01 12:34:56,789 INFO x", "2026-10-01 12:34:56.789")]
    [InlineData("2026-10-01T12:34:56.789Z INFO x", "2026-10-01 12:34:56.789")]
    [InlineData("[2026-10-01 12:34:56] INFO x", "2026-10-01 12:34:56")]
    [InlineData("01/10/2026 12:34:56 INFO x", "2026-10-01 12:34:56")]
    [InlineData("12:34:56 INFO x", "2026-09-30 12:34:56")]
    [InlineData("12:34:56.789 INFO x", "2026-09-30 12:34:56.789")]
    public void Parses_supported_formats(string line, string expected) =>
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Parse(line));

    [Theory]
    [InlineData("   at Foo.Bar() in x.cs:line 12")]
    [InlineData("INFO 2026-10-01 12:34:56 not at the start")]
    [InlineData("2026-13-40 12:34:56 impossible date")]
    [InlineData("25:00:00 impossible time")]
    [InlineData("")]
    public void Rejects_other_lines(string line) => Assert.Null(Parse(line));

    [Theory]
    [InlineData("14:32", "2026-09-30 14:32:00")]
    [InlineData(" 9:05:07 ", "2026-09-30 09:05:07")]
    [InlineData("2026-10-01 14:32", "2026-10-01 14:32:00")]
    [InlineData("01/10/2026 14:32:10", "2026-10-01 14:32:10")]
    public void Parses_go_to_targets(string text, string expected)
    {
        Assert.True(LogTimestamp.TryParseTarget(text, Default, out var v));
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), v);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData("2026-10-01")]
    [InlineData("")]
    public void Rejects_bad_go_to_targets(string text) => Assert.False(LogTimestamp.TryParseTarget(text, Default, out _));

    private static List<LogLine> Lines(params string[] texts) => texts.Select(t => new LogLine(t, LogLevel.None)).ToList();

    private static (int, GoToStatus) Find(List<LogLine> lines, string time) =>
        LogTimestamp.FindAtOrAfter(lines, Default + TimeSpan.Parse(time), Default);

    [Fact]
    public void Go_to_finds_first_line_at_or_after()
    {
        var lines = Lines("10:00:00 a", "14:30:00 b", "   at X()", "14:35:00 c", "   at Y()");
        Assert.Equal((3, GoToStatus.Found), Find(lines, "14:32"));
        Assert.Equal((1, GoToStatus.Found), Find(lines, "14:30"));
        Assert.Equal((-1, GoToStatus.NotFound), Find(lines, "23:00"));
        Assert.Equal((0, GoToStatus.BeforeFirst), Find(lines, "09:00"));
        Assert.Equal((0, GoToStatus.Found), Find(lines, "10:00"));
    }

    [Fact]
    public void Go_to_skips_markers_and_reports_no_timestamps()
    {
        var lines = new List<LogLine> { new("plain", LogLevel.None), new("10:00:00 marker-like", LogLevel.None, isMarker: true), new("11:00:00 x", LogLevel.None) };
        Assert.Equal((2, GoToStatus.BeforeFirst), Find(lines, "09:00"));
        Assert.Equal((-1, GoToStatus.NoTimestamps), Find(Lines("plain", "   at X()"), "09:00"));
    }
}
