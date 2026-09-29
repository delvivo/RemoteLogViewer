using RemoteLogViewer;

public class SessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}");
    private string FilePath => Path.Combine(_dir, "sessions.json");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Round_trip_keeps_fields_and_password_is_not_plaintext()
    {
        var store = new SessionStore(FilePath);
        var s = new SessionConfig
        {
            Name = "PROD", SharePath = @"\\nts11050\E$", UserName = @"DOM\me",
            ProtectedPassword = SessionStore.Protect("S3gret0!pw"), FilePath = @"logs\app.log", TailLines = 500, Encoding = "utf-16",
        };
        store.Save([s]);

        Assert.DoesNotContain("S3gret0!pw", File.ReadAllText(FilePath));
        Assert.DoesNotContain("fullPath", File.ReadAllText(FilePath));

        var loaded = Assert.Single(new SessionStore(FilePath).Load());
        Assert.Equal(s.Id, loaded.Id);
        Assert.Equal(@"\\nts11050\E$", loaded.SharePath);
        Assert.Equal(@"\\nts11050\E$\logs\app.log", loaded.FullPath);
        Assert.Equal(500, loaded.TailLines);
        Assert.Equal("S3gret0!pw", SessionStore.Unprotect(loaded.ProtectedPassword));
    }

    [Fact]
    public void Missing_file_loads_empty() => Assert.Empty(new SessionStore(FilePath).Load());

    [Fact]
    public void Corrupt_file_is_moved_to_bak()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var store = new SessionStore(FilePath);
        Assert.Empty(store.Load());
        Assert.NotNull(store.Warning);
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void Undecryptable_password_returns_null()
    {
        Assert.Null(SessionStore.Unprotect(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        Assert.Null(SessionStore.Unprotect("not base64!"));
        Assert.Null(SessionStore.Unprotect(null));
    }

    [Theory]
    [InlineData(@"\\srv\share", @"a.log", 0)]
    [InlineData(@"srv\share", @"a.log", 1)]
    [InlineData(@"\\srv", @"a.log", 1)]
    [InlineData(@"\\srv\share", @"\\other\x\a.log", 1)]
    [InlineData(@"\\srv\share", @"\\srv\share\a.log", 0)]
    public void Validate_share_and_file(string share, string file, int errors) =>
        Assert.Equal(errors, new SessionConfig { Name = "x", SharePath = share, FilePath = file }.Validate().Count);
}
