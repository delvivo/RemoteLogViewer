using System.Text.RegularExpressions;

namespace RemoteLogViewer;

/// Timestamp at the start of a log line: ISO, dd/MM/yyyy, or time only (date = `defaultDate`). Offsets/`Z` are ignored: no time zone conversion.
public static partial class LogTimestamp
{
    [GeneratedRegex(@"^\s*\[?(?:(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})[T ]|(?<d2>\d{2})/(?<mo2>\d{2})/(?<y2>\d{4}) )?(?<h>\d{2}):(?<mi>\d{2}):(?<s>\d{2})(?:[.,](?<f>\d{1,7}))?")]
    private static partial Regex StartRegex();

    public static bool TryParse(string line, DateTime defaultDate, out DateTime value)
    {
        value = default;
        var m = StartRegex().Match(line);
        if (!m.Success) return false;
        try
        {
            var date = defaultDate.Date;
            if (m.Groups["y"].Success) date = new DateTime(Num(m, "y"), Num(m, "mo"), Num(m, "d"));
            else if (m.Groups["y2"].Success) date = new DateTime(Num(m, "y2"), Num(m, "mo2"), Num(m, "d2"));
            value = date.AddHours(Num(m, "h")).AddMinutes(Num(m, "mi")).AddSeconds(Num(m, "s"));
            if (m.Groups["f"].Success) value = value.AddTicks(long.Parse(m.Groups["f"].Value.PadRight(7, '0')));
            return Num(m, "h") < 24 && Num(m, "mi") < 60 && Num(m, "s") < 60;
        }
        catch (ArgumentOutOfRangeException) { return false; } // e.g. month 13
    }

    private static int Num(Match m, string group) => int.Parse(m.Groups[group].Value);

    private static readonly string[] TargetFormats = ["H:mm", "H:mm:ss", "yyyy-MM-dd H:mm", "yyyy-MM-dd H:mm:ss", "dd/MM/yyyy H:mm", "dd/MM/yyyy H:mm:ss"];

    /// "Go to time" input: a time (date = `defaultDate`) or a date and time.
    public static bool TryParseTarget(string text, DateTime defaultDate, out DateTime value)
    {
        if (!DateTime.TryParseExact(text.Trim(), TargetFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.NoCurrentDateDefault, out value))
            return false;
        if (value.Date == DateTime.MinValue.Date) value = defaultDate.Date + value.TimeOfDay; // time only
        return true;
    }

    /// Index of the first line whose timestamp (own, or inherited from the previous timestamped line) is ≥ `target`.
    /// Lines before the first timestamp are skipped. `BeforeFirst` = found, but `target` is strictly earlier than every timestamp.
    public static (int Index, GoToStatus Status) FindAtOrAfter(IReadOnlyList<LogLine> lines, DateTime target, DateTime defaultDate)
    {
        var any = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsMarker || !TryParse(lines[i].Text, defaultDate, out var t)) continue; // inherited: an earlier line already decided
            if (t >= target) return (i, any || t == target ? GoToStatus.Found : GoToStatus.BeforeFirst);
            any = true;
        }
        return (-1, any ? GoToStatus.NotFound : GoToStatus.NoTimestamps);
    }
}

public enum GoToStatus { Found, BeforeFirst, NotFound, NoTimestamps }
