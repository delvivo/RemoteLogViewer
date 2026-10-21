using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RemoteLogViewer;

/// An external AI CLI. Everything (prompt + logs) goes to its stdin; the command runs in `cmd /c` (see AiRunner).
/// `Name` is translated on read so it follows a language switch.
public record AiTool(string Id, string BaseName, string DefaultCommand)
{
    public string Name => L.T(BaseName);

    // Non-interactive, read-only / no tools. Checked against each CLI's --help (2026-10).
    public static readonly IReadOnlyList<AiTool> All =
    [
        new("claude", "Claude Code", "claude -p --tools \"\" --no-session-persistence"),
        new("codex", "Codex", "codex exec --skip-git-repo-check --sandbox read-only --ephemeral -"),
        new("opencode", "OpenCode", "opencode run --agent plan"),
        new("gemini", "Gemini CLI (Antigravity)", "gemini --approval-mode plan -p \"Follow the instructions above.\""),
        new("custom", "Personalizzato", "claude -p"),
    ];

    public static AiTool Get(string id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}

/// A preset prompt in the active language (rebuilt on every call).
public record AiPrompt(string Name, string Text)
{
    public static IReadOnlyList<AiPrompt> All() =>
    [
        new(L.T("Report errori"), L.T("Analizza i log qui sotto e scrivi un report degli errori in Markdown. Inizia con un breve riepilogo. Poi raggruppa errori ed eccezioni per causa e, per ogni gruppo, indica: numero di occorrenze, prima e ultima occorrenza (timestamp), schede coinvolte, messaggio tipico, possibili cause e azioni suggerite. Ordina i gruppi per gravità e frequenza. Rispondi in italiano.")),
        new(L.T("Riepilogo"), L.T("Riassumi i log qui sotto in Markdown: stato generale, eventi rilevanti in ordine cronologico, anomalie (errori, avvisi ripetuti, pause, riavvii) e cosa conviene controllare. Rispondi in italiano.")),
    ];
}

/// One tab's lines, captured on the UI thread when the analysis starts.
public record AiSource(string Name, string Path, IReadOnlyList<string> Lines);

public record AiInput(string Text, int Dropped)
{
    public const int MaxChars = 1_000_000;

    /// Prompt + one `=== name (path) ===` section per source. Lines are budgeted on `maxChars` (+1 per newline):
    /// fair share per source, what a small source leaves goes to the others; each source keeps its most recent lines.
    public static AiInput Build(string prompt, IReadOnlyList<AiSource> sources, int maxChars = MaxChars)
    {
        var keep = new int[sources.Count];
        var remaining = maxChars;
        var order = Enumerable.Range(0, sources.Count).OrderBy(i => sources[i].Lines.Sum(l => l.Length + 1)).ToList();
        for (var k = 0; k < order.Count; k++)
        {
            var lines = sources[order[k]].Lines;
            var quota = remaining / (order.Count - k);
            var used = 0;
            var n = 0;
            for (var i = lines.Count - 1; i >= 0 && used + lines[i].Length + 1 <= quota; i--, n++) used += lines[i].Length + 1;
            keep[order[k]] = n;
            remaining -= used;
        }

        var dropped = sources.Select((s, i) => s.Lines.Count - keep[i]).Sum();
        var sb = new StringBuilder(prompt.TrimEnd()).Append("\n\n");
        if (dropped > 0) sb.Append(L.F("({0} righe più vecchie omesse per limite di dimensione)", dropped)).Append("\n\n");
        for (var i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            sb.Append($"=== {s.Name} ({s.Path}) ===\n");
            for (var j = s.Lines.Count - keep[i]; j < s.Lines.Count; j++) sb.Append(s.Lines[j]).Append('\n');
            sb.Append('\n');
        }
        return new AiInput(sb.ToString(), dropped);
    }
}

/// `ai.json` next to sessions.json: separate file, so it is never part of a sessions export.
public sealed class AiSettings
{
    public static string DefaultPath => Path.Combine(Path.GetDirectoryName(SessionStore.DefaultPath)!, "ai.json");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string SelectedTool { get; set; } = "claude";
    public Dictionary<string, string> Commands { get; set; } = [];
    public string? LastPrompt { get; set; }
    public List<string> Consented { get; set; } = [];

    public string CommandFor(string id) => Commands.TryGetValue(id, out var c) ? c : AiTool.Get(id).DefaultCommand;

    /// Empty or equal to the default removes the override ("Ripristina").
    public void SetCommand(string id, string command)
    {
        command = command.Trim();
        if (command.Length == 0 || command == AiTool.Get(id).DefaultCommand) Commands.Remove(id);
        else Commands[id] = command;
    }

    public static AiSettings Load(string path)
    {
        try
        {
            var s = File.Exists(path) ? JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(path), Json) : null;
            if (s == null) return new();
            s.Commands ??= [];
            s.Consented ??= [];
            s.SelectedTool ??= "claude";
            return s;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, true);
    }
}

/// What gets exported / opened formatted: a header describing the run, then the response as-is.
public record AiReport(DateTime Time, string Tool, string Command, IReadOnlyList<string> Sources, string Prompt, int Dropped, string Response)
{
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(L.T("Analisi IA")).Append("\n\n");
        sb.Append($"- **{L.T("Data")}**: {Time:yyyy-MM-dd HH:mm:ss}\n");
        sb.Append($"- **{L.T("Strumento")}**: {Tool} (`{Command}`)\n");
        sb.Append($"- **{L.T("Schede")}**: {string.Join(", ", Sources)}\n");
        if (Dropped > 0) sb.Append($"- **{L.T("Righe escluse")}**: {Dropped:N0}\n");
        sb.Append('\n');
        foreach (var line in Prompt.TrimEnd().Split('\n')) sb.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
        sb.Append("\n---\n\n").Append(Response.TrimEnd()).Append('\n');
        return sb.ToString();
    }

    public string ToHtml() => $$"""
        <!doctype html>
        <html lang="{{L.Lang}}">
        <head>
        <meta charset="utf-8">
        <title>{{WebUtility.HtmlEncode(L.T("Analisi IA"))}} {{Time:yyyy-MM-dd HH:mm}}</title>
        <style>
        body { font-family: "Segoe UI", sans-serif; max-width: 960px; margin: 2em auto; padding: 0 1em; line-height: 1.5; color: #222; }
        pre { background: #f4f4f4; padding: .8em; overflow-x: auto; }
        code { font-family: Consolas, monospace; background: #f4f4f4; padding: 0 .2em; }
        pre code { padding: 0; }
        table { border-collapse: collapse; margin: 1em 0; }
        th, td { border: 1px solid #ccc; padding: .3em .6em; text-align: left; vertical-align: top; }
        th { background: #eee; }
        blockquote { border-left: 4px solid #ccc; margin: 0; padding: 0 1em; color: #555; }
        </style>
        </head>
        <body>
        {{MarkdownHtml.ToHtml(ToMarkdown())}}
        </body>
        </html>
        """;
}
