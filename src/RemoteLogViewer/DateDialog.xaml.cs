using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RemoteLogViewer;

/// Asks which day to open for a session whose path contains `{date:…}`.
/// Checks which days of the displayed month have a file and crosses out the missing ones (today stays selectable: the session waits for it).
public partial class DateDialog : Window
{
    private readonly SessionConfig _config;
    private readonly Dictionary<DateTime, bool> _exists = [];
    private readonly HashSet<DateTime> _probedMonths = [];
    private readonly Task<string?> _connect; // null = connected, otherwise the error

    public DateTime SelectedDate => Cal.SelectedDate!.Value.Date;

    /// `config` must already have its credential applied (`WithCredential`).
    public DateDialog(SessionConfig config)
    {
        InitializeComponent();
        _config = config;
        Title = $"Avvia \"{config.Name}\"";
        _connect = Task.Run(() =>
        {
            try { SmbConnection.Connect(config.SharePath, config.UserName, SessionStore.Unprotect(config.ProtectedPassword)); return null; }
            catch (Exception ex) { return ex.Message; }
        });
        Closed += async (_, _) => { if (await _connect == null) SmbConnection.Release(config.SharePath); };
        Cal.SelectedDate = Cal.DisplayDate = DateTime.Today;
        Loaded += (_, _) => { Cal.Focus(); Probe(); };
    }

    private async void Probe()
    {
        var month = new DateTime(Cal.DisplayDate.Year, Cal.DisplayDate.Month, 1);
        if (Cal.DisplayMode != CalendarMode.Month || !_probedMonths.Add(month)) return;
        Status.Text = "Verifica dei file disponibili…";
        if (await _connect is { } error) { Status.Text = $"Impossibile verificare i file: {error}"; return; }

        var days = Enumerable.Range(0, DateTime.DaysInMonth(month.Year, month.Month)).Select(i => month.AddDays(i)).ToList();
        // ponytail: one File.Exists per day, works for any placeholder position/format; ~31 SMB stats per month
        var found = await Task.Run(() => days.AsParallel().WithDegreeOfParallelism(8)
            .Select(d => (d, File.Exists(_config.ResolvePath(d)))).ToList());
        foreach (var (d, ok) in found)
        {
            _exists[d] = ok;
            if (!ok && d != DateTime.Today && d != Cal.SelectedDate) Cal.BlackoutDates.Add(new CalendarDateRange(d));
        }
        Status.Text = "I giorni barrati non hanno un file di log.";
        UpdatePreview();
    }

    private void Cal_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e) => Probe();
    private void Cal_DisplayModeChanged(object? sender, CalendarModeChangedEventArgs e) => Probe();

    private void Cal_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e) => UpdatePreview();

    // Calendar keeps mouse capture after a click, so the next click on a button would be swallowed.
    private void Cal_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is System.Windows.Controls.Primitives.CalendarItem) Mouse.Capture(null);
    }

    private void UpdatePreview()
    {
        OkButton.IsEnabled = Cal.SelectedDate != null;
        if (Cal.SelectedDate is not { } d) { Preview.Text = "Scegli una data."; return; }
        var note = _exists.TryGetValue(d, out var ok) ? ok ? " (file presente)" : " (file non trovato: la sessione resterà in attesa)" : "";
        Preview.Text = $"Anteprima: {_config.ResolvePath(d)}{note}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Cal.SelectedDate != null) DialogResult = true;
    }
}
