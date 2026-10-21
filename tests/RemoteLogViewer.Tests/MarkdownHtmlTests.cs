using RemoteLogViewer;

public class MarkdownHtmlTests
{
    private static string H(string md) => MarkdownHtml.ToHtml(md);

    [Fact]
    public void Headings() => Assert.Equal("<h1>Titolo</h1>\n<h3>Sotto</h3>\n<h6>sei</h6>\n", H("# Titolo\n### Sotto\n###### sei"));

    [Fact]
    public void Paragraphs_join_lines_and_split_on_blank() =>
        Assert.Equal("<p>uno due</p>\n<p>tre</p>\n", H("uno\ndue\n\ntre"));

    [Fact]
    public void Inline_bold_italic_code() =>
        Assert.Equal("<p><strong>b</strong> <em>i</em> <code>c **x**</code> <strong>u</strong></p>\n", H("**b** *i* `c **x**` __u__"));

    [Fact]
    public void Fenced_code_is_literal_and_escaped() =>
        Assert.Equal("<pre><code># no\n**no** &lt;b&gt;\n</code></pre>\n", H("```text\n# no\n**no** <b>\n```"));

    [Fact]
    public void Lists()
    {
        Assert.Equal("<ul>\n<li>a</li>\n<li>b</li>\n</ul>\n", H("- a\n* b"));
        Assert.Equal("<ol>\n<li>a</li>\n<li>b</li>\n</ol>\n", H("1. a\n2. b"));
        Assert.Equal("<p>testo</p>\n<ul>\n<li>a</li>\n</ul>\n<p>dopo</p>\n", H("testo\n- a\n\ndopo"));
    }

    [Fact]
    public void Table() =>
        Assert.Equal("<table>\n<tr><th>a</th><th>b</th></tr>\n<tr><td>1</td><td><strong>2</strong></td></tr>\n</table>\n",
            H("| a | b |\n|---|:--:|\n| 1 | **2** |"));

    [Fact]
    public void Rule_and_quote()
    {
        Assert.Equal("<hr>\n", H("---"));
        Assert.Equal("<blockquote><p>citazione su due</p>\n</blockquote>\n", H("> citazione\n> su due"));
    }

    [Fact]
    public void Html_is_escaped_everywhere()
    {
        var html = H("<script>alert(1)</script> & `<i>`\n\n| <b> |\n|---|\n| & |");
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<i>", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
    }

    [Fact]
    public void Links_only_for_http() =>
        Assert.Equal("<p><a href=\"https://x.it/a\">doc</a> [no](javascript:x)</p>\n", H("[doc](https://x.it/a) [no](javascript:x)"));
}
