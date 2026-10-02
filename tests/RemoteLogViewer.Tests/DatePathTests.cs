using RemoteLogViewer;

public class DatePathTests
{
    private static readonly DateTime June11 = new(2026, 6, 11);

    [Theory]
    [InlineData(@"a\{date:yyyy_MM_dd}\log.log", true)]
    [InlineData(@"a\log.log", false)]
    [InlineData(@"a\{date}\log.log", false)]
    public void Has(string path, bool expected) => Assert.Equal(expected, DatePath.Has(path));

    [Theory]
    [InlineData(@"FOLDER\standard_logs\{date:yyyy_MM_dd}\log.log", @"FOLDER\standard_logs\2026_06_11\log.log")]
    [InlineData(@"logs\log_{date:yyyy-MM-dd}.log", @"logs\log_2026-06-11.log")]
    [InlineData(@"{date:yyyy}\{date:MM}\{date:yyyyMMdd}.log", @"2026\06\20260611.log")]
    [InlineData(@"plain\log.log", @"plain\log.log")]
    public void Resolve(string path, string expected) => Assert.Equal(expected, DatePath.Resolve(path, June11));

    [Theory]
    [InlineData(@"a\{date:yyyy_MM_dd}\log.log", 0)]
    [InlineData(@"a\log.log", 0)]
    [InlineData(@"a\{date:'day'dd}\log.log", 0)]   // quoted literal letters are fine
    [InlineData(@"a\{date:}\log.log", 1)]
    [InlineData(@"a\{date:yyyy/MM}\log.log", 1)]
    [InlineData(@"a\{date:yyyy:MM}\log.log", 1)]
    [InlineData(@"a\{date:yyyyMMdd_HH}\log.log", 1)]
    [InlineData(@"a\{date:hh}\log.log", 1)]
    [InlineData(@"a\{date:mm}\log.log", 1)]
    [InlineData(@"a\{date:ss}\log.log", 1)]
    [InlineData(@"a\{date:fff}\log.log", 1)]
    [InlineData(@"a\{date:tt}\log.log", 1)]
    [InlineData(@"a\{date:'unclosed}\log.log", 1)]
    public void Validate(string path, int errors) => Assert.Equal(errors, DatePath.Validate(path).Count);
}
