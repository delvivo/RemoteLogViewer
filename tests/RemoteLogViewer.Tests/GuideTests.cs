using RemoteLogViewer;

public class GuideTests
{
    private static string Guide(string lang)
    {
        using var s = typeof(LogTailer).Assembly.GetManifestResourceStream($"RemoteLogViewer.Guida.{lang}.html");
        Assert.NotNull(s);
        return new StreamReader(s).ReadToEnd();
    }

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public void Guide_is_embedded_with_version_placeholder(string lang) => Assert.Contains("%VERSION%", Guide(lang));

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public void Guide_has_no_external_resources(string lang) // must work offline
    {
        var html = Guide(lang);
        Assert.DoesNotContain("src=\"http", html);
        Assert.DoesNotContain("href=\"http", html);
        Assert.DoesNotContain("@import", html);
    }

    [Theory]
    [InlineData("it", "F1")]
    [InlineData("it", "Ctrl</kbd>+<kbd>S")]
    [InlineData("it", "Ctrl</kbd>+<kbd>F")]
    [InlineData("it", "F3")]
    [InlineData("it", "F2")]
    [InlineData("it", "Canc")]
    [InlineData("en", "F1")]
    [InlineData("en", "Ctrl</kbd>+<kbd>S")]
    [InlineData("en", "Ctrl</kbd>+<kbd>F")]
    [InlineData("en", "F3")]
    [InlineData("en", "F2")]
    [InlineData("en", "Del")]
    public void Guide_lists_shortcuts(string lang, string key) => Assert.Contains(key, Guide(lang));

    [Fact]
    public void Guides_have_the_same_sections()
    {
        static List<string> Ids(string html) => System.Text.RegularExpressions.Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(Ids(Guide("it")), Ids(Guide("en")));
    }
}
