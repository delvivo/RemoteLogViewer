using System.IO;
using System.Text;

namespace RemoteLogViewer;

/// Get-Content -Tail N -Wait: ReadTail once, then Poll periodically.
/// Opens a short-lived handle per call so server-side rotation is never blocked.
public class LogTailer(string path, string encoding = "auto")
{
    static LogTailer() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private long _offset;
    private DateTime _created;
    private Encoding _enc = Encoding.UTF8;
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _partial = new();

    public string Path => path;

    public List<string> ReadTail(int n)
    {
        using var fs = Open();
        var start0 = DetectEncoding(fs);
        var len = fs.Length;
        _created = File.GetCreationTimeUtc(path);
        _offset = len;
        _decoder = _enc.GetDecoder();
        _partial.Clear();

        // Read ever larger chunks from the end until they contain n complete lines (or the whole file).
        for (long size = 64 * 1024; ; size *= 4)
        {
            var start = Math.Max(start0, len - size);
            if (_enc is UnicodeEncoding && (start - start0) % 2 != 0) start++;
            var buf = new byte[len - start];
            fs.Position = start;
            fs.ReadExactly(buf);
            var lines = Split(_enc.GetString(buf), out var tail);
            if (start > start0 && lines.Count > 0) lines.RemoveAt(0); // first line may be cut
            if (lines.Count >= n || start == start0)
            {
                _partial.Append(tail);
                return lines.Count > n ? lines.GetRange(lines.Count - n, n) : lines;
            }
        }
    }

    public List<string> Poll(out bool rotated)
    {
        using var fs = Open();
        var len = fs.Length;
        var created = File.GetCreationTimeUtc(path);
        rotated = len < _offset || created != _created;
        if (rotated)
        {
            _offset = DetectEncoding(fs);
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
