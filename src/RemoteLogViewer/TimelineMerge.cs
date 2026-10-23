namespace RemoteLogViewer;

/// One session in a merged timeline. `Source` is the originating view (typed object: this file stays free of WPF).
public class MergedSession(int index, string name, DateTime defaultDate, object? source)
{
    public int Index { get; } = index;
    public string Name { get; set; } = name; // follows the source tab when it is renamed
    public DateTime DefaultDate { get; } = defaultDate;
    public object? Source { get; set; } = source;
    public bool Closed { get; set; }
    public bool Visible { get; set; } = true;
    public bool HasTimestamps { get; internal set; }

    internal DateTime Last = DateTime.MinValue; // effective time of the last line fed (what a line without timestamp inherits)
    internal long NextSeq, MinSeq;
}

/// Copy of a source line (never the source object: `ContextFilter` writes `IsContext`, and the source tab shows the original).
public class MergedLine(LogLine original, MergedSession session, DateTime time, long seq)
    : LogLine(original.Text, original.Level, original.IsMarker, original.IsEntry, original.IsHistory, original.IsAlert)
{
    public LogLine Original { get; } = original;
    public MergedSession Session { get; } = session;
    public DateTime Time { get; internal set; } = time; // effective: own timestamp or inherited from the previous line of the session
    public long Seq { get; } = seq;                      // order inside the session; negative for prepended blocks
}

/// Chronological merge of several sessions' lines. Pure and UI-thread-only (the caller serializes access).
/// Order = (Time, session index, Seq): total, stable across refreshes, keeps each session's own order on ties.
public class TimelineMerge(int maxRows = 200_000, int trimBlock = 20_000)
{
    private List<MergedLine> _rows = [];
    private readonly List<MergedSession> _sessions = [];

    public IReadOnlyList<MergedLine> Rows => _rows;
    public IReadOnlyList<MergedSession> Sessions => _sessions;

    public MergedSession AddSession(string name, DateTime defaultDate, object? source = null)
    {
        var s = new MergedSession(_sessions.Count, name, defaultDate, source);
        _sessions.Add(s);
        return s;
    }

    private static int Compare(MergedLine a, MergedLine b)
    {
        var c = a.Time.CompareTo(b.Time);
        if (c != 0) return c;
        c = a.Session.Index.CompareTo(b.Session.Index);
        return c != 0 ? c : a.Seq.CompareTo(b.Seq);
    }

    private static DateTime Effective(MergedSession s, LogLine l, DateTime last)
    {
        if (l.IsMarker || !LogTimestamp.TryParse(l.Text, s.DefaultDate, out var t)) return last;
        s.HasTimestamps = true;
        return t;
    }

    /// Adds `lines` of `session` (arrival order; `prepend` = an older block, in file order). Returns true when no existing row
    /// changed index (the new rows all went to the end), so the caller can feed only them to its filter; false = rebuild.
    public bool Add(MergedSession session, IReadOnlyList<LogLine> lines, bool prepend = false)
    {
        if (lines.Count == 0) return true;
        var batch = new List<MergedLine>(lines.Count);
        var last = prepend ? DateTime.MinValue : session.Last;
        var seq0 = prepend ? session.MinSeq - lines.Count : session.NextSeq;
        for (var i = 0; i < lines.Count; i++)
        {
            last = Effective(session, lines[i], last);
            batch.Add(new MergedLine(lines[i], session, last, seq0 + i));
        }

        var shifted = false;
        if (prepend)
        {
            session.MinSeq = seq0;
            if (session.Last == DateTime.MinValue) session.Last = last; // the session had no timestamp until now
            if (last != DateTime.MinValue) shifted = Retime(session, last, batch);
        }
        else
        {
            session.NextSeq += lines.Count;
            session.Last = last;
        }

        batch.Sort(Compare);
        if (_rows.Count == 0 || Compare(batch[0], _rows[^1]) >= 0)
        {
            _rows.AddRange(batch);
            return !shifted;
        }

        // One linear pass, never a List.Insert per line: two 100k-line snapshots would cost ~10^10 element moves.
        // ponytail: O(n) per out-of-order tick; incremental insert if profiling shows it.
        var merged = new List<MergedLine>(_rows.Count + batch.Count);
        int r = 0, b = 0;
        while (r < _rows.Count && b < batch.Count) merged.Add(Compare(_rows[r], batch[b]) <= 0 ? _rows[r++] : batch[b++]);
        while (r < _rows.Count) merged.Add(_rows[r++]);
        while (b < batch.Count) merged.Add(batch[b++]);
        _rows = merged;
        return false;
    }

    // Rows of the session that had no timestamp yet (MinValue sorts first) now follow the older block: move them into `batch` with the new time.
    private bool Retime(MergedSession session, DateTime time, List<MergedLine> batch)
    {
        var any = false;
        var keep = new List<MergedLine>(_rows.Count);
        var i = 0;
        for (; i < _rows.Count && _rows[i].Time == DateTime.MinValue; i++)
        {
            if (_rows[i].Session != session) { keep.Add(_rows[i]); continue; }
            _rows[i].Time = time;
            batch.Add(_rows[i]);
            any = true;
        }
        if (!any) return false;
        keep.AddRange(_rows.Skip(i));
        _rows = keep;
        return true;
    }

    /// Drops the oldest rows once past the cap (block trim, like the log buffer). True = rows were removed, rebuild.
    public bool Trim()
    {
        if (_rows.Count <= maxRows) return false;
        _rows.RemoveRange(0, Math.Min(_rows.Count, _rows.Count - maxRows + trimBlock));
        return true;
    }
}
