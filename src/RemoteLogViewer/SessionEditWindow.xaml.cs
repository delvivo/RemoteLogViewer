using System.IO;
using System.Windows;
using System.Windows.Media;

namespace RemoteLogViewer;

public partial class SessionEditWindow : Window
{
    public SessionConfig Config { get; }

    public SessionEditWindow(SessionConfig config)
    {
        InitializeComponent();
        Config = config.Clone();
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

    // Reads the form into Config. Returns validation errors.
    private List<string> ReadForm()
    {
        Config.Name = NameBox.Text.Trim();
        Config.SharePath = ShareBox.Text.Trim().TrimEnd('\\');
        Config.UserName = UserBox.Text.Trim();
        Config.FilePath = FileBox.Text.Trim();
        Config.Encoding = (string)EncodingBox.SelectedItem;
        if (PasswordBox.Password.Length > 0) Config.ProtectedPassword = SessionStore.Protect(PasswordBox.Password);
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
        var c = Config.Clone();
        var (ok, msg) = await Task.Run(() =>
        {
            try
            {
                SmbConnection.Connect(c.SharePath, c.UserName, SessionStore.Unprotect(c.ProtectedPassword));
                try
                {
                    return File.Exists(c.FullPath)
                        ? (true, $"OK: file trovato ({new FileInfo(c.FullPath).Length / 1024:N0} KB).")
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
