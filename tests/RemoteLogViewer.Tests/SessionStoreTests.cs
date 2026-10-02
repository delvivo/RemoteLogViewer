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
            Name = "PROD", SharePath = @"\\serverName\E$", UserName = @"DOM\me",
            ProtectedPassword = SessionStore.Protect("S3gret0!pw"), FilePath = @"logs\app.log", TailLines = 500, Encoding = "utf-16",
        };
        store.Save(new SessionTree([], [s]));

        Assert.DoesNotContain("S3gret0!pw", File.ReadAllText(FilePath));
        Assert.DoesNotContain("fullPath", File.ReadAllText(FilePath));

        var loaded = Assert.Single(new SessionStore(FilePath).Load().Sessions);
        Assert.Equal(s.Id, loaded.Id);
        Assert.Equal(@"\\serverName\E$", loaded.SharePath);
        Assert.Equal(@"\\serverName\E$\logs\app.log", loaded.FullPath);
        Assert.Equal(500, loaded.TailLines);
        Assert.Equal("S3gret0!pw", SessionStore.Unprotect(loaded.ProtectedPassword));
    }

    [Fact]
    public void Missing_file_loads_empty() => Assert.Empty(new SessionStore(FilePath).Load().Sessions);

    [Fact]
    public void V1_file_without_collections_loads_sessions_at_root()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "sessions": [ { "id": "3f1c2d4e-0000-0000-0000-000000000001", "name": "old", "sharePath": "\\\\srv\\E$", "filePath": "a.log" } ] }""");
        var tree = new SessionStore(FilePath).Load();
        Assert.Empty(tree.Collections);
        var s = Assert.Single(tree.Sessions);
        Assert.Equal("old", s.Name);
        Assert.Null(s.CollectionId);
    }

    [Fact]
    public void Round_trip_keeps_collections()
    {
        var prod = new SessionCollection { Name = "PROD" };
        var api = new SessionCollection { Name = "API", ParentId = prod.Id };
        var s = new SessionConfig { Name = "x", CollectionId = api.Id };
        new SessionStore(FilePath).Save(new SessionTree([prod, api], [s]));

        var tree = new SessionStore(FilePath).Load();
        Assert.Equal("PROD › API", tree.PathOf(api.Id));
        Assert.Equal(api.Id, Assert.Single(tree.Sessions).CollectionId);
    }

    [Fact]
    public void Round_trip_keeps_workspace()
    {
        var a = new SessionConfig { Name = "a" };
        var date = new DateTime(2026, 6, 11);
        var tree = new SessionTree([], [a])
        {
            Workspace = new Workspace
            {
                Tabs = [new OpenTab { SessionId = a.Id }, new OpenTab { SessionId = a.Id, Date = date, FollowToday = true }],
                Selected = 1,
                SideBySide = true,
            },
        };
        new SessionStore(FilePath).Save(tree);

        var ws = new SessionStore(FilePath).Load().Workspace;
        Assert.Equal(2, ws.Tabs.Count);
        Assert.Equal(a.Id, ws.Tabs[0].SessionId);
        Assert.Null(ws.Tabs[0].Date);
        Assert.Equal(date, ws.Tabs[1].Date);
        Assert.True(ws.Tabs[1].FollowToday);
        Assert.Equal(1, ws.Selected);
        Assert.True(ws.SideBySide);
    }

    [Fact]
    public void Round_trip_keeps_local_file_tabs()
    {
        var a = new SessionConfig { Name = "a" };
        var tree = new SessionTree([], [a])
        {
            Workspace = new Workspace { Tabs = [new OpenTab { LocalPath = @"C:\x\app.log" }, new OpenTab { SessionId = a.Id }] },
        };
        new SessionStore(FilePath).Save(tree);

        var ws = new SessionStore(FilePath).Load().Workspace;
        Assert.Equal(@"C:\x\app.log", ws.Tabs[0].LocalPath);
        Assert.Equal(Guid.Empty, ws.Tabs[0].SessionId);
        Assert.Null(ws.Tabs[1].LocalPath);
    }

    [Fact]
    public void Workspace_tab_without_local_path_loads_with_null()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "sessions": [], "workspace": { "tabs": [ { "sessionId": "11111111-1111-1111-1111-111111111111" } ] } }""");
        Assert.Null(new SessionStore(FilePath).Load().Workspace.Tabs[0].LocalPath);
    }

    [Fact]
    public void File_without_workspace_loads_empty_workspace()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "sessions": [] }""");
        var ws = new SessionStore(FilePath).Load().Workspace;
        Assert.Empty(ws.Tabs);
        Assert.Equal(-1, ws.Selected);
        Assert.False(ws.SideBySide);
    }

    [Fact]
    public void Export_never_contains_workspace()
    {
        var path = Path.Combine(_dir, "x.rlv.json");
        var s = new SessionConfig { Name = "s" };
        var tree = new SessionTree([], [s]) { Workspace = new Workspace { Tabs = [new OpenTab { SessionId = s.Id }] } };
        SessionStore.WriteExport(path, tree.Export(null));
        Assert.DoesNotContain("workspace", File.ReadAllText(path));
    }

    [Fact]
    public void Export_round_trip()
    {
        var path = Path.Combine(_dir, "x.rlv.json");
        var prod = new SessionCollection { Name = "PROD" };
        var tree = new SessionTree([prod], [new SessionConfig { Name = "s", CollectionId = prod.Id, ProtectedPassword = SessionStore.Protect("pw") }]);
        SessionStore.WriteExport(path, tree.Export(prod.Id));

        Assert.DoesNotContain("protectedPassword\": \"", File.ReadAllText(path));
        var f = SessionStore.ReadExport(path);
        Assert.Equal("PROD", Assert.Single(f.Collections).Name);
        Assert.Equal("s", Assert.Single(f.Sessions).Name);
    }

    [Theory]
    [InlineData("""{ "format": "other", "version": 1 }""", "File non riconosciuto.")]
    [InlineData("""{}""", "File non riconosciuto.")]
    [InlineData("""null""", "File non riconosciuto.")]
    [InlineData("""{ "format": "remote-log-viewer-collection", "version": 2 }""", "Versione del file non supportata.")]
    [InlineData("""{ not json""", "File non valido.")]
    [InlineData("""testo qualsiasi""", "File non valido.")]
    public void ReadExport_rejects_bad_files(string content, string message)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, content);
        Assert.Equal(message, Assert.Throws<InvalidDataException>(() => SessionStore.ReadExport(path)).Message);
    }

    [Fact]
    public void ReadExport_ignores_unknown_fields_and_null_lists()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "future.json");
        File.WriteAllText(path, """{ "format": "remote-log-viewer-collection", "version": 1, "collections": null, "sessions": [ { "name": "s", "futureField": 42 } ], "extra": true }""");
        var f = SessionStore.ReadExport(path);
        Assert.Empty(f.Collections);
        Assert.Equal("s", Assert.Single(f.Sessions).Name);
    }

    [Fact]
    public void Corrupt_file_is_moved_to_bak()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var store = new SessionStore(FilePath);
        Assert.Empty(store.Load().Sessions);
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
