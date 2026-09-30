using System.IO;
using System.Text;

namespace RemoteLogViewer;

/// Get-Content -Tail N -Wait: ReadTail once, then Poll periodically.
/// Opens a short-lived handle per call so server-side rotation is never blocked.
public class LogTailer(string path, string encoding = "auto")
{
    static LogTailer() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const int Block = 256 * 1024;

    private long _offset;
    private long _start; // preamble (BOM) length
    private long _head;  // byte offset of the oldest line handed out
    private DateTime _created;
    private Encoding _enc = Encoding.UTF8;
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _partial = new();

    public string Path => path;

    /// Lines before the oldest one read so far are still in the file (ReadBefore can return more).
    public bool HasOlder => _head > _start;

    public List<string> ReadTail(int n)
    {
        using var fs = Open();
        _start = DetectEncoding(fs);
        var len = fs.Length;
        _created = File.GetCreationTimeUtc(path);
        _offset = len;
        _decoder = _enc.GetDecoder();
        _partial.Clear();

        var tailEnd = FindBack(fs, len, 1); // start of the incomplete last line (== len if the file ends with a newline)
        _head = n == 0 || tailEnd == _start ? tailEnd : FindBack(fs, tailEnd - Unit, n);
        _partial.Append(Decode(fs, tailEnd, len));
        return Split(Decode(fs, _head, tailEnd), out _);
    }

    /// The n lines right before the oldest one read so far (for "load previous lines").
    public List<string> ReadBefore(int n)
    {
        if (!HasOlder || n <= 0) return [];
        using var fs = Open();
        var head = FindBack(fs, _head - Unit, n);
        var lines = Split(Decode(fs, head, _head), out _);
        _head = head;
        return lines;
    }

    public List<string> Poll(out bool rotated)
    {
        using var fs = Open();
        var len = fs.Length;
        var created = File.GetCreationTimeUtc(path);
        rotated = len < _offset || created != _created;
        if (rotated)
        {
            _offset = _start = _head = DetectEncoding(fs); // new file read from its start: nothing older
            _created = created;
            _decoder = _enc.GetDecoder();
            _partial.Clear();
        }
        if (len == _offset) return [];

        fs.Position = _offset;
        var buf = new byte[len - _offset];
        fs.ReadExactly(buf);
        _offset = len;
        var chars = new char[_enc.GetMaxCharCount(buf.Length)];
        var count = _decoder.GetChars(buf, 0, buf.Length, chars, 0);
        _partial.Append(chars, 0, count);
        var lines = Split(_partial.ToString(), out var tail);
        _partial.Clear().Append(tail);
        return lines;
    }

    private int Unit => _enc is UnicodeEncoding ? 2 : 1;

    /// Position right after the count-th newline found scanning backward from `from` (exclusive), or _start.
    /// Works on bytes, so offsets are exact whatever the text contains; UTF-16 newlines are matched on 2-byte boundaries.
    private long FindBack(FileStream fs, long from, int count)
    {
        var u = Unit;
        var bigEndian = _enc.CodePage == 1201;
        var buf = new byte[Block];
        for (var end = from; end > _start;)
        {
            var start = Math.Max(_start, end - Block);
            start += (start - _start) % u;
            var len = (int)(end - start);
            fs.Position = start;
            fs.ReadExactly(buf, 0, len);
            for (var i = len - u; i >= 0; i -= u)
            {
                var nl = u == 1 ? buf[i] == 0x0A : bigEndian ? buf[i] == 0 && buf[i + 1] == 0x0A : buf[i] == 0x0A && buf[i + 1] == 0;
                if (nl && --count == 0) return start + i + u;
            }
            end = start;
        }
        return _start;
    }

    private string Decode(FileStream fs, long from, long to)
    {
        var buf = new byte[to - from];
        fs.Position = from;
        fs.ReadExactly(buf);
        return _enc.GetString(buf);
    }

    private FileStream Open() => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    // Returns preamble length. BOM wins over configured encoding; no BOM + "auto" = UTF-8.
    private int DetectEncoding(FileStream fs)
    {
        Span<byte> b = stackalloc byte[3];
        fs.Position = 0;
        var read = fs.Read(b);
        if (read >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { _enc = new UTF8Encoding(false); return 3; }
        if (read >= 2 && b[0] == 0xFF && b[1] == 0xFE) { _enc = new UnicodeEncoding(false, false); return 2; }
        if (read >= 2 && b[0] == 0xFE && b[1] == 0xFF) { _enc = new UnicodeEncoding(true, false); return 2; }
        _enc = encoding switch
        {
            "utf-16" => new UnicodeEncoding(false, false),
            "windows-1252" => Encoding.GetEncoding(1252),
            _ => new UTF8Encoding(false),
        };
        return 0;
    }

    private static List<string> Split(string text, out string tail)
    {
        var parts = text.Split('\n');
        tail = parts[^1];
        return parts[..^1].Select(p => p.TrimEnd('\r')).ToList();
    }
}
