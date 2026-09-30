using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RemoteLogViewer;

public partial class LogView : UserControl
{
    public static readonly RoutedCommand FindNext = new(), FindPrev = new(), FocusSearch = new();
    private const int MaxLines = 100_000, TrimBlock = 10_000;

    public ActiveSession Session { get; }
    public event Action? StatusChanged;
    public event Action<int>? ErrorsArrived; // new ERROR entries (not from the initial read)
    public Brush StatusBrush => StatusDot.Fill;
    public int UnreadErrors { get; private set; } // ERROR entries arrived while this view was not visible

    private readonly List<LogLine> _all = [];
    private ObservableCollection<LogLine> _visible = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private Func<string, bool>? _filter;
    private readonly HashSet<LogLevel> _hiddenLevels = [];
    private readonly int[] _counts = new int[Enum.GetValues<LogLevel>().Length]; // entries per level in _all
    private ContextFilter _ctx = new(0, _ => true, null);
    private string _search = "";
    private ScrollViewer? _scroll;

    public LogView(ActiveSession session)
    {
        InitializeComponent();
        Session = session;
        Lines.ItemsSource = _visible;
        CommandBindings.Add(new CommandBinding(FindNext, (_, _) => Find(1)));
        CommandBindings.Add(new CommandBinding(FindPrev, (_, _) => Find(-1)));
        CommandBindings.Add(new CommandBinding(FocusSearch, (_, _) => { SearchBox.Focus(); SearchBox.SelectAll(); }));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveVisible(), (_, e) => e.CanExecute = _visible.Count > 0));
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is not true || UnreadErrors == 0) return;
            UnreadErrors = 0;
            StatusChanged?.Invoke();
        };
        Session.StateChanged += () => Dispatcher.BeginInvoke(UpdateStatus);
        _timer.Tick += (_, _) => Drain();
        _timer.Start();
        UpdateStatus();
        Session.Start();
    }

    public void Close()
    {
        _timer.Stop();
        Session.Stop();
    }

    private bool Follow => FollowButton.IsChecked == true;

    // Lines with no detected level (None) are never hidden by the level filter.
    private bool LevelOk(LogLine l) => !_hiddenLevels.Contains(l.Level);

    private int ContextLines => int.TryParse(ContextBox.Text, out var n) ? Math.Clamp(n, 0, 50) : 0;

    private bool SearchHit(LogLine l) => _search.Length > 0 && l.Text.Contains(_search, StringComparison.OrdinalIgnoreCase);

    private void Drain()
    {
        if (Session.Pending.IsEmpty && Session.Older.IsEmpty) return;
        var older = false;
        while (Session.Older.TryDequeue(out var block))
        {
            foreach (var l in block) { l.IsMatch = SearchHit(l); Count(l, 1); }
            _all.InsertRange(0, block);
            older = true;
        }
        var added = new List<LogLine>();
        var errors = 0;
        while (Session.Pending.TryDequeue(out var l))
        {
            l.IsMatch = SearchHit(l);
            _all.Add(l);
            Count(l, 1);
            if (l is { Level: LogLevel.Error, IsEntry: true, IsHistory: false }) errors++;
            added.AddRange(_ctx.Add(_all, _all.Count - 1));
        }
        if (_all.Count > MaxLines)
        {
            // ponytail: trim in blocks + full rebuild; cheap because it happens once per TrimBlock lines.
            _all.RemoveRange(0, _all.Count - MaxLines + TrimBlock);
            Array.Clear(_counts);
            foreach (var l in _all) Count(l, 1);
            Rebuild();
        }
        else if (older) Rebuild(keepTopLine: true); // indices shifted: replay the whole buffer
        else foreach (var l in added) _visible.Add(l);
        UpdateCount();
        if (Follow) ScrollToEnd();
        if (errors == 0) return;
        if (!IsVisible) { UnreadErrors += errors; StatusChanged?.Invoke(); }
        ErrorsArrived?.Invoke(errors);
    }

    private void Count(LogLine l, int delta)
    {
        if (l.IsEntry) _counts[(int)l.Level] += delta;
    }

    private void Rebuild(bool keepTopLine = false)
    {
        _scroll ??= FindChild<ScrollViewer>(Lines);
        // Item-based scrolling (virtualized ListBox): the offset is the index of the top line.
        var top = keepTopLine && _scroll != null && (int)_scroll.VerticalOffset < _visible.Count ? _visible[(int)_scroll.VerticalOffset] : null;
        _ctx = new ContextFilter(_filter == null ? 0 : ContextLines, LevelOk, _filter);
        var visible = new List<LogLine>();
        for (var i = 0; i < _all.Count; i++) visible.AddRange(_ctx.Add(_all, i));
        _visible = new ObservableCollection<LogLine>(visible);
        Lines.ItemsSource = _visible;
        UpdateCount();
        if (Follow) ScrollToEnd();
        else if (top != null && _visible.IndexOf(top) is var index and >= 0)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => _scroll?.ScrollToVerticalOffset(index));
    }

    private void ScrollToEnd()
    {
        _scroll ??= FindChild<ScrollViewer>(Lines);
        _scroll?.ScrollToEnd();
    }

    private void UpdateCount()
    {
        ErrorCheck.Content = $"ERROR {_counts[(int)LogLevel.Error]}";
        WarnCheck.Content = $"WARN {_counts[(int)LogLevel.Warn]}";
        InfoCheck.Content = $"INFO {_counts[(int)LogLevel.Info]}";
        DebugCheck.Content = $"DEBUG {_counts[(int)LogLevel.Debug]}";
        var matches = _search.Length == 0 ? "" : $" · {_all.Count(l => l.IsMatch)} trovate";
        CountLabel.Text = _visible.Count == _all.Count ? $"{_all.Count} righe{matches}" : $"{_visible.Count}/{_all.Count} righe{matches}";
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = Session.StatusText;
        StatusLabel.ToolTip = Session.StatusText;
        StatusDot.Fill = Session.State switch
        {
            SessionState.Running => Brushes.LimeGreen,
            SessionState.Error => Brushes.Red,
            SessionState.Stopped => Brushes.Gray,
            _ => Brushes.Orange,
        };
        StartStopButton.Content = Session.State is SessionState.Stopped or SessionState.Error ? "Avvia" : "Stop";
        OlderButton.IsEnabled = Session.CanLoadOlder && Session.State == SessionState.Running;
        StatusChanged?.Invoke();
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (Session.State is SessionState.Stopped or SessionState.Error) Session.Start();
        else Session.Stop();
    }

    private void Follow_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ScrollToEnd();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _all.Clear();
        Array.Clear(_counts);
        Rebuild();
    }

    private void Older_Click(object sender, RoutedEventArgs e)
    {
        var wanted = Session.Config.TailLines > 0 ? Session.Config.TailLines : 1000;
        var n = Math.Min(wanted, MaxLines - _all.Count);
        if (n <= 0)
        {
            Info($"Buffer pieno ({MaxLines:N0} righe): usa Pulisci per fare spazio.");
            return;
        }
        if (n < wanted) Info($"Verranno caricate solo {n:N0} righe: il buffer è limitato a {MaxLines:N0}.");
        Session.RequestOlder(n);
    }

    private void Info(string text, MessageBoxImage image = MessageBoxImage.Information) =>
        MessageBox.Show(Window.GetWindow(this), text, "Remote Log Viewer", MessageBoxButton.OK, image);

    private void SaveVisible()
    {
        var name = string.Concat(Session.Config.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dlg = new SaveFileDialog
        {
            Filter = "Log (*.log)|*.log|Tutti i file (*.*)|*.*",
            FileName = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.log",
            Title = "Salva righe visibili",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllLines(dlg.FileName, _visible.Select(l => l.Text), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Info($"Salvataggio non riuscito: {ex.Message}", MessageBoxImage.Error);
        }
    }

    private void Lines_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0 || e.VerticalChange == 0) return; // content changed, not the user
        if (e.VerticalChange < 0) FollowButton.IsChecked = false;
        else if (e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight) FollowButton.IsChecked = true;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        var text = FilterBox.Text;
        FilterBox.ClearValue(BorderBrushProperty);
        _filter = null;
        if (text.Length > 0 && RegexCheck.IsChecked == true)
        {
            try
            {
                var re = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
                _filter = s => { try { return re.IsMatch(s); } catch (RegexMatchTimeoutException) { return false; } };
            }
            catch (ArgumentException) { FilterBox.BorderBrush = Brushes.Red; }
        }
        else if (text.Length > 0) _filter = s => s.Contains(text, StringComparison.OrdinalIgnoreCase);
        Rebuild();
    }

    private void Context_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded && _filter != null) Rebuild();
    }

    private void Level_Changed(object sender, RoutedEventArgs e)
    {
        var cb = (CheckBox)sender;
        var level = Enum.Parse<LogLevel>((string)cb.Tag);
        if (cb.IsChecked == true) _hiddenLevels.Remove(level); else _hiddenLevels.Add(level);
        if (IsLoaded) Rebuild(); // Checked fires during InitializeComponent
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        foreach (var l in _all) l.IsMatch = SearchHit(l);
        UpdateCount();
    }

    private void Find(int dir)
    {
        if (_search.Length == 0 || _visible.Count == 0) return;
        var n = _visible.Count;
        var start = Lines.SelectedIndex >= 0 ? Lines.SelectedIndex : (dir > 0 ? -1 : n);
        for (var k = 1; k <= n; k++)
        {
            var i = ((start + dir * k) % n + n) % n;
            if (!_visible[i].IsMatch) continue;
            FollowButton.IsChecked = false;
            Lines.SelectedIndex = i;
            Lines.ScrollIntoView(_visible[i]);
            return;
        }
    }

    private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = Lines.SelectedItems.Cast<LogLine>().ToHashSet();
        if (selected.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, _visible.Where(selected.Contains).Select(l => l.Text)));
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var c = VisualTreeHelper.GetChild(parent, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is { } r) return r;
        }
        return null;
    }
}
