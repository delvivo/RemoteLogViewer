using RemoteLogViewer;

// End-to-end over a real SMB path: the local admin share with the current Windows user.
public class ActiveSessionTests : IDisposable
{
    // localhost does not resolve for SMB on some machines, 127.0.0.1 does.
    private static readonly string? Share = new[] { @"\\localhost\C$", @"\\127.0.0.1\C$" }.FirstOrDefault(s => Directory.Exists(s + @"\"));

    private readonly string _local = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}.log");

    public void Dispose() => File.Delete(_local);

    private SessionConfig Config() => new()
    {
        Name = "local", SharePath = Share!, FilePath = _local[3..], TailLines = 3, // strip "C:\"
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

    private SessionConfig LocalConfig()
    {
        var c = SessionConfig.Local(_local);
        c.TailLines = 3;
        return c;
    }

    // No SMB involved: runs on every machine.
    [Fact]
    public async Task Local_file_tails_appends_truncation_and_waits_when_deleted()
    {
        File.WriteAllText(_local, "INFO 1\nINFO 2\nERROR 3\n   at stack\nWARN 5\n");
        var s = new ActiveSession(LocalConfig());
        Assert.Equal(_local, s.Path);
        Assert.Equal(Path.GetFileName(_local), s.DisplayName);
        s.Start();
        try
        {
            var tail = await Collect(s, 3);
            Assert.Equal(["ERROR 3", "   at stack", "WARN 5"], tail.Select(l => l.Text));
            Assert.Equal(SessionState.Running, s.State);

            File.AppendAllText(_local, "INFO 6\n");
            Assert.Equal("INFO 6", Assert.Single(await Collect(s, 1, 2000)).Text);

            File.WriteAllText(_local, "INFO nuovo\n");
            var after = await Collect(s, 2, 2000);
            Assert.True(after[0].IsMarker);
            Assert.Equal("INFO nuovo", after[1].Text);

            File.Delete(_local);
            await WaitFor(s, SessionState.Waiting);

            File.WriteAllText(_local, "INFO tornato\n");
            var back = await Collect(s, 2, 4000);
            Assert.Contains(back, l => l.Text == "INFO tornato");
            Assert.Equal(SessionState.Running, s.State);
        }
        finally { s.Stop(); }

        await Task.Delay(700);
        Assert.Equal(SessionState.Stopped, s.State);
    }

    private static async Task WaitFor(ActiveSession s, SessionState state, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (s.State != state && DateTime.UtcNow < until) await Task.Delay(50);
        Assert.Equal(state, s.State);
    }

    [Fact]
    public async Task Tails_appends_and_detects_truncation_over_smb()
    {
        if (Share == null) return; // no admin share on this machine

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
    public async Task Today_session_switches_to_next_day_file_at_midnight()
    {
        if (Share == null) return;

        var root = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(root, "2026_01_01"));
        Directory.CreateDirectory(Path.Combine(root, "2026_01_02"));
        File.WriteAllText(Path.Combine(root, "2026_01_01", "log.log"), "INFO day1\n");
        File.WriteAllText(Path.Combine(root, "2026_01_02", "log.log"), "INFO a\nINFO b\nINFO c\nINFO d\n");
        var config = new SessionConfig { Name = "local", SharePath = Share!, FilePath = root[3..] + @"\{date:yyyy_MM_dd}\log.log", TailLines = 3 };
        var now = new DateTime(2026, 1, 1);
        var s = new ActiveSession(config, now, () => now);
        Assert.True(s.FollowToday);
        s.Start();
        try
        {
            Assert.Equal("INFO day1", Assert.Single(await Collect(s, 1)).Text);
            now = new DateTime(2026, 1, 2);
            var after = await Collect(s, 5, 3000);
            Assert.Equal("— nuovo giorno: 2026-01-02 —", after[0].Text);
            Assert.Equal(["INFO a", "INFO b", "INFO c", "INFO d"], after.Skip(1).Select(l => l.Text)); // from start, not TailLines
            Assert.Equal("local · 2026-01-02", s.DisplayName);
        }
        finally { s.Stop(); await Task.Delay(700); Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Undated_current_file_keeps_tailing_across_midnight()
    {
        if (Share == null) return;

        File.WriteAllText(_local, "INFO day1\n");
        var dated = _local[3..].Replace(".log", "{date:'.'yyyy_MM_dd}.log");
        var config = new SessionConfig { Name = "local", SharePath = Share!, FilePath = dated, TailLines = 3, CurrentUndated = true };
        var now = new DateTime(2026, 1, 1);
        var s = new ActiveSession(config, now, () => now);
        Assert.Equal(Share + @"\" + _local[3..], s.Path);
        s.Start();
        try
        {
            Assert.Equal("INFO day1", Assert.Single(await Collect(s, 1)).Text);
            now = new DateTime(2026, 1, 2);
            File.AppendAllText(_local, "INFO day2\n");
            var after = await Collect(s, 2, 3000);
            Assert.Equal(["— nuovo giorno: 2026-01-02 —", "INFO day2"], after.Select(l => l.Text)); // same file, no re-read
            Assert.Equal(Share + @"\" + _local[3..], s.Path);
        }
        finally { s.Stop(); await Task.Delay(700); }
    }

    [Fact]
    public async Task Past_date_session_does_not_switch_day()
    {
        if (Share == null) return;

        File.WriteAllText(_local, "INFO past\n");
        var config = new SessionConfig { Name = "local", SharePath = Share!, FilePath = _local[3..], TailLines = 3 };
        var now = new DateTime(2026, 1, 5);
        var s = new ActiveSession(config, new DateTime(2026, 1, 1), () => now);
        Assert.False(s.FollowToday);
        s.Start();
        try
        {
            Assert.Single(await Collect(s, 1));
            now = new DateTime(2026, 1, 6);
            Assert.Empty(await Collect(s, 1, 1500));
            Assert.Equal(new DateTime(2026, 1, 1), s.Date);
        }
        finally { s.Stop(); }
    }

    [Fact]
    public async Task Missing_file_waits_then_reads_when_it_appears()
    {
        if (Share == null) return;

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
