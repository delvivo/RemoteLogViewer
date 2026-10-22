using System.Windows;

namespace RemoteLogViewer;

/// Minimal text prompt (WPF has no InputBox). `validate` returns an error message or null.
public partial class TextDialog : Window
{
    private readonly Func<string, string?>? _validate;
    private readonly string? _emptyError;

    private TextDialog(string title, string label, string initial, Func<string, string?>? validate, string? emptyError)
    {
        InitializeComponent();
        Title = title;
        Prompt.Content = label;
        Input.Text = initial;
        _validate = validate;
        _emptyError = emptyError;
        Loaded += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }

    /// `emptyError` = message for an empty input (default: the name one).
    public static string? Ask(Window owner, string title, string label, string initial = "", Func<string, string?>? validate = null, string? emptyError = null)
    {
        var dlg = new TextDialog(title, label, initial, validate, emptyError) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Input.Text.Trim() : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = Input.Text.Trim();
        var error = text.Length == 0 ? _emptyError ?? L.T("Nome obbligatorio.") : _validate?.Invoke(text);
        if (error != null) { Error.Text = error; return; }
        DialogResult = true;
    }
}
