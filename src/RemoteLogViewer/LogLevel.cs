using System.ComponentModel;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

public enum LogLevel { None, Debug, Info, Warn, Error }

// Class, not record: identical log lines must stay distinct items for ListBox selection.
public class LogLine(string text, LogLevel level, bool isMarker = false) : INotifyPropertyChanged
{
    public string Text { get; } = text;
    public LogLevel Level { get; } = level;
    public bool IsMarker { get; } = isMarker;

    private bool _isMatch;
    public bool IsMatch // search highlight
    {
        get => _isMatch;
        set { if (_isMatch != value) { _isMatch = value; PropertyChanged?.Invoke(this, new(nameof(IsMatch))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => Text;
}

public static partial class LogLevels
{
    [GeneratedRegex(@"\b(FATAL|CRITICAL|ERROR|ERR|WARN|WARNING|INFO|DEBUG|TRACE|VERBOSE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

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
