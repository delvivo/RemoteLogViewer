using System.IO;

namespace RemoteLogViewer;

public class SessionConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string SharePath { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? ProtectedPassword { get; set; }
    public string FilePath { get; set; } = "";
    public int TailLines { get; set; } = 1000;
    public string Encoding { get; set; } = "auto";
    public Guid? CollectionId { get; set; } // null = root
    public Guid? CredentialId { get; set; } // null = UserName/ProtectedPassword above
    public bool CurrentUndated { get; set; } // today's file has no date: placeholders resolve to "" for today

    /// Display path: date placeholders left unresolved.
    [System.Text.Json.Serialization.JsonIgnore]
    public string FullPath => Combine(FilePath);

    /// Path to actually open, with `{date:…}` placeholders resolved.
    public string ResolvePath(DateTime date, DateTime? today = null) =>
        Combine(CurrentUndated && date.Date == (today ?? DateTime.Today).Date ? DatePath.Strip(FilePath) : DatePath.Resolve(FilePath, date));

    private string Combine(string file) => file.StartsWith(@"\\") ? file : Path.Combine(SharePath.TrimEnd('\\') + "\\", file.TrimStart('\\'));

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add(L.T("Nome obbligatorio."));
        if (!SharePath.StartsWith(@"\\") || SharePath.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            errors.Add(L.T(@"Share nel formato \\server\share."));
        if (DatePath.Has(SharePath)) errors.Add(L.T("La data non è ammessa nella share."));
        if (string.IsNullOrWhiteSpace(FilePath)) errors.Add(L.T("File obbligatorio."));
        else if (FilePath.StartsWith(@"\\") && !ResolvePath(DateTime.Today).StartsWith(SharePath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            errors.Add(L.T("Il percorso UNC del file deve essere dentro la share."));
        errors.AddRange(DatePath.Validate(FilePath));
        if (TailLines is < 0 or > 100_000) errors.Add(L.T("Righe iniziali tra 0 e 100000."));
        return errors;
    }

    public SessionConfig Clone() => (SessionConfig)MemberwiseClone();

    /// Clone with the saved credential's user/password applied (if the session uses one).
    public SessionConfig WithCredential(IEnumerable<Credential> credentials)
    {
        var c = Clone();
        if (credentials.FirstOrDefault(x => x.Id == CredentialId) is { } cred)
            (c.UserName, c.ProtectedPassword) = (cred.UserName, cred.ProtectedPassword);
        return c;
    }
}
