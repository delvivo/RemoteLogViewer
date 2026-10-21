using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

/// Minimal Markdown → HTML body fragment for AI reports: headings, paragraphs, bold/italic/code/links, fenced code,
/// flat lists, tables, blockquotes, rules. Everything is HTML-encoded first.
// ponytail: no nested lists, no reference links; add when AI reports need them (or switch to Markdig).
public static partial class MarkdownHtml
{
    [GeneratedRegex(@"^(#{1,6})\s+(.*?)\s*#*\s*$")] private static partial Regex Heading();
    [GeneratedRegex(@"^\s*([-*_])(\s*\1){2,}\s*$")] private static partial Regex Rule();
    [GeneratedRegex(@"^\s*[-*+]\s+(.*)$")] private static partial Regex Bullet();
    [GeneratedRegex(@"^\s*\d+[.)]\s+(.*)$")] private static partial Regex Numbered();
    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$")] private static partial Regex TableSeparator();
    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")] private static partial Regex Bold();
    [GeneratedRegex(@"(?<![\*\w])\*(?!\s)(.+?)(?<!\s)\*(?!\*)")] private static partial Regex Italic();
    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^)\s]+)\)")] private static partial Regex Link();

    public static string ToHtml(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var paragraph = new List<string>();
        string? list = null; // "ul" / "ol" while a list is open

        void Flush()
        {
            if (paragraph.Count > 0) sb.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>\n");
            paragraph.Clear();
            if (list != null) sb.Append($"</{list}>\n");
            list = null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```"))
            {
                Flush();
                sb.Append("<pre><code>");
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```"); i++) sb.Append(WebUtility.HtmlEncode(lines[i])).Append('\n');
                sb.Append("</code></pre>\n");
                continue;
            }
            if (line.Trim().Length == 0) { Flush(); continue; }
            if (Heading().Match(line) is { Success: true } h)
            {
                Flush();
                var n = h.Groups[1].Length;
                sb.Append($"<h{n}>").Append(Inline(h.Groups[2].Value)).Append($"</h{n}>\n");
                continue;
            }
            if (Rule().IsMatch(line)) { Flush(); sb.Append("<hr>\n"); continue; }
            if (line.TrimStart().StartsWith('|') && i + 1 < lines.Length && TableSeparator().IsMatch(lines[i + 1]))
            {
                Flush();
                sb.Append("<table>\n");
                Row(sb, line, "th");
                for (i += 2; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++) Row(sb, lines[i], "td");
                i--;
                sb.Append("</table>\n");
                continue;
            }
            if (line.TrimStart().StartsWith('>'))
            {
                Flush();
                var quote = new List<string>();
                for (; i < lines.Length && lines[i].TrimStart().StartsWith('>'); i++) quote.Add(lines[i].TrimStart()[1..].TrimStart());
                i--;
                sb.Append("<blockquote>").Append(ToHtml(string.Join("\n", quote))).Append("</blockquote>\n");
                continue;
            }
            var bullet = Bullet().Match(line);
            var numbered = bullet.Success ? Match.Empty : Numbered().Match(line);
            if (bullet.Success || numbered.Success)
            {
                var kind = bullet.Success ? "ul" : "ol";
                if (paragraph.Count > 0 || list != kind) Flush();
                if (list == null) { sb.Append($"<{kind}>\n"); list = kind; }
                sb.Append("<li>").Append(Inline((bullet.Success ? bullet : numbered).Groups[1].Value)).Append("</li>\n");
                continue;
            }
            if (list != null) Flush();
            paragraph.Add(line.Trim());
        }
        Flush();
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string line, string cell)
    {
        var t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        sb.Append("<tr>");
        foreach (var c in t.Split('|')) sb.Append($"<{cell}>").Append(Inline(c.Trim())).Append($"</{cell}>");
        sb.Append("</tr>\n");
    }

    /// Code spans first (their content stays literal), then bold, italic, links on the rest.
    private static string Inline(string text)
    {
        var parts = text.Split('`');
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            var encoded = WebUtility.HtmlEncode(parts[i]);
            if (i % 2 == 1 && i < parts.Length - 1) { sb.Append("<code>").Append(encoded).Append("</code>"); continue; }
            if (i % 2 == 1) sb.Append('`'); // unmatched backtick
            encoded = Bold().Replace(encoded, m => $"<strong>{(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)}</strong>");
            encoded = Italic().Replace(encoded, "<em>$1</em>");
            encoded = Link().Replace(encoded, "<a href=\"$2\">$1</a>");
            sb.Append(encoded);
        }
        return sb.ToString();
    }
}
