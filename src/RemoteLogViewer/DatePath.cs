using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

/// `{date:yyyy_MM_dd}` placeholders in a file path, resolved with the date chosen when a session starts.
public static partial class DatePath
{
    [GeneratedRegex(@"\{date:([^{}]*)\}")]
    private static partial Regex Placeholder();

    private static readonly DateTime Sample = new(2026, 12, 31);

    public static bool Has(string path) => Placeholder().IsMatch(path);

    public static string Resolve(string path, DateTime date) =>
        Placeholder().Replace(path, m => date.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture));

    /// Path with every placeholder removed (today's undated file, e.g. `app{date:'.'yyyy-MM-dd}.log` → `app.log`).
    public static string Strip(string path) => Placeholder().Replace(path, "");

    public static List<string> Validate(string path)
    {
        var errors = new List<string>();
        foreach (Match m in Placeholder().Matches(path))
        {
            var fmt = m.Groups[1].Value;
            if (fmt.Length == 0) { errors.Add(L.T("Formato data vuoto.")); continue; }
            if (HasTimeComponent(fmt)) { errors.Add(L.T("Il formato data non può contenere ore/minuti/secondi.")); continue; }
            string resolved;
            try { resolved = Sample.ToString(fmt, CultureInfo.InvariantCulture); }
            catch (FormatException) { errors.Add(L.F("Formato data non valido: {0}.", fmt)); continue; }
            if (resolved.IndexOfAny(@"\/:*?""<>|".ToCharArray()) >= 0)
                errors.Add(L.T("Il formato data produce caratteri non ammessi nel percorso."));
        }
        return errors;
    }

    // Format letters for hours/minutes/seconds/fractions/AM-PM/time zone, outside quotes and escapes.
    private static bool HasTimeComponent(string fmt)
    {
        char? quote = null;
        for (var i = 0; i < fmt.Length; i++)
        {
            var c = fmt[i];
            if (quote != null) { if (c == quote) quote = null; continue; }
            if (c is '\'' or '"') quote = c;
            else if (c == '\\') i++;
            else if ("HhmsFftzK".Contains(c)) return true;
        }
        return false;
    }
}
