using System.Collections.Concurrent;
using System.IO;

namespace RemoteLogViewer;

public enum SessionState { Connecting, Running, Waiting, Reconnecting, Error, Stopped }

/// Runtime of one session: connect share → tail → poll every 500 ms, reconnect every 5 s on I/O errors.
/// Lines go to Pending (background thread); the UI drains it in batches.
public class ActiveSession(SessionConfig config)
{
    public SessionConfig Config { get; } = config;
    public ConcurrentQueue<LogLine> Pending { get; } = new();
    public SessionState State { get; private set; } = SessionState.Stopped;
    public string StatusText { get; private set; } = "Ferma";
    public event Action? StateChanged; // raised on background thread

    private CancellationTokenSource? _cts;
    private LogLevel _last;

    public void Start()
    {
        if (_cts != null && State is not (SessionState.Error or SessionState.Stopped)) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Task.Run(() => Run(ct));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task Run(CancellationToken ct)
    {
        var share = Config.SharePath;
        var connected = false;
        var everRead = false;
        LogTailer? tailer = null;

        while (!ct.IsCancellationRequested)
        {
            var delay = 500;
            try
            {
                if (!connected)
                {
                    Set(everRead ? SessionState.Reconnecting : SessionState.Connecting, everRead ? "Riconnessione…" : "Connessione…");
                    SmbConnection.Connect(share, Config.UserName, SessionStore.Unprotect(Config.ProtectedPassword));
                    connected = true;
                }
                if (tailer == null)
                {
                    var t = new LogTailer(Config.FullPath, Config.Encoding);
                    Emit(t.ReadTail(Config.TailLines));
                    tailer = t;
                    everRead = true;
                }
                else
                {
                    var lines = tailer.Poll(out var rotated);
                    if (State != SessionState.Running) Marker("— riconnesso —");
                    if (rotated) Marker("— file troncato o ruotato: lettura dall'inizio —");
                    Emit(lines);
                }
                Set(SessionState.Running, "Attiva");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException && connected)
            {
                // File not there (yet, or mid-rotation): wait for it.
                if (tailer != null) { Marker("— file scomparso, in attesa —"); tailer = null; }
                Set(SessionState.Waiting, "In attesa del file…");
                delay = 2000;
            }
            catch (Exception e) when (!everRead && e is SmbException or UnauthorizedAccessException or IOException)
            {
                // First connect failed: wrong credentials/path. No automatic retry.
                Set(SessionState.Error, e.Message);
                break;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (State == SessionState.Running) Marker($"— disconnesso: {e.Message} —");
                if (connected) { SmbConnection.Release(share); connected = false; }
                Set(SessionState.Reconnecting, $"Disconnesso, nuovo tentativo tra 5 s ({e.Message})");
                delay = 5000;
            }
            catch (Exception e)
            {
                Set(SessionState.Error, e.Message);
                break;
            }

            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
        }

        if (connected) SmbConnection.Release(share);
        if (State != SessionState.Error) Set(SessionState.Stopped, "Ferma");
    }

    private void Emit(List<string> lines)
    {
        foreach (var l in lines)
        {
            _last = LogLevels.Detect(l, _last);
            Pending.Enqueue(new LogLine(l, _last));
        }
    }

    private void Marker(string text) => Pending.Enqueue(new LogLine(text, LogLevel.None, true));

    private void Set(SessionState state, string text)
    {
        if (State == state && StatusText == text) return;
        State = state;
        StatusText = text;
        StateChanged?.Invoke();
    }
}
