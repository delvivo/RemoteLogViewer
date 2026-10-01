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
}
