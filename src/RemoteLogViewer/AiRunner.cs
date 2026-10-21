using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteLogViewer;

public record AiRunResult(int? ExitCode, string Error, bool Cancelled);

/// Runs an AI CLI: `cmd /d /s /c "<command>"` (resolves npm `.cmd` shims via PATHEXT) in an empty temp folder,
/// writes `stdin` and closes it, streams stdout to `onOutput` (background thread). Cancel kills the whole process tree.
public static class AiRunner
{
    public const int NotFoundExitCode = 9009; // cmd: "is not recognized as an internal or external command"
    public const int MaxError = 4096;
    private static readonly Regex Ansi = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    /// `onError` gets each non-empty stderr line as it arrives (background thread): tools that retry silently (e.g. a
    /// rate-limited provider) would otherwise look stuck.
    public static async Task<AiRunResult> RunAsync(string command, string stdin, Action<string> onOutput, CancellationToken ct, Action<string>? onError = null)
    {
        if (!CanResolve(command)) return new(NotFoundExitCode, "", false);
        var dir = Path.Combine(Path.GetTempPath(), "RemoteLogViewer", "ai-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var utf8 = new UTF8Encoding(false);
        var psi = new ProcessStartInfo("cmd.exe", "/d /s /c \"" + command + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            WorkingDirectory = dir,
        };
        psi.Environment["NO_COLOR"] = "1";
        using var p = new Process { StartInfo = psi };
        try
        {
            p.Start();
            // Drained in parallel: a full stderr pipe (e.g. Codex progress) would block the tool forever.
            var errorTail = new StringBuilder();
            var error = Task.Run(async () =>
            {
                while (await p.StandardError.ReadLineAsync() is { } line)
                {
                    line = Ansi.Replace(line, "");
                    errorTail.Append(line).Append('\n');
                    if (errorTail.Length > MaxError) errorTail.Remove(0, errorTail.Length - MaxError);
                    if (line.Trim().Length > 0) onError?.Invoke(line);
                }
            });
            var write = Task.Run(async () =>
            {
                try
                {
                    await p.StandardInput.WriteAsync(stdin);
                    p.StandardInput.Close();
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException) { } // tool exited without reading it all
            });
            await using (ct.Register(() => Kill(p)))
            {
                var buffer = new char[4096];
                int n;
                while ((n = await p.StandardOutput.ReadAsync(buffer)) > 0) onOutput(Ansi.Replace(new string(buffer, 0, n), ""));
                await p.WaitForExitAsync();
            }
            await write;
            await error;
            var err = errorTail.ToString();
            return ct.IsCancellationRequested ? new(null, err, true) : new(p.ExitCode, err, false);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private static readonly HashSet<string> Builtins = new(StringComparer.OrdinalIgnoreCase)
    {
        "assoc", "call", "cd", "chdir", "cls", "copy", "date", "del", "dir", "echo", "erase", "exit", "for", "ftype", "if", "md", "mkdir",
        "move", "path", "popd", "pushd", "rd", "ren", "rename", "rmdir", "set", "setlocal", "start", "time", "title", "type", "ver", "vol",
    };

    /// cmd's exit code for an unknown command is not reliable (1 on some Windows builds, not 9009),
    /// so the first token is resolved like cmd does: built-in, or a file on PATH with a PATHEXT extension.
    public static bool CanResolve(string command)
    {
        var t = command.TrimStart(' ', '\t', '@', '(');
        var exe = t.StartsWith('"') ? t[1..].Split('"')[0] : t.Split([' ', '\t', '|', '<', '>', '&', ')'], 2)[0];
        if (exe.Length == 0) return false;
        if (Builtins.Contains(exe)) return true;
        string[] exts = Path.HasExtension(exe) ? [""] : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] dirs = exe.IndexOfAny(['\\', '/', ':']) >= 0 ? [""]
            : (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        return dirs.Any(d => exts.Any(e => File.Exists(Path.Combine(d.Trim('"'), exe + e))));
    }

    private static void Kill(Process p)
    {
        try { p.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { } // already exited
    }
}
