using RemoteLogViewer;

// End-to-end over a real SMB path: \\localhost\C$ with the current Windows user.
public class ActiveSessionTests : IDisposable
{
    private readonly string _local = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}.log");

    public void Dispose() => File.Delete(_local);

    private SessionConfig Config() => new()
    {
        Name = "local", SharePath = @"\\localhost\C$", FilePath = _local[3..], TailLines = 3, // strip "C:\"
    };

    private static async Task<List<LogLine>> Collect(ActiveSession s, int count, int timeoutMs = 5000)
    {
        var got = new List<LogLine>();
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (got.Count < count && DateTime.UtcNow < until)
        {
            while (s.Pending.TryDequeue(out var l)) got.Add(l);
            await Task.Delay(50);
        }
        return got;
    }

    [Fact]
    public async Task Tails_appends_and_detects_truncation_over_smb()
    {
        if (!Directory.Exists(@"\\localhost\C$\")) return; // no admin share on this machine

        File.WriteAllText(_local, "INFO 1\nINFO 2\nERROR 3\n   at stack\nWARN 5\n");
        var s = new ActiveSession(Config());
        s.Start();
        try
        {
            var tail = await Collect(s, 3);
            Assert.Equal(["ERROR 3", "   at stack", "WARN 5"], tail.Select(l => l.Text));
            Assert.Equal([LogLevel.Error, LogLevel.Error, LogLevel.Warn], tail.Select(l => l.Level));
            Assert.Equal(SessionState.Running, s.State);

            File.AppendAllText(_local, "INFO 6\n");
            Assert.Equal("INFO 6", Assert.Single(await Collect(s, 1, 2000)).Text); // FR-004: within 2 s

            File.WriteAllText(_local, "INFO nuovo\n");
            var after = await Collect(s, 2, 2000);
            Assert.True(after[0].IsMarker);
            Assert.Equal("INFO nuovo", after[1].Text);
        }
        finally { s.Stop(); }

        await Task.Delay(700);
        Assert.Equal(SessionState.Stopped, s.State);
    }

    [Fact]
    public async Task Missing_file_waits_then_reads_when_it_appears()
    {
        if (!Directory.Exists(@"\\localhost\C$\")) return;

        var s = new ActiveSession(Config());
        s.Start();
        try
        {
            await Task.Delay(1000);
            Assert.Equal(SessionState.Waiting, s.State);
            File.WriteAllText(_local, "INFO eccomi\n");
            Assert.Equal("INFO eccomi", Assert.Single(await Collect(s, 1, 4000)).Text);
        }
        finally { s.Stop(); }
    }
}
