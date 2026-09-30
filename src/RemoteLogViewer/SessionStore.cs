using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RemoteLogViewer;

public class SessionStore(string path)
{
    public static readonly string DefaultPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteLogViewer", "sessions.json");

    private static readonly byte[] Entropy = "RemoteLogViewer"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // v1 had only Sessions; Collections missing → all sessions at root.
    private record SessionsFile(List<SessionCollection>? Collections, List<SessionConfig>? Sessions, List<Credential>? Credentials);

    public string Path => path;

    /// Set by Load when the file was corrupt and has been moved aside.
    public string? Warning { get; private set; }

    public SessionTree Load()
    {
        if (!File.Exists(path)) return new SessionTree();
        try
        {
            var f = JsonSerializer.Deserialize<SessionsFile>(File.ReadAllText(path), Json);
            var tree = new SessionTree(f?.Collections ?? [], f?.Sessions ?? [], f?.Credentials ?? []);
            tree.Normalize();
            return tree;
        }
        catch (JsonException)
        {
            File.Move(path, path + ".bak", true);
            Warning = $"File sessioni corrotto, salvato come {path}.bak.";
            return new SessionTree();
        }
    }

    public void Save(SessionTree tree) => WriteAtomic(path, new SessionsFile(tree.Collections, tree.Sessions, tree.Credentials));

    public static void WriteExport(string file, ExportFile export) => WriteAtomic(file, export);

    public static ExportFile ReadExport(string file)
    {
        ExportFile? f;
        try { f = JsonSerializer.Deserialize<ExportFile>(File.ReadAllText(file), Json); }
        catch (JsonException) { throw new InvalidDataException("File non valido."); }
        if (f?.Format != ExportFile.FormatId) throw new InvalidDataException("File non riconosciuto.");
        if (f.Version > 1) throw new InvalidDataException("Versione del file non supportata.");
        f.Collections ??= [];
        f.Sessions ??= [];
        return f;
    }

    private static void WriteAtomic<T>(string file, T content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(content, Json));
        File.Move(tmp, file, true);
    }

    public static string Protect(string password) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));

    /// null if missing or not decryptable (other user / machine): the user must re-enter it.
    public static string? Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrEmpty(protectedPassword)) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedPassword), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
