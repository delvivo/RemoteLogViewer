using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace RemoteLogViewer;

/// Equivalent of New-SmbMapping without a drive letter: WNetAddConnection2 on the UNC share.
/// Ref-counted per share; only connections opened by this app are cancelled.
public static class SmbConnection
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class NetResource
    {
        public int Scope, Type = 1 /* RESOURCETYPE_DISK */, DisplayType, Usage;
        public string? LocalName, RemoteName, Comment, Provider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(NetResource res, string? password, string? user, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);

    private static readonly Dictionary<string, int> RefCounts = new(StringComparer.OrdinalIgnoreCase);

    public static void Connect(string share, string? user, string? password)
    {
        share = share.TrimEnd('\\');
        lock (RefCounts)
        {
            if (RefCounts.TryGetValue(share, out var n)) { RefCounts[share] = n + 1; return; }
            var rc = WNetAddConnection2(new NetResource { RemoteName = share },
                string.IsNullOrEmpty(user) ? null : password, string.IsNullOrEmpty(user) ? null : user, 0);
            if (rc == 85 /* ERROR_ALREADY_ASSIGNED */) return; // existing user mapping: use it, never cancel it
            if (rc != 0) throw new SmbException(rc);
            RefCounts[share] = 1;
        }
    }

    public static void Release(string share)
    {
        share = share.TrimEnd('\\');
        lock (RefCounts)
        {
            if (!RefCounts.TryGetValue(share, out var n)) return;
            if (n > 1) { RefCounts[share] = n - 1; return; }
            RefCounts.Remove(share);
            WNetCancelConnection2(share, 0, false);
        }
    }

    public static void ReleaseAll()
    {
        lock (RefCounts)
        {
            foreach (var s in RefCounts.Keys) WNetCancelConnection2(s, 0, false);
            RefCounts.Clear();
        }
    }

    public static string Message(int code) => code switch
    {
        5 => L.T("Accesso negato."),
        53 or 67 or 1203 => L.T("Server o share non raggiungibile."),
        86 or 1326 => L.T("Credenziali non valide."),
        1219 => L.T("Esiste già una connessione a questo server con credenziali diverse. Chiudila (net use \\\\server\\share /delete) o usa le stesse credenziali."),
        1311 => L.T("Nessun server di accesso disponibile per il dominio."),
        _ => new Win32Exception(code).Message,
    };
}

public class SmbException(int code) : IOException(SmbConnection.Message(code))
{
    public int Code { get; } = code;
}
