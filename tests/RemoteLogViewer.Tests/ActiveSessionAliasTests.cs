using RemoteLogViewer;

public class ActiveSessionAliasTests
{
    private static ActiveSession Make(DateTime? date = null) => new(SessionConfig.Local(@"C:\logs\app.log"), date);

    [Fact]
    public void Display_name_is_the_session_name_until_renamed()
    {
        var s = Make();
        Assert.Equal(s.Config.Name, s.DisplayName);
        s.Alias = "billing";
        Assert.Equal("billing", s.DisplayName);
        s.Alias = null;
        Assert.Equal(s.Config.Name, s.DisplayName);
    }

    [Fact]
    public void Alias_keeps_the_date_suffix_and_does_not_touch_the_saved_config()
    {
        var s = Make(new DateTime(2026, 10, 1));
        var name = s.Config.Name;
        s.Alias = "billing";
        Assert.Equal("billing · 2026-10-01", s.DisplayName);
        Assert.Equal(name, s.Config.Name);
    }
}
