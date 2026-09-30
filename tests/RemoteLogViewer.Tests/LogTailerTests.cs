using System.Text;
using RemoteLogViewer;

public class LogTailerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}.log");

    public void Dispose() => File.Delete(_path);

    private void Append(string text, Encoding? enc = null) => File.AppendAllText(_path, text, enc ?? new UTF8Encoding(false));

    private static string Lines(int from, int to) => string.Concat(Enumerable.Range(from, to - from + 1).Select(i => $"riga {i}\r\n"));

    [Fact]
    public void ReadTail_returns_last_n_lines()
    {
        Append(Lines(1, 2000));
        var lines = new LogTailer(_path).ReadTail(1000);
        Assert.Equal(1000, lines.Count);
        Assert.Equal("riga 1001", lines[0]);
        Assert.Equal("riga 2000", lines[^1]);
    }

    [Fact]
    public void ReadTail_on_big_file_reads_across_blocks()
    {
        Append(string.Concat(Enumerable.Range(1, 50_000).Select(i => $"{new string('x', 100)} {i}\n")));
        var lines = new LogTailer(_path).ReadTail(3000);
        Assert.Equal(3000, lines.Count);
        Assert.EndsWith(" 47001", lines[0]);
    }

    [Fact]
    public void ReadTail_with_fewer_lines_than_requested_returns_all()
    {
        Append(Lines(1, 5));
        var lines = new LogTailer(_path).ReadTail(1000);
        Assert.Equal(["riga 1", "riga 2", "riga 3", "riga 4", "riga 5"], lines);
    }

    [Fact]
    public void Poll_returns_appended_lines_and_holds_partial_line()
    {
        Append(Lines(1, 3));
        var t = new LogTailer(_path);
        t.ReadTail(10);
        Append("riga 4\r\nriga 5 parz");
        Assert.Equal(["riga 4"], t.Poll(out _));
        Append("iale\r\n");
        Assert.Equal(["riga 5 parziale"], t.Poll(out var rotated));
        Assert.False(rotated);
        Assert.Empty(t.Poll(out _));
    }

    [Fact]
    public void Poll_detects_truncation_and_restarts_from_beginning()
    {
        Append(Lines(1, 100));
        var t = new LogTailer(_path);
        t.ReadTail(10);
        File.WriteAllText(_path, "nuovo\n");
        Assert.Equal(["nuovo"], t.Poll(out var rotated));
        Assert.True(rotated);
    }

    [Fact]
    public void Utf16_with_bom_is_decoded()
    {
        File.WriteAllText(_path, "àèì 1\r\nàèì 2\r\n", Encoding.Unicode);
        var t = new LogTailer(_path);
        Assert.Equal(["àèì 1", "àèì 2"], t.ReadTail(10));
        Append("àèì 3\r\n", new UnicodeEncoding(false, false));
        Assert.Equal(["àèì 3"], t.Poll(out _));
    }

    [Fact]
    public void Utf8_multibyte_split_across_polls_is_not_corrupted()
    {
        Append("a\n");
        var t = new LogTailer(_path);
        t.ReadTail(10);
        var bytes = Encoding.UTF8.GetBytes("è\n");
        using (var fs = new FileStream(_path, FileMode.Append)) fs.Write(bytes, 0, 1);
        Assert.Empty(t.Poll(out _));
        using (var fs = new FileStream(_path, FileMode.Append)) fs.Write(bytes, 1, bytes.Length - 1);
        Assert.Equal(["è"], t.Poll(out _));
    }

    [Fact]
    public void Windows1252_forced_encoding()
    {
        Append("caffè\n", CodePagesEncodingProvider.Instance.GetEncoding(1252));
        Assert.Equal(["caffè"], new LogTailer(_path, "windows-1252").ReadTail(10));
    }
}
