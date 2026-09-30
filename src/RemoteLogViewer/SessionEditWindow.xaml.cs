using System.IO;
using System.Windows;
using System.Windows.Media;

namespace RemoteLogViewer;

public partial class SessionEditWindow : Window
{
    private const string InlineCredential = "(utente/password qui sotto)";
    private readonly IReadOnlyList<Credential> _credentials;

    public SessionConfig Config { get; }

    public SessionEditWindow(SessionConfig config, IReadOnlyList<Credential> credentials)
    {
        InitializeComponent();
        _credentials = credentials;
        Config = config.Clone();
        CredentialBox.ItemsSource = new object[] { InlineCredential }.Concat(credentials.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)).ToList();
        CredentialBox.SelectedItem = credentials.FirstOrDefault(c => c.Id == Config.CredentialId) ?? (object)InlineCredential;
        NameBox.Text = Config.Name;
        ShareBox.Text = Config.SharePath;
        UserBox.Text = Config.UserName;
        FileBox.Text = Config.FilePath;
        TailBox.Text = Config.TailLines.ToString();
        EncodingBox.ItemsSource = new[] { "auto", "utf-8", "utf-16", "windows-1252" };
        EncodingBox.SelectedItem = Config.Encoding;
        if (Config.ProtectedPassword != null && SessionStore.Unprotect(Config.ProtectedPassword) == null)
            ShowResult("Password salvata non leggibile su questo PC/utente: reinseriscila.", false);
        Loaded += (_, _) => NameBox.Focus();
    }

    private void CredentialBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var inline = CredentialBox.SelectedItem is not Credential;
        UserBox.IsEnabled = PasswordBox.IsEnabled = inline;
    }

    private void FileBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var file = FileBox.Text.Trim();
        if (!DatePath.Has(file)) { FilePreview.Visibility = Visibility.Collapsed; return; }
        FilePreview.Visibility = Visibility.Visible;
        var errors = DatePath.Validate(file);
        var preview = new SessionConfig { SharePath = ShareBox.Text.Trim().TrimEnd('\\'), FilePath = file };
        FilePreview.Text = errors.Count > 0 ? string.Join(" ", errors) : $"Anteprima (oggi): {preview.ResolvePath(DateTime.Today)}";
        FilePreview.Foreground = errors.Count > 0 ? Brushes.Red : Brushes.Gray;
    }

    // Reads the form into Config. Returns validation errors.
    private List<string> ReadForm()
    {
        Config.Name = NameBox.Text.Trim();
        Config.SharePath = ShareBox.Text.Trim().TrimEnd('\\');
        Config.UserName = UserBox.Text.Trim();
        Config.FilePath = FileBox.Text.Trim();
        Config.Encoding = (string)EncodingBox.SelectedItem;
        Config.CredentialId = (CredentialBox.SelectedItem as Credential)?.Id;
        if (Config.CredentialId != null) (Config.UserName, Config.ProtectedPassword) = ("", null); // the credential wins
        else if (PasswordBox.Password.Length > 0) Config.ProtectedPassword = SessionStore.Protect(PasswordBox.Password);
        else if (Config.UserName.Length == 0) Config.ProtectedPassword = null;
        var errors = new List<string>();
        if (int.TryParse(TailBox.Text, out var n)) Config.TailLines = n; else errors.Add("Righe iniziali non valide.");
        errors.AddRange(Config.Validate());
        return errors;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var errors = ReadForm();
        errors.Remove("Nome obbligatorio.");
        if (errors.Count > 0) { ShowResult(string.Join("\n", errors), false); return; }

        TestButton.IsEnabled = false;
        ShowResult("Connessione…", null);
        var c = Config.WithCredential(_credentials);
        var (ok, msg) = await Task.Run(() =>
        {
            try
            {
                SmbConnection.Connect(c.SharePath, c.UserName, SessionStore.Unprotect(c.ProtectedPassword));
                try
                {
                    var path = c.ResolvePath(DateTime.Today);
                    return File.Exists(path)
                        ? (true, $"OK: file trovato ({new FileInfo(path).Length / 1024:N0} KB).")
                        : (false, "Share OK, ma il file non esiste (la sessione resterà in attesa).");
                }
                finally { SmbConnection.Release(c.SharePath); }
            }
            catch (Exception ex) { return (false, ex.Message); }
        });
        ShowResult(msg, ok);
        TestButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var errors = ReadForm();
        if (errors.Count > 0) { ShowResult(string.Join("\n", errors), false); return; }
        DialogResult = true;
    }

    private void ShowResult(string text, bool? ok)
    {
        Result.Text = text;
        Result.Foreground = ok switch { true => Brushes.Green, false => Brushes.Red, null => Brushes.Gray };
    }
}
