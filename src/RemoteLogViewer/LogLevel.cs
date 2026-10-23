using System.ComponentModel;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

public enum LogLevel { None, Debug, Info, Warn, Error }

// Class, not record: identical log lines must stay distinct items for ListBox selection.
public class LogLine(string text, LogLevel level, bool isMarker = false, bool isEntry = false, bool isHistory = false, bool isAlert = false) : INotifyPropertyChanged
{
    public string Text { get; } = text;
    public LogLevel Level { get; } = level;
    public bool IsMarker { get; } = isMarker;
    public bool IsEntry { get; } = isEntry;     // level detected on this line: start of an entry (a stack trace is one entry)
    public bool IsHistory { get; } = isHistory; // read when opening / loaded on request: never raises alerts
    public bool IsAlert { get; } = isAlert;     // new line matching the session's alert pattern

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

    private bool _isBookmarked;
    public bool IsBookmarked // user bookmark (F2); lives on the object, so it survives Rebuild and trim
    {
        get => _isBookmarked;
        set { if (_isBookmarked != value) { _isBookmarked = value; PropertyChanged?.Invoke(this, new(nameof(IsBookmarked))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => Text;
}

public static partial class LogLevels
{
    [GeneratedRegex(@"\b(FATAL|CRITICAL|ERROR|ERR|WARN|WARNING|INFO|DEBUG|TRACE|VERBOSE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    /// Line with its level; `last` carries the level to continuation lines (they are not entries).
    public static LogLine Make(string line, ref LogLevel last, bool history, LineRules? rules = null)
    {
        rules ??= LineRules.Default;
        var own = rules.Detect(line);
        if (own != LogLevel.None) last = own;
        return new LogLine(line, last, isEntry: own != LogLevel.None, isHistory: history, isAlert: !history && rules.IsAlert(line));
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

/// Per-session patterns: custom levels (checked in severity order, before the built-in detection) and the alert pattern.
/// Case-insensitive, 50 ms timeout per line (a timeout = no match). An invalid pattern is ignored: the editor rejects it, but an imported file may carry one.
public sealed class LineRules
{
    public static readonly LineRules Default = new(new SessionConfig());

    private readonly (LogLevel Level, Regex Re)[] _levels;
    private readonly Regex? _alert;

    public LineRules(SessionConfig c)
    {
        _levels = new[] { (LogLevel.Error, c.ErrorPattern), (LogLevel.Warn, c.WarnPattern), (LogLevel.Info, c.InfoPattern), (LogLevel.Debug, c.DebugPattern) }
            .Select(p => (p.Item1, Re: Compile(p.Item2))).Where(p => p.Re != null).Select(p => (p.Item1, p.Re!)).ToArray();
        _alert = Compile(c.AlertPattern);
    }

    /// Level detected on the line itself (None = no level: the caller inherits the previous one).
    public LogLevel Detect(string line)
    {
        foreach (var (level, re) in _levels) if (IsMatch(re, line)) return level;
        return LogLevels.Detect(line, LogLevel.None);
    }

    public bool IsAlert(string line) => _alert != null && IsMatch(_alert, line);

    /// Null if the pattern is empty or valid, otherwise the parser's message.
    public static string? Check(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return null;
        try { _ = new Regex(pattern); return null; }
        catch (ArgumentException e) { return e.Message; }
    }

    private static Regex? Compile(string? pattern) =>
        string.IsNullOrEmpty(pattern) || Check(pattern) != null ? null
            : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private static bool IsMatch(Regex re, string line)
    {
        try { return re.IsMatch(line); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
