using System.Text.RegularExpressions;

namespace RemoteLogViewer;

public record SearchCriteria(
    string Text,
    bool IsRegex = false,
    bool MatchCase = false,
    bool WholeWord = false,
    IReadOnlySet<LogLevel>? Levels = null, // null or all four = no level filter; a partial filter also excludes LogLevel.None
    DateTime? From = null,
    DateTime? To = null)
{
    private static readonly LogLevel[] Real = [LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error];

    public bool HasLevelFilter => Levels != null && !Real.All(Levels.Contains);
    public bool HasTimeFilter => From != null || To != null;
    public bool IsEmpty => Text.Length == 0 && !HasLevelFilter && !HasTimeFilter;
}

/// Snapshot of one session's buffer. `Tag` lets the UI map a hit back to its view.
public record SearchSource(string Name, IReadOnlyList<LogLine> Lines, DateTime DefaultDate, object? Tag = null);

public record SearchHit(int Source, LogLine Line, int Start, int Length);

public record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated);

public static class LogSearch
{
    public const int MaxHits = 10_000;

    /// Searches every snapshot; hits are grouped by source, in buffer order. Throws ArgumentException on an invalid regex.
    public static SearchResult Run(IReadOnlyList<SearchSource> sources, SearchCriteria c, CancellationToken ct = default)
    {
        var hits = new List<SearchHit>();
        if (c.IsEmpty) return new(hits, false);

        Regex? re = null;
        if (c.Text.Length > 0)
        {
            var pattern = c.IsRegex ? c.Text : Regex.Escape(c.Text);
            if (c.WholeWord) pattern = $@"(?<!\w)(?:{pattern})(?!\w)";
            var options = RegexOptions.Compiled | (c.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
            re = new Regex(pattern, options, TimeSpan.FromMilliseconds(50));
        }

        for (var s = 0; s < sources.Count; s++)
        {
            var lines = sources[s].Lines;
            DateTime? last = null; // continuation lines (stack traces) inherit the last timestamp of the session
            for (var i = 0; i < lines.Count; i++)
            {
                if (i % 1000 == 0) ct.ThrowIfCancellationRequested();
                var l = lines[i];
                if (l.IsMarker) continue;

                if (c.HasTimeFilter)
                {
                    if (LogTimestamp.TryParse(l.Text, sources[s].DefaultDate, out var t)) last = t;
                    if (last is not { } ts || ts < c.From || ts > c.To) continue;
                }
                if (c.HasLevelFilter && !c.Levels!.Contains(l.Level)) continue;

                var start = 0;
                var length = 0;
                if (re != null)
                {
                    Match m;
                    try { m = re.Match(l.Text); }
                    catch (RegexMatchTimeoutException) { continue; }
                    if (!m.Success) continue;
                    (start, length) = (m.Index, m.Length);
                }

                if (hits.Count >= MaxHits) return new(hits, true);
                hits.Add(new SearchHit(s, l, start, length));
            }
        }
        return new(hits, false);
    }

    /// Text file with one `# name (count)` block per source that has hits.
    public static string FormatExport(IReadOnlyList<SearchSource> sources, SearchResult result)
    {
        var blocks = result.Hits.GroupBy(h => h.Source).OrderBy(g => g.Key)
            .Select(g => string.Join(Environment.NewLine, g.Select(h => h.Line.Text).Prepend($"# {sources[g.Key].Name} ({g.Count()})")));
        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }
}
