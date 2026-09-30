namespace RemoteLogViewer;

/// Filter with `grep -C N` context, fed one buffer line at a time (so Drain and Rebuild share the logic).
/// Context positions count every buffer line; the level check only decides whether a line is shown.
public class ContextFilter(int context, Func<LogLine, bool> levelOk, Func<string, bool>? text)
{
    private int _lastShown = -1; // buffer index of the last line emitted (or skipped by the level check) around a match
    private int _after;          // context lines still due after the last match

    /// Lines to append to the view for the new line all[i]; lines must arrive in order, i = 0, 1, 2…
    public IEnumerable<LogLine> Add(IReadOnlyList<LogLine> all, int i)
    {
        var l = all[i];
        if (l.IsMarker) return [l]; // markers always shown, they don't break a context group
        if (context == 0 || text == null)
            return levelOk(l) && (text == null || text(l.Text)) ? [Show(l, false)] : [];

        if (levelOk(l) && text(l.Text))
        {
            var result = new List<LogLine>();
            var from = Math.Max(_lastShown + 1, i - context);
            if (_lastShown >= 0 && from > _lastShown + 1) result.Add(new LogLine("--", LogLevel.None, isMarker: true));
            for (var j = from; j < i; j++)
                if (!all[j].IsMarker && levelOk(all[j])) result.Add(Show(all[j], true));
            result.Add(Show(l, false));
            _lastShown = i;
            _after = context;
            return result;
        }
        if (_after == 0) return [];
        _after--;
        _lastShown = i;
        return levelOk(l) ? [Show(l, true)] : [];
    }

    private static LogLine Show(LogLine l, bool isContext)
    {
        l.IsContext = isContext;
        return l;
    }
}
