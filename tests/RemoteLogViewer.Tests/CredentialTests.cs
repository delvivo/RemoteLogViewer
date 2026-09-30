using RemoteLogViewer;

public class CredentialTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"rlv-{Guid.NewGuid()}");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static Credential Cred(string password = "pw1") =>
        new() { Name = "Admin PROD", UserName = @"DOM\admin", ProtectedPassword = SessionStore.Protect(password) };

    [Fact]
    public void WithCredential_uses_saved_credential_and_returns_a_clone()
    {
        var c = Cred();
        var s = new SessionConfig { Name = "s", UserName = @"DOM\inline", CredentialId = c.Id };
        var resolved = s.WithCredential([c]);
        Assert.NotSame(s, resolved);
        Assert.Equal(@"DOM\admin", resolved.UserName);
        Assert.Equal("pw1", SessionStore.Unprotect(resolved.ProtectedPassword));

        c.ProtectedPassword = SessionStore.Protect("pw2"); // password change: every session picks it up
        Assert.Equal("pw2", SessionStore.Unprotect(s.WithCredential([c]).ProtectedPassword));
    }

    [Fact]
    public void WithCredential_without_credential_keeps_inline_values()
    {
        var s = new SessionConfig { UserName = @"DOM\inline", ProtectedPassword = SessionStore.Protect("x") };
        Assert.Equal(@"DOM\inline", s.WithCredential([Cred()]).UserName);
    }

    [Fact]
    public void Delete_credential_detaches_sessions()
    {
        var c = Cred();
        var s = new SessionConfig { CredentialId = c.Id };
        var tree = new SessionTree([], [s], [c]);
        Assert.Equal(1, tree.CountUsing(c.Id));
        tree.DeleteCredential(c.Id);
        Assert.Empty(tree.Credentials);
        Assert.Null(s.CredentialId);
    }

    [Fact]
    public void Store_round_trip_and_normalize_drops_dangling_reference()
    {
        var c = Cred();
        var path = Path.Combine(_dir, "sessions.json");
        var store = new SessionStore(path);
        store.Save(new SessionTree([], [new SessionConfig { Name = "a", CredentialId = c.Id }, new SessionConfig { Name = "b", CredentialId = Guid.NewGuid() }], [c]));
        Assert.DoesNotContain("pw1", File.ReadAllText(path));

        var tree = store.Load();
        var loaded = Assert.Single(tree.Credentials);
        Assert.Equal((c.Id, c.Name, c.UserName), (loaded.Id, loaded.Name, loaded.UserName));
        Assert.Equal("pw1", SessionStore.Unprotect(loaded.ProtectedPassword));
        Assert.Equal(c.Id, tree.Sessions.Single(s => s.Name == "a").CredentialId);
        Assert.Null(tree.Sessions.Single(s => s.Name == "b").CredentialId);
    }

    [Fact]
    public void Import_keeps_known_credential_and_drops_unknown()
    {
        var c = Cred();
        var tree = new SessionTree([], [], [c]);
        var file = new ExportFile
        {
            Format = ExportFile.FormatId, Version = 1,
            Sessions = [new SessionConfig { Name = "known", CredentialId = c.Id }, new SessionConfig { Name = "other", CredentialId = Guid.NewGuid() }],
        };
        tree.Import(file, null);
        Assert.Equal(c.Id, tree.Sessions.Single(s => s.Name == "known").CredentialId);
        Assert.Null(tree.Sessions.Single(s => s.Name == "other").CredentialId);
    }

    [Fact]
    public void Export_never_contains_credentials()
    {
        var c = Cred();
        var tree = new SessionTree([], [new SessionConfig { Name = "a", CredentialId = c.Id }], [c]);
        var f = tree.Export(null);
        Assert.Equal(c.Id, Assert.Single(f.Sessions).CredentialId); // re-links on import into the same app
        Assert.DoesNotContain(typeof(ExportFile).GetProperties(), p => p.Name == "Credentials");
    }
}
