namespace RemoteLogViewer;

public static class Bookmarks
{
    /// Index of the next (dir = 1) or previous (dir = -1) bookmarked line from `selected`, wrapping; -1 if none.
    public static int Next(IReadOnlyList<LogLine> lines, int selected, int dir)
    {
        var n = lines.Count;
        var start = selected >= 0 ? selected : (dir > 0 ? -1 : n);
        for (var k = 1; k <= n; k++)
        {
            var i = ((start + dir * k) % n + n) % n;
            if (lines[i].IsBookmarked) return i;
        }
        return -1;
    }
}
