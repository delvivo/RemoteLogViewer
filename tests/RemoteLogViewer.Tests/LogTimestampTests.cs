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
}
