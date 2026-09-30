using System.ComponentModel;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

public enum LogLevel { None, Debug, Info, Warn, Error }

// Class, not record: identical log lines must stay distinct items for ListBox selection.
public class LogLine(string text, LogLevel level, bool isMarker = false, bool isEntry = false, bool isHistory = false) : INotifyPropertyChanged
{
    public string Text { get; } = text;
    public LogLevel Level { get; } = level;
    public bool IsMarker { get; } = isMarker;
    public bool IsEntry { get; } = isEntry;     // level detected on this line: start of an entry (a stack trace is one entry)
    public bool IsHistory { get; } = isHistory; // read when opening / loaded on request: never raises alerts

    private bool _isMatch;
    public bool IsMatch // search highlight
    {
        get => _isMatch;
        set { if (_isMatch != value) { _isMatch = value; PropertyChanged?.Invoke(this, new(nameof(IsMatch))); } }
    }

    private bool _isContext;
    public bool IsContext // shown as context around a filter match, not as a match
    {
        get => _isContext;
        set { if (_isContext != value) { _isContext = value; PropertyChanged?.Invoke(this, new(nameof(IsContext))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => Text;
}

public static partial class LogLevels
{
    [GeneratedRegex(@"\b(FATAL|CRITICAL|ERROR|ERR|WARN|WARNING|INFO|DEBUG|TRACE|VERBOSE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    /// Line with its level; `last` carries the level to continuation lines (they are not entries).
    public static LogLine Make(string line, ref LogLevel last, bool history)
    {
        var own = Detect(line, LogLevel.None);
        if (own != LogLevel.None) last = own;
        return new LogLine(line, last, isEntry: own != LogLevel.None, isHistory: history);
    }

    // Lines without a level (stack traces, wrapped text) inherit the previous line's level.
    public static LogLevel Detect(string line, LogLevel previous)
    {
        var m = LevelRegex().Match(line);
        if (!m.Success) return previous;
        return m.Value.ToUpperInvariant() switch
        {
            "FATAL" or "CRITICAL" or "ERROR" or "ERR" => LogLevel.Error,
            "WARN" or "WARNING" => LogLevel.Warn,
            "INFO" => LogLevel.Info,
            _ => LogLevel.Debug,
        };
    }
}
