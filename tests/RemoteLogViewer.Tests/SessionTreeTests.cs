using RemoteLogViewer;

public class SessionTreeTests
{
    // PROD > API > Nodi ; TEST ; sessions: root "b-root", "A-root"; PROD "p1"; API "a1","a2"; Nodi "n1"
    private readonly SessionTree _t = new();
    private readonly SessionCollection _prod, _api, _nodi, _test;

    public SessionTreeTests()
    {
        _prod = Add("PROD", null);
        _api = Add("API", _prod.Id);
        _nodi = Add("Nodi", _api.Id);
        _test = Add("TEST", null);
        Session("b-root", null);
        Session("A-root", null);
        Session("p1", _prod.Id, "secret");
        Session("a1", _api.Id);
        Session("a2", _api.Id);
        Session("n1", _nodi.Id);
    }

    private SessionCollection Add(string name, Guid? parent)
    {
        var c = new SessionCollection { Name = name, ParentId = parent };
        _t.Collections.Add(c);
        return c;
    }

    private SessionConfig Session(string name, Guid? collection, string? password = null)
    {
        var s = new SessionConfig
        {
            Name = name, CollectionId = collection, SharePath = @"\\srv\E$", UserName = @"DOM\me", FilePath = $@"logs\{name}.log",
            TailLines = 250, Encoding = "utf-8", ProtectedPassword = password == null ? null : SessionStore.Protect(password),
        };
        _t.Sessions.Add(s);
        return s;
    }

    [Fact]
    public void Children_are_collections_then_sessions_sorted_by_name()
    {
        Assert.Equal(["PROD", "TEST"], _t.ChildCollections(null).Select(c => c.Name));
        Assert.Equal(["A-root", "b-root"], _t.ChildSessions(null).Select(s => s.Name));
        Assert.Equal(["a1", "a2"], _t.ChildSessions(_api.Id).Select(s => s.Name));
    }

    [Fact]
    public void Cannot_move_collection_into_itself_or_descendant()
    {
        Assert.False(_t.CanMove(_prod.Id, _prod.Id));
        Assert.False(_t.CanMove(_prod.Id, _api.Id));
        Assert.False(_t.CanMove(_prod.Id, _nodi.Id));
        Assert.True(_t.CanMove(_prod.Id, _test.Id));
        Assert.True(_t.CanMove(_nodi.Id, null));
        Assert.Throws<InvalidOperationException>(() => _t.Move(_prod, _nodi.Id));
    }

    [Fact]
    public void Move_collection_and_session()
    {
        _t.Move(_api, _test.Id);
        Assert.Equal(_test.Id, _api.ParentId);
        Assert.Equal(3, _t.CountSessions(_test.Id)); // a1, a2, n1 came along

        var s = _t.Sessions.First(x => x.Name == "b-root");
        _t.Move(s, _nodi.Id);
        Assert.Equal(_nodi.Id, s.CollectionId);
    }

    [Fact]
    public void Move_into_parent_with_same_named_collection_gets_suffix()
    {
        var other = Add("API", _test.Id);
        _t.Move(_api, _test.Id);
        Assert.Equal("API (2)", _api.Name);
        Assert.Equal("API", other.Name);
    }

    [Fact]
    public void Count_and_delete_are_recursive()
    {
        Assert.Equal(4, _t.CountSessions(_prod.Id));
        Assert.Equal(2, _t.CountCollections(_prod.Id));
        _t.Delete(_prod.Id);
        Assert.Equal(["TEST"], _t.Collections.Select(c => c.Name));
        Assert.Equal(["b-root", "A-root"], _t.Sessions.Select(s => s.Name));
    }

    [Fact]
    public void UniqueName_adds_suffix_case_insensitive()
    {
        Assert.Equal("NEW", _t.UniqueName(null, "NEW"));
        Assert.Equal("prod (2)", _t.UniqueName(null, "prod"));
        Add("PROD (2)", null);
        Assert.Equal("PROD (3)", _t.UniqueName(null, "PROD"));
        Assert.True(_t.NameTaken(null, "test"));
        Assert.False(_t.NameTaken(null, "test", _test.Id));
    }

    [Fact]
    public void PathOf_joins_ancestors() => Assert.Equal("PROD › API › Nodi", _t.PathOf(_nodi.Id));

    [Fact]
    public void Normalize_moves_orphans_and_cycles_to_root()
    {
        var orphan = Add("Orfana", Guid.NewGuid());
        var s = Session("orfana", Guid.NewGuid());
        var x = Add("X", null);
        var y = Add("Y", x.Id);
        x.ParentId = y.Id; // X ↔ Y
        _t.Normalize();
        Assert.Null(orphan.ParentId);
        Assert.Null(s.CollectionId);
        Assert.True(x.ParentId == null || y.ParentId == null);
        Assert.Equal(1, _t.CountCollections(x.Id) + _t.CountCollections(y.Id)); // walking terminates, one contains the other
    }

    // ---- export / import ----

    [Fact]
    public void Export_collection_includes_descendants_without_passwords()
    {
        var f = _t.Export(_prod.Id);
        Assert.Equal(ExportFile.FormatId, f.Format);
        Assert.Equal(1, f.Version);
        Assert.Equal(["PROD", "API", "Nodi"], f.Collections.Select(c => c.Name));
        Assert.Null(f.Collections.Single(c => c.Name == "PROD").ParentId);
        Assert.Equal(["p1", "a1", "a2", "n1"], f.Sessions.Select(s => s.Name));
        Assert.All(f.Sessions, s => Assert.Null(s.ProtectedPassword));
        Assert.NotNull(_t.Sessions.Single(s => s.Name == "p1").ProtectedPassword); // original untouched
    }

    [Fact]
    public void Export_root_includes_everything()
    {
        var f = _t.Export(null);
        Assert.Equal(4, f.Collections.Count);
        Assert.Equal(6, f.Sessions.Count);
    }

    [Fact]
    public void Import_twice_creates_independent_copies_with_suffix()
    {
        var f = _t.Export(_prod.Id);
        var before = _t.Collections.Select(c => c.Id).Concat(_t.Sessions.Select(s => s.Id)).ToHashSet();

        var first = Assert.IsType<SessionCollection>(_t.Import(f, null));
        var second = Assert.IsType<SessionCollection>(_t.Import(f, null));

        Assert.Equal("PROD (2)", first.Name);
        Assert.Equal("PROD (3)", second.Name);
        var ids = _t.Collections.Select(c => c.Id).Concat(_t.Sessions.Select(s => s.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(4, _t.CountSessions(first.Id));
        Assert.Equal(4, _t.CountSessions(second.Id));
        Assert.Equal("PROD (2) › API › Nodi", _t.PathOf(_t.Collections.Single(c => c.Name == "Nodi" && !before.Contains(c.Id) && _t.PathOf(c.Id).StartsWith("PROD (2)")).Id));

        // editing an imported session does not touch the original (SC-003: parameters identical)
        var imported = _t.Sessions.First(s => s.Name == "a1" && !before.Contains(s.Id));
        var original = _t.Sessions.First(s => s.Name == "a1" && before.Contains(s.Id));
        Assert.Equal((original.SharePath, original.UserName, original.FilePath, original.TailLines, original.Encoding),
                     (imported.SharePath, imported.UserName, imported.FilePath, imported.TailLines, imported.Encoding));
        imported.FilePath = "changed";
        Assert.NotEqual("changed", original.FilePath);
    }

    [Fact]
    public void Import_root_export_attaches_roots_under_target()
    {
        var f = _t.Export(null);
        var target = Add("Import", null);
        _t.Import(f, target.Id);
        Assert.Equal(["PROD", "TEST"], _t.ChildCollections(target.Id).Select(c => c.Name));
        Assert.Equal(["A-root", "b-root"], _t.ChildSessions(target.Id).Select(s => s.Name));
        Assert.Equal(6, _t.CountSessions(target.Id));
    }

    [Fact]
    public void Import_session_at_root_with_same_name_gets_suffix()
    {
        var f = new ExportFile { Format = ExportFile.FormatId, Version = 1, Sessions = [new SessionConfig { Name = "A-root" }] };
        var s = Assert.IsType<SessionConfig>(_t.Import(f, null));
        Assert.Equal("A-root (2)", s.Name);
    }

    public static TheoryData<string> InvalidFiles => ["duplicate", "orphan", "cycle", "session-orphan", "empty-name"];

    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public void Import_invalid_file_throws_and_leaves_tree_unchanged(string kind)
    {
        var a = new SessionCollection { Name = "A" };
        var b = new SessionCollection { Name = "B", ParentId = a.Id };
        var f = new ExportFile { Format = ExportFile.FormatId, Version = 1, Collections = [a, b] };
        switch (kind)
        {
            case "duplicate": f.Collections.Add(new SessionCollection { Id = a.Id, Name = "dup" }); break;
            case "orphan": b.ParentId = Guid.NewGuid(); break;
            case "cycle": a.ParentId = b.Id; break;
            case "session-orphan": f.Sessions.Add(new SessionConfig { Name = "s", CollectionId = Guid.NewGuid() }); break;
            case "empty-name": b.Name = " "; break;
        }
        var (cols, sessions) = (_t.Collections.Count, _t.Sessions.Count);
        Assert.Equal("File non valido.", Assert.Throws<InvalidDataException>(() => _t.Import(f, null)).Message);
        Assert.Equal((cols, sessions), (_t.Collections.Count, _t.Sessions.Count));
    }
}
