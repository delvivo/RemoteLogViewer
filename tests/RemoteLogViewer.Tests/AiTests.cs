using RemoteLogViewer;

public class AiTests
{
    private static AiSource Src(string name, params string[] lines) => new(name, $@"C:\logs\{name}.log", lines);

    [Fact]
    public void Build_under_limit_keeps_everything_with_headers_in_order()
    {
        var input = AiInput.Build("Domanda", [Src("a", "a1", "a2"), Src("b", "b1")]);

        Assert.Equal(0, input.Dropped);
        Assert.Equal("Domanda\n\n=== a (C:\\logs\\a.log) ===\na1\na2\n\n=== b (C:\\logs\\b.log) ===\nb1\n\n", input.Text);
    }

    [Fact]
    public void Build_over_limit_keeps_the_most_recent_lines_and_reports_dropped()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"riga{i:00}").ToArray(); // 6 chars + newline = 7 each
        var input = AiInput.Build("P", [Src("a", lines)], maxChars: 21);

        Assert.Equal(7, input.Dropped);
        Assert.Contains("riga08\nriga09\nriga10\n", input.Text);
        Assert.DoesNotContain("riga07", input.Text);
        Assert.Contains("(7 righe più vecchie omesse per limite di dimensione)", input.Text);
        Assert.True(input.Text.IndexOf("omesse") < input.Text.IndexOf("==="));
    }

    [Fact]
    public void Build_gives_the_share_unused_by_a_small_source_to_the_others()
    {
        var big = Enumerable.Range(1, 100).Select(i => $"big{i:000}").ToArray(); // 7 chars each
        var input = AiInput.Build("P", [Src("big", big), Src("small", "s1")], maxChars: 73); // small uses 3, big gets 70 → 10 lines

        Assert.Contains("s1\n", input.Text);
        Assert.Contains("big091\n", input.Text);
        Assert.DoesNotContain("big090", input.Text);
        Assert.Equal(90, input.Dropped);
    }

    [Fact]
    public void Build_never_exceeds_the_budget_on_log_lines()
    {
        var lines = Enumerable.Range(0, 5000).Select(i => new string('x', i % 50)).ToArray();
        var sources = new[] { Src("a", lines), Src("b", lines[..1000]), Src("c", lines[..10]) };
        var input = AiInput.Build("P", sources, maxChars: 10_000);

        var kept = sources.Sum(s => s.Lines.Count) - input.Dropped;
        Assert.True(kept > 0);
        var logChars = input.Text.Split('\n').Where(l => l.StartsWith('x') || l.Length == 0).Sum(l => l.Length);
        Assert.True(logChars <= 10_000);
    }

    [Fact]
    public void Build_with_an_empty_source_writes_only_its_header()
    {
        var input = AiInput.Build("P", [Src("vuota")]);
        Assert.Equal("P\n\n=== vuota (C:\\logs\\vuota.log) ===\n\n", input.Text);
    }

    [Fact]
    public void Settings_default_when_missing_or_corrupt()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, "ai.json");
        Assert.Equal("claude", AiSettings.Load(path).SelectedTool);

        File.WriteAllText(path, "{ non json");
        var s = AiSettings.Load(path);
        Assert.Equal("claude", s.SelectedTool);
        Assert.Empty(s.Commands);
        Assert.Empty(s.Consented);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, "sub", "ai.json");
        var s = new AiSettings { SelectedTool = "codex", LastPrompt = "perché?" };
        s.SetCommand("codex", "codex exec -m o9 -");
        s.Consented.Add("codex");
        s.Save(path);

        var r = AiSettings.Load(path);
        Assert.Equal("codex", r.SelectedTool);
        Assert.Equal("perché?", r.LastPrompt);
        Assert.Equal("codex exec -m o9 -", r.CommandFor("codex"));
        Assert.Equal(["codex"], r.Consented);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void SetCommand_with_default_or_empty_removes_the_override()
    {
        var s = new AiSettings();
        s.SetCommand("antigravity", "agy -p=x");
        Assert.Equal("agy -p=x", s.CommandFor("antigravity"));

        s.SetCommand("antigravity", AiTool.Get("antigravity").DefaultCommand);
        Assert.Empty(s.Commands);
        s.SetCommand("antigravity", "x");
        s.SetCommand("antigravity", "  ");
        Assert.Equal(AiTool.Get("antigravity").DefaultCommand, s.CommandFor("antigravity"));
    }

    [Fact]
    public void Unknown_tool_falls_back_without_throwing()
    {
        var s = new AiSettings { SelectedTool = "boh" };
        Assert.Equal("claude", AiTool.Get(s.SelectedTool).Id);
        Assert.Equal(AiTool.All[0].DefaultCommand, s.CommandFor("boh"));
    }

    private static readonly AiReport Report = new(new DateTime(2026, 10, 21, 9, 30, 0), "Claude Code", "claude -p",
        ["app · 2026-10-21", "db"], "Report errori\nper favore", 12, "## Errori\n\n| Causa | N |\n|---|---|\n| timeout | 3 |\n");

    [Fact]
    public void Report_markdown_has_header_and_full_response()
    {
        var md = Report.ToMarkdown();
        Assert.Contains("Claude Code", md);
        Assert.Contains("`claude -p`", md);
        Assert.Contains("app · 2026-10-21, db", md);
        Assert.Contains("> Report errori\n> per favore", md);
        Assert.Contains("2026-10-21 09:30:00", md);
        Assert.Contains("12", md);
        Assert.EndsWith("| timeout | 3 |\n", md);
    }

    [Fact]
    public void Report_html_is_self_contained()
    {
        var html = Report.ToHtml();
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("charset=\"utf-8\"", html);
        Assert.Contains("<table>", html);
        Assert.DoesNotContain("http", html);
    }

    private static readonly string[] Ten = Enumerable.Range(0, 10).Select(i => $"l{i}").ToArray();

    [Fact]
    public void Excerpt_takes_radius_lines_each_side_and_marks_the_target()
    {
        Assert.Equal(["l3", "l4", ">>> l5", "l6", "l7"], AiInput.Excerpt(Ten, 5, 2));
    }

    [Theory]
    [InlineData(0, new[] { ">>> l0", "l1", "l2" })]
    [InlineData(9, new[] { "l7", "l8", ">>> l9" })]
    public void Excerpt_is_shorter_at_the_edges(int index, string[] expected) =>
        Assert.Equal(expected, AiInput.Excerpt(Ten, index, 2));

    [Fact]
    public void Excerpt_radius_zero_and_single_line()
    {
        Assert.Equal([">>> l5"], AiInput.Excerpt(Ten, 5, 0));
        Assert.Equal([">>> x"], AiInput.Excerpt(["x"], 0, 20));
    }

    [Fact]
    public void Explain_preset_exists_mentions_the_marker_and_is_translated()
    {
        Assert.Contains(AiPrompt.All(), p => p.Name == "Spiega questa riga" && p.Text == AiPrompt.ExplainText());
        Assert.Contains(">>> ", AiPrompt.ExplainText());
        Assert.Equal("Explain this line", L.Translate("Spiega questa riga", "en"));
        Assert.Contains("Answer in English", L.Translate(AiPrompt.ExplainText(), "en"));
    }
}
