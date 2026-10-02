using System.Collections.Concurrent;
using System.IO;

namespace RemoteLogViewer;

public enum SessionState { Connecting, Running, Waiting, Reconnecting, Error, Stopped }

/// Runtime of one session: connect share → tail → poll every 500 ms, reconnect every 5 s on I/O errors.
/// Lines go to Pending (background thread); the UI drains it in batches.
/// `date` resolves `{date:…}` placeholders; a session started on today follows the new day's file after midnight.
public class ActiveSession(SessionConfig config, DateTime? date = null, Func<DateTime>? today = null)
{
    private readonly Func<DateTime> _today = today ?? (() => DateTime.Today);

    public SessionConfig Config { get; } = config;
    public DateTime? Date { get; private set; } = date?.Date;
    public bool FollowToday { get; } = date != null && date.Value.Date == (today ?? (() => DateTime.Today))().Date;
    public string DisplayName => Date is { } d ? $"{Config.Name} · {d:yyyy-MM-dd}" : Config.Name;
    public string Path => Date is { } d ? Config.ResolvePath(d, _today()) : Config.FullPath;
    public ConcurrentQueue<LogLine> Pending { get; } = new();
    public ConcurrentQueue<List<LogLine>> Older { get; } = new(); // blocks of earlier lines, to put at the top
    public bool CanLoadOlder => _canLoadOlder;
    public SessionState State { get; private set; } = SessionState.Stopped;
    public string StatusText => _statusArgs.Length == 0 ? L.T(_statusKey) : L.F(_statusKey, _statusArgs); // localized when read, so a language switch shows at once
    private string _statusKey = "Ferma";
    private object[] _statusArgs = [];
    public event Action? StateChanged; // raised on background thread

    private CancellationTokenSource? _cts;
    private LogLevel _last;
    private volatile bool _canLoadOlder;
    private int _olderRequest;

    /// Ask the loop (which owns the tailer) to read n lines before the oldest one read so far.
    public void RequestOlder(int n) => Interlocked.Exchange(ref _olderRequest, n);

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
        var fromStart = false;
        LogTailer? tailer = null;

        while (!ct.IsCancellationRequested)
        {
            var delay = 500;
            try
            {
                if (!connected)
                {
                    Set(everRead ? SessionState.Reconnecting : SessionState.Connecting, everRead ? "Riconnessione…" : "Connessione…");
                    if (!Config.IsLocal) SmbConnection.Connect(share, Config.UserName, SessionStore.Unprotect(Config.ProtectedPassword));
                    connected = true;
                }
                if (FollowToday && _today().Date != Date)
                {
                    // New day: switch to its file and read it from the start (capped like the view buffer).
                    // An undated current file keeps its path: the tailer sees the server's rotation by itself.
                    Date = _today().Date;
                    Marker(L.F("— nuovo giorno: {0:yyyy-MM-dd} —", Date));
                    if (!Config.CurrentUndated) { tailer = null; fromStart = true; }
                    StateChanged?.Invoke(); // tab title shows the date
                }
                if (tailer == null)
                {
                    var t = new LogTailer(Path, Config.Encoding);
                    Emit(t.ReadTail(fromStart ? 100_000 : Config.TailLines), history: !fromStart); // a new day's lines are new
                    tailer = t;
                    everRead = true;
                    fromStart = false;
                }
                else
                {
                    if (Interlocked.Exchange(ref _olderRequest, 0) is var n and > 0)
                    {
                        var last = LogLevel.None;
                        Older.Enqueue(tailer.ReadBefore(n).Select(l => LogLevels.Make(l, ref last, history: true)).ToList());
                    }
                    var lines = tailer.Poll(out var rotated);
                    if (State != SessionState.Running) Marker(L.T("— riconnesso —"));
                    if (rotated) Marker(L.T("— file troncato o ruotato: lettura dall'inizio —"));
                    Emit(lines, history: false);
                }
                Set(SessionState.Running, "Attiva");
                SetCanLoadOlder(tailer.HasOlder);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException && connected)
            {
                // File not there (yet, or mid-rotation): wait for it.
                if (tailer != null) { Marker(L.T("— file scomparso, in attesa —")); tailer = null; }
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
                if (State == SessionState.Running) Marker(L.F("— disconnesso: {0} —", e.Message));
                if (connected) { if (!Config.IsLocal) SmbConnection.Release(share); connected = false; }
                Set(SessionState.Reconnecting, "Disconnesso, nuovo tentativo tra 5 s ({0})", e.Message);
                delay = 5000;
            }
            catch (Exception e)
            {
                Set(SessionState.Error, e.Message);
                break;
            }

            if (State != SessionState.Running) SetCanLoadOlder(false);
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
        }

        if (connected && !Config.IsLocal) SmbConnection.Release(share);
        _canLoadOlder = false;
        if (State != SessionState.Error) Set(SessionState.Stopped, "Ferma");
    }

    private void SetCanLoadOlder(bool value)
    {
        if (_canLoadOlder == value) return;
        _canLoadOlder = value;
        StateChanged?.Invoke();
    }

    private void Emit(List<string> lines, bool history)
    {
        foreach (var l in lines) Pending.Enqueue(LogLevels.Make(l, ref _last, history));
    }

    private void Marker(string text) => Pending.Enqueue(new LogLine(text, LogLevel.None, true));

    /// `key` is the Italian text (translated on read); `args` fill its placeholders.
    private void Set(SessionState state, string key, params object[] args)
    {
        if (State == state && _statusKey == key && _statusArgs.SequenceEqual(args)) return;
        State = state;
        (_statusKey, _statusArgs) = (key, args);
        StateChanged?.Invoke();
    }
}
