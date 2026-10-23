using System.Diagnostics;
using RemoteLogViewer;

public class TimelineMergeTests
{
    private static readonly DateTime Day = new(2026, 10, 1);

    private static List<LogLine> Lines(params string[] texts)
    {
        var last = LogLevel.None;
        return texts.Select(t => LogLevels.Make(t, ref last, false)).ToList();
    }

    private static string At(string time, string text = "x") => $"2026-10-01 {time} INFO {text}";

    private static string[] Texts(TimelineMerge m) => m.Rows.Select(r => r.Text).ToArray();

    [Fact]
    public void Two_sessions_interleave_by_timestamp_with_their_session_attached()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        m.Add(a, Lines(At("10:00:01", "a1"), At("10:00:03", "a3")));
        m.Add(b, Lines(At("10:00:02", "b2")));
        Assert.Equal([At("10:00:01", "a1"), At("10:00:02", "b2"), At("10:00:03", "a3")], Texts(m));
        Assert.Equal(["A", "B", "A"], m.Rows.Select(r => r.Session.Name));
    }

    [Fact]
    public void Line_without_timestamp_inherits_and_stays_after_its_head_line()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        m.Add(a, Lines(At("10:00:01", "a1"), "   at Foo.Bar()", At("10:00:05", "a5")));
        m.Add(b, Lines(At("10:00:02", "b2")));
        Assert.Equal([At("10:00:01", "a1"), "   at Foo.Bar()", At("10:00:02", "b2"), At("10:00:05", "a5")], Texts(m));
    }

    [Fact]
    public void Marker_lines_inherit_the_previous_time()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var marker = new LogLine("--- new day ---", LogLevel.None, isMarker: true);
        m.Add(a, [Lines(At("10:00:01"))[0], marker]);
        Assert.Equal(m.Rows[0].Time, m.Rows[1].Time);
    }

    [Fact]
    public void Equal_timestamps_order_by_session_then_arrival_whatever_the_feed_order()
    {
        TimelineMerge Build(bool bFirst)
        {
            var m = new TimelineMerge();
            var a = m.AddSession("A", Day);
            var b = m.AddSession("B", Day);
            if (bFirst) m.Add(b, Lines(At("10:00:01", "b1"), At("10:00:01", "b2")));
            m.Add(a, Lines(At("10:00:01", "a1"), At("10:00:01", "a2")));
            if (!bFirst) m.Add(b, Lines(At("10:00:01", "b1"), At("10:00:01", "b2")));
            return m;
        }
        var expected = new[] { At("10:00:01", "a1"), At("10:00:01", "a2"), At("10:00:01", "b1"), At("10:00:01", "b2") };
        Assert.Equal(expected, Texts(Build(true)));
        Assert.Equal(expected, Texts(Build(false)));
    }

    [Fact]
    public void Add_returns_true_for_in_order_batches_and_false_when_a_late_line_lands_in_the_middle()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        Assert.True(m.Add(a, Lines(At("10:00:01", "a1"), At("10:00:03", "a3"))));
        Assert.True(m.Add(a, Lines(At("10:00:04", "a4"))));
        Assert.False(m.Add(b, Lines(At("10:00:02", "b2"))));
        Assert.Equal([At("10:00:01", "a1"), At("10:00:02", "b2"), At("10:00:03", "a3"), At("10:00:04", "a4")], Texts(m));
    }

    [Fact]
    public void Prepended_block_lands_before_the_existing_rows_of_its_session_in_file_order()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        m.Add(a, Lines(At("10:00:05", "a5")));
        Assert.False(m.Add(a, Lines(At("10:00:01", "a1"), At("10:00:02", "a2")), prepend: true));
        Assert.Equal([At("10:00:01", "a1"), At("10:00:02", "a2"), At("10:00:05", "a5")], Texts(m));
    }

    [Fact]
    public void Leading_untimed_rows_are_retimed_after_a_prepend()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        m.Add(a, Lines("   at Orphan()", At("10:00:09", "a9")));
        m.Add(b, Lines(At("10:00:04", "b4")));
        Assert.Equal("   at Orphan()", m.Rows[0].Text); // MinValue: first
        m.Add(a, Lines(At("10:00:01", "a1"), At("10:00:02", "a2")), prepend: true);
        Assert.Equal([At("10:00:01", "a1"), At("10:00:02", "a2"), "   at Orphan()", At("10:00:04", "b4"), At("10:00:09", "a9")], Texts(m));
    }

    [Fact]
    public void Session_without_timestamps_sorts_first_in_arrival_order()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        m.Add(a, Lines(At("10:00:01", "a1")));
        m.Add(b, Lines("plain 1", "plain 2"));
        Assert.Equal(["plain 1", "plain 2", At("10:00:01", "a1")], Texts(m));
        Assert.True(a.HasTimestamps);
        Assert.False(b.HasTimestamps);
    }

    [Fact]
    public void Time_only_timestamps_use_the_session_default_date()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", new DateTime(2026, 3, 4));
        m.Add(a, Lines("10:00:01 INFO hello"));
        Assert.Equal(new DateTime(2026, 3, 4, 10, 0, 1), m.Rows[0].Time);
    }

    [Fact]
    public void Trim_removes_the_oldest_rows_past_the_cap_and_reports_it()
    {
        var m = new TimelineMerge(maxRows: 10, trimBlock: 4);
        var a = m.AddSession("A", Day);
        m.Add(a, Lines(Enumerable.Range(0, 10).Select(i => At($"10:00:{i:00}", $"n{i}")).ToArray()));
        Assert.False(m.Trim());
        m.Add(a, Lines(At("10:00:10", "n10")));
        Assert.True(m.Trim());
        Assert.Equal(6, m.Rows.Count); // 11 - (11 - 10 + 4)
        Assert.Equal(At("10:00:05", "n5"), m.Rows[0].Text);
    }

    [Fact]
    public void Merged_line_is_a_copy_so_context_marking_does_not_touch_the_source()
    {
        var m = new TimelineMerge();
        var a = m.AddSession("A", Day);
        var src = Lines(At("10:00:01"));
        m.Add(a, src);
        m.Rows[0].IsContext = true;
        Assert.False(src[0].IsContext);
        Assert.Same(src[0], m.Rows[0].Original);
    }

    [Fact]
    public void Two_hundred_thousand_interleaved_lines_merge_quickly()
    {
        string[] Make(int offset) => Enumerable.Range(0, 100_000).Select(i => $"{Day.AddSeconds(i * 2 + offset):yyyy-MM-dd HH:mm:ss} INFO l{i}").ToArray();
        var m = new TimelineMerge(maxRows: 500_000);
        var a = m.AddSession("A", Day);
        var b = m.AddSession("B", Day);
        var la = Lines(Make(0));
        var lb = Lines(Make(1));
        var sw = Stopwatch.StartNew();
        m.Add(a, la);
        m.Add(b, lb);
        sw.Stop();
        Assert.Equal(200_000, m.Rows.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"took {sw.Elapsed}");
        for (var i = 1; i < m.Rows.Count; i++) Assert.True(m.Rows[i - 1].Time <= m.Rows[i].Time);
    }
}
