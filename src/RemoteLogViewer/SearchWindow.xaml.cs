using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace RemoteLogViewer;

/// One row of the flat results list: a session header or a hit with its match split for highlighting.
public record ResultRow(string Header, SearchHit? Hit, string Pre, string Match, string Post)
{
    public bool IsHeader => Hit == null;
    public string Automation => Hit == null ? Header : Pre + Match + Post;
}

/// Non-modal cross-tab search. Searches a snapshot of every open session's buffer; the results are a snapshot too (re-run to refresh).
public partial class SearchWindow : Window
{
    private readonly Func<IReadOnlyList<LogView>> _views;
    private readonly Func<LogView, LogLine, RevealResult> _reveal;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<SearchSource> _sources = [];
    private SearchResult? _result;
    private Func<string> _status = () => "";

    public SearchWindow(Func<IReadOnlyList<LogView>> views, Func<LogView, LogLine, RevealResult> reveal)
    {
        InitializeComponent();
        _views = views;
        _reveal = reveal;
        L.Changed += RefreshStatus;
        Loaded += (_, _) => TextBox.Focus();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        L.Changed -= RefreshStatus;
        _cts?.Cancel();
    }

    private void SetStatus(Func<string> text)
    {
        _status = text;
        RefreshStatus();
    }

    private void RefreshStatus() => StatusLabel.Text = _status();

    private void Busy(bool busy)
    {
        SearchButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        TextBox.ClearValue(BorderBrushProperty);
        FromTime.ClearValue(BorderBrushProperty);
        ToTime.ClearValue(BorderBrushProperty);
    }

    private SearchCriteria? BuildCriteria()
    {
        var levels = new[] { ErrorCheck, WarnCheck, InfoCheck, DebugCheck }
            .Where(c => c.IsChecked == true).Select(c => Enum.Parse<LogLevel>((string)c.Tag)).ToHashSet();
        if (!TryBound(FromDate, FromTime, endOfDay: false, out var from) || !TryBound(ToDate, ToTime, endOfDay: true, out var to)) return null;
        return new SearchCriteria(TextBox.Text, RegexCheck.IsChecked == true, CaseCheck.IsChecked == true, WordCheck.IsChecked == true, levels, from, to);
    }

    // Empty date + empty time = no bound. A time needs a date; a date alone means start (or end) of that day.
    private bool TryBound(DatePicker date, TextBox time, bool endOfDay, out DateTime? value)
    {
        value = null;
        var text = time.Text.Trim();
        if (date.SelectedDate is not { } d)
        {
            if (text.Length == 0) return true;
            time.BorderBrush = Brushes.Red;
            SetStatus(() => L.T("Indica anche la data"));
            return false;
        }
        if (text.Length == 0)
        {
            value = endOfDay ? d.Date.AddDays(1).AddTicks(-1) : d.Date;
            return true;
        }
        if (!TimeOnly.TryParseExact(text, ["H:mm", "H:mm:ss", "HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            time.BorderBrush = Brushes.Red;
            SetStatus(() => L.T("Ora non valida (usa HH:mm o HH:mm:ss)"));
            return false;
        }
        value = d.Date + t.ToTimeSpan();
        return true;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (BuildCriteria() is not { } criteria) return;
        if (criteria.IsEmpty)
        {
            SetStatus(() => L.T("Inserisci un testo o un filtro"));
            return;
        }
        var views = _views();
        if (views.Count == 0)
        {
            SetStatus(() => L.T("Nessuna sessione aperta"));
            return;
        }

        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var sources = views.Select(v => new SearchSource(v.Session.DisplayName, v.Snapshot(), v.Session.Date ?? DateTime.Today, v)).ToList();
        Busy(true);
        SetStatus(() => L.T("Ricerca in corso…"));
        try
        {
            var result = await Task.Run(() => LogSearch.Run(sources, criteria, cts.Token), cts.Token);
            if (cts == _cts) Show(sources, result);
        }
        catch (OperationCanceledException)
        {
            if (cts == _cts) SetStatus(() => L.T("Ricerca annullata"));
        }
        catch (ArgumentException ex) // invalid regex: previous results stay
        {
            if (cts != _cts) return;
            TextBox.BorderBrush = Brushes.Red;
            SetStatus(() => L.F("Espressione regolare non valida: {0}", ex.Message));
        }
        finally
        {
            if (cts == _cts) Busy(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Show(IReadOnlyList<SearchSource> sources, SearchResult result)
    {
        _sources = sources;
        _result = result;
        var rows = new List<ResultRow>();
        foreach (var g in result.Hits.GroupBy(h => h.Source))
        {
            rows.Add(new ResultRow($"{sources[g.Key].Name} · {g.Count()}", null, "", "", ""));
            rows.AddRange(g.Select(Row));
        }
        Results.ItemsSource = rows;
        ExportButton.IsEnabled = result.Hits.Count > 0;
        var groups = result.Hits.Select(h => h.Source).Distinct().Count();
        var total = result.Hits.Count;
        SetStatus(() => total == 0 ? L.T("Nessun risultato")
            : L.F("{0} risultati in {1} sessioni", total, groups) + (result.Truncated ? " · " + L.F("Risultati limitati a {0:N0}: restringi la ricerca", LogSearch.MaxHits) : ""));
    }

    // Long lines: keep the match visible by trimming the text before it.
    private static ResultRow Row(SearchHit h)
    {
        var text = h.Line.Text;
        var pre = text[..h.Start];
        if (pre.Length > 60) pre = "…" + pre[^60..];
        var post = text[(h.Start + h.Length)..];
        if (post.Length > 300) post = post[..300];
        var level = h.Line.Level == LogLevel.None ? "" : $"[{h.Line.Level.ToString().ToUpperInvariant()}] ";
        return new ResultRow("", h, level + pre, text.Substring(h.Start, h.Length), post);
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(Results, (DependencyObject)e.OriginalSource) is ListBoxItem { DataContext: ResultRow row }) Activate(row);
    }

    private void Results_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Results.SelectedItem is not ResultRow row) return;
        Activate(row);
        e.Handled = true;
    }

    private void Activate(ResultRow row)
    {
        if (row.Hit is not { } hit || _sources[hit.Source].Tag is not LogView view) return;
        var result = _reveal(view, hit.Line);
        SetStatus(() => result switch
        {
            RevealResult.NotFound => L.T("La riga non è più disponibile nel buffer"),
            RevealResult.ShownFiltersCleared => L.T("Filtri rimossi"),
            _ => "",
        });
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_result is not { Hits.Count: > 0 } result) return;
        var dlg = new SaveFileDialog
        {
            Filter = L.T("Log (*.log)|*.log|Tutti i file (*.*)|*.*"),
            FileName = $"ricerca_{DateTime.Now:yyyyMMdd_HHmmss}.log",
            Title = L.T("Esporta risultati"),
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, LogSearch.FormatExport(_sources, result), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.F("Salvataggio non riuscito: {0}", ex.Message), "Remote Log Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
