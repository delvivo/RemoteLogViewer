using RemoteLogViewer;

public class SessionConfigTests
{
    [Fact]
    public void ResolvePath_combines_share_and_resolved_file()
    {
        var c = new SessionConfig { SharePath = @"\\server\E$", FilePath = @"FOLDER\standard_logs\{date:yyyy_MM_dd}\log.log" };
        Assert.Equal(@"\\server\E$\FOLDER\standard_logs\2026_06_11\log.log", c.ResolvePath(new DateTime(2026, 6, 11)));
        Assert.Equal(@"\\server\E$\FOLDER\standard_logs\{date:yyyy_MM_dd}\log.log", c.FullPath); // display keeps the placeholder
    }

    [Fact]
    public void Local_config_points_straight_at_the_file()
    {
        var c = SessionConfig.Local(@"C:\log\app.log");
        Assert.True(c.IsLocal);
        Assert.Equal("app.log", c.Name);
        Assert.Equal(@"C:\log\app.log", c.FilePath);
        Assert.Equal(1000, c.TailLines);
        Assert.Equal("auto", c.Encoding);
        Assert.Equal(@"C:\log\app.log", c.FullPath); // no share to combine with
        Assert.Equal(@"C:\log\app.log", c.ResolvePath(DateTime.Today));
    }

    [Fact]
    public void Local_config_normalizes_relative_paths()
    {
        var c = SessionConfig.Local(Path.Combine("sub", "..", "x.log"));
        Assert.Equal(Path.GetFullPath("x.log"), c.FilePath);
        Assert.False(new SessionConfig().IsLocal);
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

    [Fact]
    public void CurrentUndated_strips_placeholder_only_for_today()
    {
        var c = new SessionConfig { SharePath = @"\server\E$", FilePath = @"logs\app{date:'.'yyyy-MM-dd}.log", CurrentUndated = true };
        var today = new DateTime(2026, 9, 30);
        Assert.Equal(@"\server\E$\logs\app.log", c.ResolvePath(today, today));
        Assert.Equal(@"\server\E$\logs\app.2026-09-29.log", c.ResolvePath(today.AddDays(-1), today));
        c.CurrentUndated = false;
        Assert.Equal(@"\server\E$\logs\app.2026-09-30.log", c.ResolvePath(today, today));
    }
}
