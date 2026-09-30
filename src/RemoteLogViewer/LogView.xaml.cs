using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RemoteLogViewer;

public partial class LogView : UserControl
{
    public static readonly RoutedCommand FindNext = new(), FindPrev = new(), FocusSearch = new();
    private const int MaxLines = 100_000, TrimBlock = 10_000;

    public ActiveSession Session { get; }
    public event Action? StatusChanged;
    public Brush StatusBrush => StatusDot.Fill;

    private readonly List<LogLine> _all = [];
    private ObservableCollection<LogLine> _visible = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private Func<string, bool>? _filter;
    private readonly HashSet<LogLevel> _hiddenLevels = [];
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
    private bool Matches(LogLine l) => l.IsMarker || (!_hiddenLevels.Contains(l.Level) && (_filter == null || _filter(l.Text)));

    private bool SearchHit(LogLine l) => _search.Length > 0 && l.Text.Contains(_search, StringComparison.OrdinalIgnoreCase);

    private void Drain()
    {
        if (Session.Pending.IsEmpty) return;
        var added = new List<LogLine>();
        while (Session.Pending.TryDequeue(out var l))
        {
            l.IsMatch = SearchHit(l);
            _all.Add(l);
            if (Matches(l)) added.Add(l);
        }
        if (_all.Count > MaxLines)
        {
            // ponytail: trim in blocks + full rebuild; cheap because it happens once per TrimBlock lines.
            _all.RemoveRange(0, _all.Count - MaxLines + TrimBlock);
            Rebuild();
        }
        else foreach (var l in added) _visible.Add(l);
        UpdateCount();
        if (Follow) ScrollToEnd();
    }

    private void Rebuild()
    {
        _visible = new ObservableCollection<LogLine>(_all.Where(Matches));
        Lines.ItemsSource = _visible;
        UpdateCount();
        if (Follow) ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        _scroll ??= FindChild<ScrollViewer>(Lines);
        _scroll?.ScrollToEnd();
    }

    private void UpdateCount()
    {
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
        Rebuild();
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
