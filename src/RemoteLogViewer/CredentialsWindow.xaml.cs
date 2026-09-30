using System.Windows;
using System.Windows.Media;

namespace RemoteLogViewer;

/// Manages SessionTree.Credentials in place; `save` persists after every change.
public partial class CredentialsWindow : Window
{
    private readonly SessionTree _tree;
    private readonly Action _save;

    public CredentialsWindow(SessionTree tree, Action save)
    {
        InitializeComponent();
        (_tree, _save) = (tree, save);
        Refresh(tree.Credentials.FirstOrDefault());
        Loaded += (_, _) => NameBox.Focus();
    }

    private Credential? Current => List.SelectedItem as Credential;

    private void Refresh(Credential? select)
    {
        List.ItemsSource = _tree.Credentials.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        List.SelectedItem = select;
        if (select == null) Load(null); // SelectionChanged doesn't fire when nothing was selected
    }

    private void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => Load(Current);

    private void Load(Credential? c)
    {
        NameBox.Text = c?.Name ?? "";
        UserBox.Text = c?.UserName ?? "";
        PasswordBox.Clear();
        if (c?.ProtectedPassword != null && SessionStore.Unprotect(c.ProtectedPassword) == null)
            ShowResult("Password salvata non leggibile su questo PC/utente: reinseriscila.", false);
        else
            ShowResult(c == null ? "Nuova credenziale." : $"Usata da {_tree.CountUsing(c.Id)} sessioni.", null);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        List.SelectedItem = null;
        Load(null);
        NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var c = Current;
        var name = NameBox.Text.Trim();
        var user = UserBox.Text.Trim();
        var errors = new List<string>();
        if (name.Length == 0) errors.Add("Nome obbligatorio.");
        else if (_tree.Credentials.Any(x => x != c && string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            errors.Add("Esiste già una credenziale con questo nome.");
        if (user.Length == 0) errors.Add("Utente obbligatorio.");
        if (errors.Count > 0) { ShowResult(string.Join("\n", errors), false); return; }

        if (c == null) _tree.Credentials.Add(c = new Credential());
        c.Name = name;
        c.UserName = user;
        if (PasswordBox.Password.Length > 0) c.ProtectedPassword = SessionStore.Protect(PasswordBox.Password);
        _save();
        Refresh(c);
        ShowResult($"Salvata. Usata da {_tree.CountUsing(c.Id)} sessioni (le sessioni già aperte la useranno alla prossima apertura).", true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Current is not { } c) return;
        var n = _tree.CountUsing(c.Id);
        var question = n == 0 ? $"Eliminare la credenziale \"{c.Name}\"?"
            : $"La credenziale \"{c.Name}\" è usata da {n} sessioni, che useranno utente/password propri (se vuoti, l'utente Windows corrente). Eliminarla?";
        if (MessageBox.Show(this, question, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _tree.DeleteCredential(c.Id);
        _save();
        Refresh(_tree.Credentials.FirstOrDefault());
    }

    private void ShowResult(string text, bool? ok)
    {
        Result.Text = text;
        Result.Foreground = ok switch { true => Brushes.Green, false => Brushes.Red, null => Brushes.Gray };
    }
}
