using System.Diagnostics;
using System.Text;
using RemoteLogViewer;

public class AiRunnerTests
{
    private static async Task<(AiRunResult Result, string Output)> Run(string command, string stdin = "", CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var result = await AiRunner.RunAsync(command, stdin, s => { lock (sb) sb.Append(s); }, ct);
        return (result, sb.ToString());
    }

    [Fact]
    public async Task Stdin_reaches_the_command_and_stdout_comes_back()
    {
        var (result, output) = await Run("findstr /n \"^\"", "prima riga\nseconda è à\n");

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Cancelled);
        Assert.Contains("1:prima riga", output);
        Assert.Contains("2:seconda è à", output);
    }

    [Fact]
    public async Task Missing_command_exits_9009()
    {
        var (result, _) = await Run("nonesiste_xyz");
        Assert.Equal(AiRunner.NotFoundExitCode, result.ExitCode);
    }

    [Fact]
    public async Task Stderr_lines_are_reported_while_running()
    {
        var lines = new List<string>();
        var result = await AiRunner.RunAsync("(echo primo 1>&2) & echo. 1>&2 & (echo secondo 1>&2) & echo fatto", "", _ => { },
            CancellationToken.None, l => { lock (lines) lines.Add(l.Trim()); });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["primo", "secondo"], lines); // blank lines skipped
        Assert.Contains("secondo", result.Error);
    }

    [Fact]
    public void CanResolve_finds_builtins_path_and_absolute_commands()
    {
        Assert.True(AiRunner.CanResolve("findstr /n \"^\""));
        Assert.True(AiRunner.CanResolve("@for /l %i in (1,1,2) do @echo x"));
        Assert.True(AiRunner.CanResolve("(echo a) & echo b"));
        Assert.True(AiRunner.CanResolve("\"C:\\Windows\\System32\\cmd.exe\" /c ver"));
        Assert.False(AiRunner.CanResolve("nonesiste_xyz -p"));
        Assert.False(AiRunner.CanResolve("   "));
    }

    [Fact]
    public async Task Large_stderr_does_not_block_and_is_trimmed()
    {
        var run = Run("for /l %i in (1,1,5000) do @echo errore riga numero %i 1>&2");
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(30_000)));

        var (result, _) = await run;
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Error.Length <= AiRunner.MaxError);
        Assert.Contains("errore riga numero 5000", result.Error);
    }

    [Fact]
    public async Task Cancel_kills_the_process_tree_quickly()
    {
        using var cts = new CancellationTokenSource(300);
        var sw = Stopwatch.StartNew();
        var (result, _) = await Run("ping -n 30 127.0.0.1", ct: cts.Token);

        Assert.True(result.Cancelled);
        Assert.Null(result.ExitCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), sw.Elapsed.ToString());
    }

    [Fact]
    public async Task Working_folder_is_temporary_and_removed()
    {
        var (_, output) = await Run("cd");
        var dir = output.Trim();

        Assert.Contains(Path.Combine("RemoteLogViewer", "ai-"), dir);
        Assert.False(Directory.Exists(dir));
    }
}
