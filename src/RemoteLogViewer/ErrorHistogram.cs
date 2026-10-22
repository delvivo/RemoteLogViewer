namespace RemoteLogViewer;

/// Error entries per minute, fed one line at a time in buffer order. Same rule as the unread badge (Error entry or alert),
/// history included. A line without a timestamp belongs to the previous line's minute; nothing counts before the first timestamp.
public sealed class ErrorHistogram
{
    private readonly Dictionary<DateTime, int> _counts = [];
    private DateTime? _minute;     // minute of the last timestamped line
    private DateTime _latest;      // latest minute seen (any line)

    public void Reset()
    {
        _counts.Clear();
        _minute = null;
        _latest = default;
    }

    public void Add(LogLine line, DateTime defaultDate)
    {
        if (line.IsMarker) return;
        if (LogTimestamp.TryParse(line.Text, defaultDate, out var t))
        {
            _minute = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
            if (_minute > _latest) _latest = _minute.Value;
        }
        if (_minute is { } m && (line is { Level: LogLevel.Error, IsEntry: true } || line.IsAlert))
            _counts[m] = _counts.GetValueOrDefault(m) + 1;
    }

    public const int MaxMinutes = 50_000; // ~35 days; a longer span keeps the most recent minutes

    /// Consecutive minutes from the earliest one with an error (or the latest minute seen, if none) to the latest minute seen,
    /// empty minutes included, at most MaxMinutes (the most recent).
    public (DateTime First, int[] Counts) Series()
    {
        if (_minute == null) return (default, []);
        var first = _counts.Count > 0 ? _counts.Keys.Min() : _latest;
        var n = (int)Math.Min((_latest - first).TotalMinutes + 1, MaxMinutes);
        first = _latest.AddMinutes(-(n - 1));
        var counts = new int[n];
        foreach (var (m, c) in _counts)
            if (m >= first && m <= _latest) counts[(int)(m - first).TotalMinutes] = c;
        return (first, counts);
    }
}
