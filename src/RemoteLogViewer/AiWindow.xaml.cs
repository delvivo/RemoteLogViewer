using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RemoteLogViewer;

/// Non-modal AI analysis: sends the visible lines of the checked tabs (captured at start) plus a prompt to an external CLI
/// (AiRunner) and shows its output as it arrives. One run at a time; closing the window kills the tool.
public partial class AiWindow : Window
{
    private readonly Func<IReadOnlyList<LogView>> _views;
    private readonly HashSet<LogView> _selected = [];
    private List<LogView> _shown = [];
    private readonly AiSettings _settings = AiSettings.Load(AiSettings.DefaultPath);
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Stopwatch _clock = new();
    private CancellationTokenSource? _cts;
    private AiReport? _report;
    private string _toolId;
    private bool _refreshing;
    private Func<string> _status = () => "";

    public AiWindow(Func<IReadOnlyList<LogView>> views, LogView? active)
    {
        InitializeComponent();
        _views = views;
        if (active != null) _selected.Add(active);
        _toolId = AiTool.Get(_settings.SelectedTool).Id;
        _refreshing = true;
        ToolBox.ItemsSource = AiTool.All;
        ToolBox.SelectedItem = AiTool.Get(_toolId);
        _refreshing = false;
        CommandBox.Text = _settings.CommandFor(_toolId);
        PromptBox.Text = _settings.LastPrompt ?? AiPrompt.All()[0].Text;
        FillPresets();
        RefreshTabs();
        _timer.Tick += (_, _) => Drain();
        L.Changed += OnLanguageChanged;
        Loaded += (_, _) => PromptBox.Focus();
    }

    private bool Running => _cts != null;

    private void Window_Activated(object? sender, EventArgs e) => RefreshTabs();

    private void Window_Closed(object? sender, EventArgs e)
    {
        L.Changed -= OnLanguageChanged;
        _cts?.Cancel();
        _timer.Stop();
        CommitCommand();
    }

    // ---- tabs ----

    /// Rebuilt only when the open tabs (or their titles) changed: rebuilding on every activation would eat the click that activated the window.
    private void RefreshTabs()
    {
        var views = _views().ToList();
        if (views.SequenceEqual(_shown) && TabList.Children.OfType<CheckBox>().Select(c => (string)c.Content).SequenceEqual(views.Select(v => v.Session.DisplayName))) return;
        _shown = views;
        _selected.IntersectWith(views);
        TabList.Children.Clear();
        foreach (var v in views)
        {
            var box = new CheckBox { Content = v.Session.DisplayName, Tag = v, IsChecked = _selected.Contains(v), Margin = new Thickness(0, 2, 0, 2) };
            box.Checked += (_, _) => { _selected.Add(v); UpdateButtons(); };
            box.Unchecked += (_, _) => { _selected.Remove(v); UpdateButtons(); };
            TabList.Children.Add(box);
        }
        if (views.Count == 0) TabList.Children.Add(new TextBlock { Text = L.T("Nessuna sessione aperta"), Foreground = System.Windows.Media.Brushes.Gray });
        UpdateButtons();
    }

    // ---- tool, command, prompt ----

    private void Tool_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ToolBox.SelectedItem is not AiTool tool) return;
        _toolId = _settings.SelectedTool = tool.Id;
        CommandBox.Text = _settings.CommandFor(tool.Id);
        SaveSettings();
    }

    private void Command_LostFocus(object sender, RoutedEventArgs e) => CommitCommand();

    private void CommitCommand()
    {
        _settings.SetCommand(_toolId, CommandBox.Text);
        SaveSettings();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        CommandBox.Text = AiTool.Get(_toolId).DefaultCommand;
        CommitCommand();
    }

    private void FillPresets()
    {
        _refreshing = true;
        var presets = AiPrompt.All();
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedItem = presets.FirstOrDefault(p => p.Text == PromptBox.Text);
        _refreshing = false;
    }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || PresetBox.SelectedItem is not AiPrompt preset) return;
        PromptBox.Text = preset.Text;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) UpdateButtons();
    }

    private void OnLanguageChanged()
    {
        _refreshing = true;
        ToolBox.ItemsSource = null; // names are translated on read: re-bind to show them in the new language
        ToolBox.ItemsSource = AiTool.All;
        ToolBox.SelectedItem = AiTool.Get(_toolId);
        _refreshing = false;
        FillPresets(); // prompt text stays as typed
        _shown = []; // re-creates the "no session" hint in the new language
        RefreshTabs();
        RefreshStatus();
    }

    private void SaveSettings()
    {
        try { _settings.Save(AiSettings.DefaultPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // not remembered; the run itself is unaffected
    }

    // ---- run ----

    private void UpdateButtons()
    {
        StartButton.IsEnabled = !Running && _selected.Count > 0 && PromptBox.Text.Trim().Length > 0 && CommandBox.Text.Trim().Length > 0;
        CancelButton.IsEnabled = Running;
        var done = !Running && _report is { Response.Length: > 0 };
        CopyButton.IsEnabled = OpenButton.IsEnabled = ExportButton.IsEnabled = done;
    }

    private void SetStatus(Func<string> status)
    {
        _status = status;
        RefreshStatus();
    }

    private void RefreshStatus() => StatusLabel.Text = _status();

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Running) return;
        RefreshTabs();
        var views = _shown.Where(_selected.Contains).ToList();
        if (views.Count == 0 || PromptBox.Text.Trim().Length == 0) return;
        CommitCommand();
        var tool = AiTool.Get(_toolId);
        if (!_settings.Consented.Contains(tool.Id))
        {
            var answer = MessageBox.Show(this, L.F("I log selezionati verranno inviati a {0}. Per gli strumenti online i dati lasciano questo PC. Continuare?", tool.Name),
                L.T("Analisi IA"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
            _settings.Consented.Add(tool.Id);
        }
        var prompt = PromptBox.Text;
        _settings.LastPrompt = prompt;
        SaveSettings();

        var command = _settings.CommandFor(tool.Id);
        var input = AiInput.Build(prompt, views.Select(v => new AiSource(v.Session.DisplayName, v.Session.Path, v.VisibleTexts())).ToList());
        var cts = _cts = new CancellationTokenSource();
        _report = null;
        _pending.Clear();
        OutputBox.Clear();
        _clock.Restart();
        _timer.Start();
        SetStatus(() => L.F("In corso… {0}", Elapsed()) + Dropped(input.Dropped));
        UpdateButtons();

        AiRunResult result;
        try
        {
            result = await AiRunner.RunAsync(command, input.Text, _pending.Enqueue, cts.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            result = new AiRunResult(-1, ex.Message, false);
        }
        _clock.Stop();
        _timer.Stop();
        Drain();
        _cts = null;
        cts.Dispose();
        if (!IsLoaded) return; // window closed meanwhile

        var output = OutputBox.Text;
        var time = Elapsed();
        if (result.Cancelled) SetStatus(() => L.T("Annullato") + Dropped(input.Dropped));
        else if (result.ExitCode == AiRunner.NotFoundExitCode) SetStatus(() => L.F("Comando non trovato: {0}", command));
        else if (result.ExitCode != 0)
        {
            SetStatus(() => L.F("Errore (codice {0})", result.ExitCode!) + Dropped(input.Dropped));
            if (result.Error.Length > 0) OutputBox.AppendText((output.Length > 0 ? "\n\n" : "") + result.Error);
        }
        else if (output.Trim().Length == 0)
        {
            SetStatus(() => L.T("Nessuna risposta"));
            if (result.Error.Length > 0) OutputBox.AppendText(result.Error);
        }
        else SetStatus(() => L.F("Completato in {0}", time) + Dropped(input.Dropped));
        OutputBox.ScrollToEnd();

        _report = new AiReport(DateTime.Now, tool.Name, command, views.Select(v => v.Session.DisplayName).ToList(), prompt, input.Dropped, output);
        UpdateButtons();
    }

    private static string Dropped(int n) => n > 0 ? " · " + L.F("{0} righe escluse per limite 1 MB", n) : "";

    private string Elapsed() => _clock.Elapsed.ToString(@"mm\:ss");

    private void Drain()
    {
        if (!_pending.IsEmpty)
        {
            var sb = new StringBuilder();
            while (_pending.TryDequeue(out var s)) sb.Append(s);
            OutputBox.AppendText(sb.ToString());
            OutputBox.ScrollToEnd();
        }
        if (Running) RefreshStatus(); // elapsed time
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ---- results ----

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_report != null) Clipboard.SetText(_report.Response);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) return;
        try
        {
            var file = Path.Combine(Path.GetTempPath(), "RemoteLogViewer", "ai-report.html");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, _report.ToHtml(), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, ex.Message, L.T("Analisi IA"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md|HTML (*.html)|*.html",
            FileName = $"ai-report-{_report.Time:yyyyMMdd-HHmm}",
            DefaultExt = ".md",
            Title = L.T("Esporta report"),
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var html = Path.GetExtension(dlg.FileName).Equals(".html", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(dlg.FileName).Equals(".htm", StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(dlg.FileName, html ? _report.ToHtml() : _report.ToMarkdown(), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.F("Salvataggio non riuscito: {0}", ex.Message), L.T("Analisi IA"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
