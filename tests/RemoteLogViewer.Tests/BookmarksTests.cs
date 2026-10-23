using RemoteLogViewer;

public class BookmarksTests
{
    private static List<LogLine> Buffer(int n, params int[] marked)
    {
        var l = Enumerable.Range(0, n).Select(i => new LogLine($"riga {i}", LogLevel.Info)).ToList();
        foreach (var i in marked) l[i].IsBookmarked = true;
        return l;
    }

    [Fact]
    public void Next_returns_following_bookmark()
    {
        Assert.Equal(50, Bookmarks.Next(Buffer(100, 10, 50, 90), 20, 1));
        Assert.Equal(10, Bookmarks.Next(Buffer(100, 10, 50, 90), 20, -1));
    }

    [Fact]
    public void Wraps_around_at_the_ends()
    {
        Assert.Equal(10, Bookmarks.Next(Buffer(100, 10, 50, 90), 90, 1));
        Assert.Equal(90, Bookmarks.Next(Buffer(100, 10, 50, 90), 10, -1));
    }

    [Fact]
    public void No_selection_starts_from_the_ends()
    {
        Assert.Equal(10, Bookmarks.Next(Buffer(100, 10, 50, 90), -1, 1));
        Assert.Equal(90, Bookmarks.Next(Buffer(100, 10, 50, 90), -1, -1));
    }

    [Fact]
    public void Single_selected_bookmark_returns_itself()
    {
        Assert.Equal(5, Bookmarks.Next(Buffer(10, 5), 5, 1));
    }

    [Fact]
    public void None_returns_minus_one()
    {
        Assert.Equal(-1, Bookmarks.Next(Buffer(10), 3, 1));
        Assert.Equal(-1, Bookmarks.Next([], -1, 1));
    }

    [Fact]
    public void Identical_text_lines_stay_distinct()
    {
        var a = new LogLine("same", LogLevel.Info);
        var b = new LogLine("same", LogLevel.Info);
        b.IsBookmarked = true;
        Assert.Equal(1, Bookmarks.Next([a, b], -1, 1));
        Assert.False(a.IsBookmarked);
    }
}
