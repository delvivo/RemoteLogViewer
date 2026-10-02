using RemoteLogViewer;

public class LocTests
{
    [Fact]
    public void English_comes_from_the_table_and_falls_back_to_the_key()
    {
        Assert.Equal("Name required.", L.Translate("Nome obbligatorio.", "en"));
        Assert.Equal("not translated", L.Translate("not translated", "en"));
        Assert.Equal("Nome obbligatorio.", L.Translate("Nome obbligatorio.", "it"));
    }
}
