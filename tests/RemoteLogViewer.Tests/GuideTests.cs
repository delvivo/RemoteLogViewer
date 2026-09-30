using RemoteLogViewer;

public class GuideTests
{
    private static string Guide()
    {
        using var s = typeof(LogTailer).Assembly.GetManifestResourceStream("RemoteLogViewer.Guida.html");
        Assert.NotNull(s);
        return new StreamReader(s).ReadToEnd();
    }

    [Fact]
    public void Guide_is_embedded_with_version_placeholder() => Assert.Contains("%VERSION%", Guide());

    [Fact]
    public void Guide_has_no_external_resources() // must work offline
    {
        var html = Guide();
        Assert.DoesNotContain("src=\"http", html);
        Assert.DoesNotContain("href=\"http", html);
        Assert.DoesNotContain("@import", html);
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("Ctrl</kbd>+<kbd>S")]
    [InlineData("Ctrl</kbd>+<kbd>F")]
    [InlineData("F3")]
    [InlineData("F2")]
    [InlineData("Canc")]
    public void Guide_lists_shortcuts(string key) => Assert.Contains(key, Guide());
}
