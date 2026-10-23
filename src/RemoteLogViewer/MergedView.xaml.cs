using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace RemoteLogViewer;

/// Session colour for the merged view's session column (solid for the bar and swatch, soft for the cell background).
public class SessionBrushConverter(bool soft) : IValueConverter
{
    public static readonly SessionBrushConverter Solid = new(false), Soft = new(true);

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x19, 0x76, 0xD2), Color.FromRgb(0x38, 0x8E, 0x3C), Color.FromRgb(0xF5, 0x7C, 0x00), Color.FromRgb(0x7B, 0x1F, 0xA2),
        Color.FromRgb(0x00, 0x83, 0x8F), Color.FromRgb(0xC2, 0x18, 0x5B), Color.FromRgb(0x5D, 0x40, 0x37), Color.FromRgb(0xAF, 0xB4, 0x2B),
    ];

    private static readonly Brush[] SolidBrushes = Palette.Select(Frozen).ToArray();
    private static readonly Brush[] SoftBrushes = Palette.Select(c => Frozen(Color.FromArgb(0x38, c.R, c.G, c.B))).ToArray();

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Brush For(MergedSession s, bool soft) => (soft ? SoftBrushes : SolidBrushes)[s.Index % Palette.Length];

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is MergedSession s ? For(s, soft) : Brushes.Transparent; // separator rows have no session

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// Lines of all the open session tabs interleaved by timestamp. Starts from each tab's snapshot, then follows its events.
public partial class MergedView : UserControl
{
    private readonly TimelineMerge _merge = new();
    private ObservableCollection<LogLine> _visible = [];
    private readonly List<Action> _unsubscribe = [];
    private readonly Dictionary<MergedSession, CheckBox> _boxes = [];
    private readonly HashSet<LogLevel> _hiddenLevels = [];
    private readonly int[] _counts = new int[Enum.GetValues<LogLevel>().Length]; // entries per level in the merged rows
    private ContextFilter _ctx = new(0, _ => true, null);
    private Func<string, bool>? _filter;
    private ScrollViewer? _scroll;
    private LogLine? _menuLine;

    public event Action<LogView, LogLine>? RevealRequested; // source tab + the original line
    public string Title => L.F("Unione ({0})", _merge.Sessions.Count);

    public MergedView(IReadOnlyList<LogView> sources)
    {
        InitializeComponent();
        Lines.ItemsSource = _visible;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveVisible(), (_, e) => e.CanExecute = _visible.Count > 0));
        foreach (var view in sources) AddSource(view);
        Rebuild();
        L.Changed += OnLanguageChanged;
    }

    private void AddSource(LogView view)
    {
        var s = _merge.AddSession(view.Session.DisplayName, view.Session.Date ?? DateTime.Today, view);
        var box = new CheckBox { IsChecked = true, Margin = new Thickness(4, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        box.Checked += (_, _) => SetVisible(s, true);
        box.Unchecked += (_, _) => SetVisible(s, false);
        _boxes[s] = box;
        SessionPanel.Children.Add(box);

        var snapshot = view.Snapshot(); // same UI-thread turn as the subscription: no line is lost or doubled
        CountEntries(snapshot);
        _merge.Add(s, snapshot);
        UpdateBox(s);

        Action<IReadOnlyList<LogLine>> appended = lines => OnLines(s, lines, prepend: false);
        Action<IReadOnlyList<LogLine>> prepended = lines => OnLines(s, lines, prepend: true);
        Action closed = () => OnSourceClosed(s);
        view.LinesAppended += appended;
        view.LinesPrepended += prepended;
        view.Closed += closed;
        _unsubscribe.Add(() => { view.LinesAppended -= appended; view.LinesPrepended -= prepended; view.Closed -= closed; });
    }

    public void Close()
    {
        L.Changed -= OnLanguageChanged;
        foreach (var u in _unsubscribe) u();
        _unsubscribe.Clear();
    }

    private void OnLanguageChanged()
    {
        foreach (var s in _merge.Sessions) UpdateBox(s);
        UpdateCount();
    }

    private void CountEntries(IEnumerable<LogLine> lines)
    {
        foreach (var l in lines)
            if (l.IsEntry) _counts[(int)l.Level]++;
    }

    private void Recount()
    {
        Array.Clear(_counts);
        CountEntries(_merge.Rows);
    }

    private void OnLines(MergedSession s, IReadOnlyList<LogLine> lines, bool prepend)
    {
        CountEntries(lines);
        var inOrder = _merge.Add(s, lines, prepend);
        var trimmed = _merge.Trim();
        UpdateBox(s);
        if (trimmed) Recount();
        if (inOrder && !trimmed)
        {
            // Everything went to the end: feed only the new rows (same cheap path as LogView.Drain).
            for (var i = _merge.Rows.Count - lines.Count; i < _merge.Rows.Count; i++)
                foreach (var l in _ctx.Add(_merge.Rows, i)) _visible.Add(l);
            UpdateCount();
            if (Follow) ScrollToEnd();
        }
        else Rebuild(keepTopLine: true); // a row landed in the middle (or the head was trimmed): indices shifted
    }

    private void OnSourceClosed(MergedSession s)
    {
        s.Closed = true;
        s.Source = null; // rows stay; no events arrive any more
        UpdateBox(s);
    }

    private void UpdateBox(MergedSession s)
    {
        var box = _boxes[s];
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Border { Width = 10, Height = 10, Margin = new Thickness(0, 0, 4, 0), Background = SessionBrushConverter.For(s, soft: false) });
        panel.Children.Add(new TextBlock { Text = s.Closed ? $"{s.Name} {L.T("(chiusa)")}" : s.Name });
        box.Content = panel;
        var tip = s.Closed ? L.T("sessione chiusa") : L.T("Mostra o nasconde le righe di questa sessione");
        if (!s.HasTimestamps) tip += Environment.NewLine + L.T("Nessun orario riconoscibile: le righe stanno in testa.");
        box.ToolTip = tip;
        System.Windows.Automation.AutomationProperties.SetName(box, s.Name);
    }

    private void SetVisible(MergedSession s, bool visible)
    {
        s.Visible = visible;
        if (IsLoaded) Rebuild(); // Checked fires during InitializeComponent / before the view is shown
    }

    private bool Follow => FollowButton.IsChecked == true;

    private int ContextLines => int.TryParse(ContextBox.Text, out var n) ? Math.Clamp(n, 0, 50) : 0;

    // Lines with no detected level (None) are never hidden by the level filter; separators have no session and stay.
    private bool LineOk(LogLine l) => !_hiddenLevels.Contains(l.Level) && (l is not MergedLine m || m.Session.Visible);

    private void Rebuild(bool keepTopLine = false)
    {
        _scroll ??= FindChild<ScrollViewer>(Lines);
        var top = keepTopLine && _scroll != null && (int)_scroll.VerticalOffset < _visible.Count ? _visible[(int)_scroll.VerticalOffset] : null;
        _ctx = new ContextFilter(_filter == null ? 0 : ContextLines, LineOk, _filter);
        var visible = new List<LogLine>();
        for (var i = 0; i < _merge.Rows.Count; i++) visible.AddRange(_ctx.Add(_merge.Rows, i));
        _visible = new ObservableCollection<LogLine>(visible);
        Lines.ItemsSource = _visible;
        UpdateCount();
        if (Follow) ScrollToEnd();
        else if (top != null && _visible.IndexOf(top) is var index and >= 0)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => _scroll?.ScrollToVerticalOffset(index));
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
        var total = _merge.Rows.Count;
        CountLabel.Text = _visible.Count == total ? L.F("{0} righe{1}", total, "") : L.F("{0}/{1} righe{2}", _visible.Count, total, "");
    }

    private void Follow_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ScrollToEnd();
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
        if (IsLoaded) Rebuild();
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

    // ---- go to the source tab ----

    private MergedLine? LineAt(DependencyObject? d)
    {
        while (d != null && d is not ListBoxItem) d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return (d as ListBoxItem)?.DataContext as MergedLine;
    }

    private void Lines_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _menuLine = LineAt(e.OriginalSource as DependencyObject);
        if (_menuLine is not MergedLine { Session.Closed: false }) e.Handled = true; // no line, a separator or a closed session: no menu
    }

    private void GoToSource_Click(object sender, RoutedEventArgs e)
    {
        if (_menuLine is MergedLine m) Reveal(m);
    }

    private void Lines_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LineAt(e.OriginalSource as DependencyObject) is { } m) Reveal(m);
    }

    private void Reveal(MergedLine m)
    {
        if (m.Session.Closed || m.Session.Source is not LogView view) return;
        RevealRequested?.Invoke(view, m.Original);
    }

    // ---- save / copy ----

    private string[] VisibleTexts() => _visible.Select(l => l is MergedLine m ? $"[{m.Session.Name}] {m.Text}" : l.Text).ToArray();

    private void SaveVisible()
    {
        var dlg = new SaveFileDialog
        {
            Filter = L.T("Log (*.log)|*.log|Tutti i file (*.*)|*.*"),
            FileName = $"{L.T("Unione")}_{DateTime.Now:yyyyMMdd_HHmmss}.log",
            Title = L.T("Salva righe visibili"),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllLines(dlg.FileName, VisibleTexts(), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this), L.F("Salvataggio non riuscito: {0}", ex.Message), "Remote Log Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
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
