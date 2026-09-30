using System.Windows;
using System.Windows.Controls;

namespace RemoteLogViewer;

/// Asks which day to open for a session whose path contains `{date:…}`.
public partial class DateDialog : Window
{
    private readonly SessionConfig _config;

    public DateTime SelectedDate => Picker.SelectedDate!.Value.Date;

    public DateDialog(SessionConfig config)
    {
        InitializeComponent();
        _config = config;
        Title = $"Avvia \"{config.Name}\"";
        Picker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => Picker.Focus();
    }

    private void Picker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = Picker.SelectedDate != null;
        Preview.Text = Picker.SelectedDate is { } d ? $"Anteprima: {_config.ResolvePath(d)}" : "Scegli una data.";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Picker.SelectedDate != null) DialogResult = true;
    }
}
