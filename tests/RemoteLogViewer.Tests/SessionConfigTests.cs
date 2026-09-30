using RemoteLogViewer;

public class SessionConfigTests
{
    [Fact]
    public void ResolvePath_combines_share_and_resolved_file()
    {
        var c = new SessionConfig { SharePath = @"\\server\E$", FilePath = @"DOB\standard_logs\{date:yyyy_MM_dd}\log.log" };
        Assert.Equal(@"\\server\E$\DOB\standard_logs\2026_06_11\log.log", c.ResolvePath(new DateTime(2026, 6, 11)));
        Assert.Equal(@"\\server\E$\DOB\standard_logs\{date:yyyy_MM_dd}\log.log", c.FullPath); // display keeps the placeholder
    }

    [Fact]
    public void Date_placeholder_not_allowed_in_share()
    {
        var c = new SessionConfig { Name = "x", SharePath = @"\\server\{date:yyyy}", FilePath = "a.log" };
        Assert.Contains("La data non è ammessa nella share.", c.Validate());
    }

    [Fact]
    public void Validate_includes_date_format_errors()
    {
        var c = new SessionConfig { Name = "x", SharePath = @"\\server\E$", FilePath = @"logs\{date:}\a.log" };
        Assert.Contains("Formato data vuoto.", c.Validate());
    }

    [Fact]
    public void Unc_file_with_date_inside_share_is_valid()
    {
        var c = new SessionConfig { Name = "x", SharePath = @"\\server\E$", FilePath = @"\\server\E$\logs\{date:yyyy_MM_dd}\a.log" };
        Assert.Empty(c.Validate());
    }
}
