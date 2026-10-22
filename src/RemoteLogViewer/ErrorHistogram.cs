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

    /// The last `maxBars` consecutive minutes ending at the latest minute seen, empty minutes included.
    public (DateTime Minute, int Count)[] Window(int maxBars)
    {
        if (_minute == null || maxBars <= 0) return [];
        var first = _latest.AddMinutes(-(maxBars - 1));
        return Enumerable.Range(0, maxBars).Select(i => first.AddMinutes(i)).Select(m => (m, _counts.GetValueOrDefault(m))).ToArray();
    }
}
