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

    /// Display path: date placeholders left unresolved.
    [System.Text.Json.Serialization.JsonIgnore]
    public string FullPath => Combine(FilePath);

    /// Path to actually open, with `{date:…}` placeholders resolved.
    public string ResolvePath(DateTime date) => Combine(DatePath.Resolve(FilePath, date));

    private string Combine(string file) => file.StartsWith(@"\\") ? file : Path.Combine(SharePath.TrimEnd('\\') + "\\", file.TrimStart('\\'));

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Nome obbligatorio.");
        if (!SharePath.StartsWith(@"\\") || SharePath.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            errors.Add(@"Share nel formato \\server\share.");
        if (DatePath.Has(SharePath)) errors.Add("La data non è ammessa nella share.");
        if (string.IsNullOrWhiteSpace(FilePath)) errors.Add("File obbligatorio.");
        else if (FilePath.StartsWith(@"\\") && !ResolvePath(DateTime.Today).StartsWith(SharePath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            errors.Add("Il percorso UNC del file deve essere dentro la share.");
        errors.AddRange(DatePath.Validate(FilePath));
        if (TailLines is < 0 or > 100_000) errors.Add("Righe iniziali tra 0 e 100000.");
        return errors;
    }

    public SessionConfig Clone() => (SessionConfig)MemberwiseClone();
}
