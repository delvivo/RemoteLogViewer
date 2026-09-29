using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RemoteLogViewer;

public partial class MainWindow : Window
{
    private readonly SessionStore _store = new(SessionStore.DefaultPath);
    private readonly ObservableCollection<SessionConfig> _sessions;

    public MainWindow()
    {
        InitializeComponent();
        _sessions = new(_store.Load());
        SessionList.ItemsSource = _sessions;
        if (_store.Warning != null) Loaded += (_, _) => MessageBox.Show(this, _store.Warning, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private SessionConfig? Selected => SessionList.SelectedItem as SessionConfig;

    private void Save() => _store.Save(_sessions);

    // ---- saved sessions ----

    private void New_Click(object sender, RoutedEventArgs e) => Add(new SessionConfig());

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        var copy = s.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name += " (copia)";
        Add(copy);
    }

    private void Add(SessionConfig config)
    {
        var dlg = new SessionEditWindow(config) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _sessions.Add(dlg.Config);
        SessionList.SelectedItem = dlg.Config;
        Save();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        var dlg = new SessionEditWindow(s) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _sessions[_sessions.IndexOf(s)] = dlg.Config;
        SessionList.SelectedItem = dlg.Config;
        Save();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        if (MessageBox.Show(this, $"Eliminare la sessione \"{s.Name}\"?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _sessions.Remove(s);
        Save();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } s) Open(s);
    }

    private void SessionList_DoubleClick(object sender, MouseButtonEventArgs e) => Start_Click(sender, e);

    private void SessionList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Start_Click(sender, e);
        else if (e.Key == Key.Delete) Delete_Click(sender, e);
    }

    // ---- active sessions ----

    private void Open(SessionConfig config)
    {
        var view = new LogView(new ActiveSession(config.Clone())); // clone: later edits don't touch a running session
        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 0), Fill = view.StatusBrush };
        var close = new Button { Content = "✕", Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(dot);
        header.Children.Add(new TextBlock { Text = config.Name });
        header.Children.Add(close);
        var tab = new TabItem { Header = header, Tag = view, ToolTip = config.FullPath };
        view.StatusChanged += () => dot.Fill = view.StatusBrush;
        close.Click += (_, _) => CloseTab(tab);
        tab.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) CloseTab(tab); };
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
        Relayout();
    }

    private void CloseTab(TabItem tab)
    {
        ((LogView)tab.Tag).Close();
        Tabs.Items.Remove(tab);
        Relayout();
    }

    private void SideBySide_Click(object sender, RoutedEventArgs e) => Relayout();

    // Tabs are the source of truth; side-by-side mode re-parents the same LogViews into the grid.
    private void Relayout()
    {
        var tabs = Tabs.Items.Cast<TabItem>().ToList();
        foreach (var t in tabs)
        {
            var v = (LogView)t.Tag;
            if (v.Parent is TabItem ti) ti.Content = null;
            else if (v.Parent is Panel p) p.Children.Remove(v);
        }
        SideBySideGrid.Children.Clear();

        var side = SideBySideButton.IsChecked == true;
        foreach (var t in tabs)
        {
            var v = (LogView)t.Tag;
            if (!side) { t.Content = v; continue; }
            var title = new TextBlock { Text = v.Session.Config.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 2, 4, 2) };
            DockPanel.SetDock(title, Dock.Top);
            var cell = new DockPanel();
            cell.Children.Add(title);
            cell.Children.Add(v);
            SideBySideGrid.Children.Add(new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Margin = new Thickness(2), Child = cell });
        }
        SideBySideGrid.Columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(tabs.Count))); // 2 → 2×1, 3-4 → 2×2
        Tabs.Visibility = side ? Visibility.Collapsed : Visibility.Visible;
        SideBySideGrid.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (TabItem t in Tabs.Items) ((LogView)t.Tag).Close();
    }
}
