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

    private record SessionsFile(List<SessionConfig> Sessions);

    public string Path => path;

    /// Set by Load when the file was corrupt and has been moved aside.
    public string? Warning { get; private set; }

    public List<SessionConfig> Load()
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<SessionsFile>(File.ReadAllText(path), Json)?.Sessions ?? [];
        }
        catch (JsonException)
        {
            File.Move(path, path + ".bak", true);
            Warning = $"File sessioni corrotto, salvato come {path}.bak.";
            return [];
        }
    }

    public void Save(IEnumerable<SessionConfig> sessions)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(new SessionsFile(sessions.ToList()), Json));
        File.Move(tmp, path, true);
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
