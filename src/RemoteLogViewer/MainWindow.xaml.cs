using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace RemoteLogViewer;

public partial class MainWindow : Window
{
    private const string TreeDragFormat = "rlv-tree-item";
    private const string TabDragFormat = "rlv-tab";
    private static string ExportFilter => L.T("Collezioni Remote Log Viewer (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json");

    private readonly SessionStore _store = new(SessionStore.DefaultPath);
    private readonly SessionTree _tree;
    private readonly HashSet<Guid> _expanded = [];

    private Point _dragStart;
    private object? _dragItem;   // SessionConfig / SessionCollection being dragged in the tree
    private TabItem? _dragTab;

    public MainWindow()
    {
        InitializeComponent();
        LangBox.ItemsSource = new Dictionary<string, string> { ["it"] = "Italiano", ["en"] = "English" };
        LangBox.SelectedValue = L.Lang;
        _tree = _store.Load();
        RebuildTree();
        if (_store.Warning != null) Loaded += (_, _) => MessageBox.Show(this, _store.Warning, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Save() => _store.Save(_tree);

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LangBox.SelectedValue is string lang) L.SetLang(lang);
    }

    // ---- saved sessions tree ----

    /// SessionConfig, SessionCollection or null.
    private object? Selected => SessionTreeView.SelectedItem switch
    {
        SessionNode n => n.Session,
        CollectionNode n => n.Collection,
        _ => null,
    };

    /// Where "Nuova"/"Collezione" put new items: the selected collection, or the selected session's collection.
    private Guid? TargetCollection => Selected switch
    {
        SessionCollection c => c.Id,
        SessionConfig s => s.CollectionId,
        _ => null,
    };

    // ponytail: full rebuild after every change; fine for hundreds of items, diff the nodes if it ever lags.
    private void RebuildTree(object? select = null)
    {
        select ??= Selected;
        var parent = select switch { SessionConfig s => s.CollectionId, SessionCollection c => c.ParentId, _ => null };
        while (parent is { } id) // expand ancestors so the selected item is visible
        {
            _expanded.Add(id);
            parent = _tree.Find(id)?.ParentId;
        }

        List<object> Build(Guid? parent)
        {
            var nodes = new List<object>();
            foreach (var c in _tree.ChildCollections(parent))
            {
                var n = new CollectionNode(c, _expanded) { IsSelected = c == select };
                n.Children.AddRange(Build(c.Id));
                nodes.Add(n);
            }
            foreach (var s in _tree.ChildSessions(parent)) nodes.Add(new SessionNode(s) { IsSelected = s == select });
            return nodes;
        }
        SessionTreeView.ItemsSource = Build(null);
    }

    private void New_Click(object sender, RoutedEventArgs e) => AddSession(new SessionConfig { CollectionId = TargetCollection });

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is SessionConfig s) Duplicate(s);
    }

    private void Duplicate(SessionConfig s)
    {
        var copy = s.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name += L.T(" (copia)");
        AddSession(copy);
    }

    private void AddSession(SessionConfig config)
    {
        var dlg = new SessionEditWindow(config, _tree.Credentials) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _tree.Sessions.Add(dlg.Config);
        Save();
        RebuildTree(dlg.Config);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        switch (Selected)
        {
            case SessionConfig s: EditSession(s); break;
            case SessionCollection c: Rename(c); break;
        }
    }

    private void EditSession(SessionConfig s)
    {
        var dlg = new SessionEditWindow(s, _tree.Credentials) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _tree.Sessions[_tree.Sessions.IndexOf(s)] = dlg.Config; // CollectionId carried over by the clone
        Save();
        RebuildTree(dlg.Config);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        switch (Selected)
        {
            case SessionConfig s: DeleteSession(s); break;
            case SessionCollection c: DeleteCollection(c); break;
        }
    }

    private void DeleteSession(SessionConfig s)
    {
        if (!Confirm(L.F("Eliminare la sessione \"{0}\"?", s.Name))) return;
        _tree.Sessions.Remove(s);
        Save();
        RebuildTree();
    }

    private void DeleteCollection(SessionCollection c)
    {
        int sessions = _tree.CountSessions(c.Id), collections = _tree.CountCollections(c.Id);
        var question = sessions + collections == 0
            ? L.F("Eliminare la collezione \"{0}\"?", c.Name)
            : L.F("Eliminare la collezione \"{0}\" con {1} sessioni e {2} sottocollezioni?", c.Name, sessions, collections);
        if (!Confirm(question)) return;
        _tree.Delete(c.Id); // open tabs keep running: they use a clone of the config
        Save();
        RebuildTree();
    }

    private bool Confirm(string question) =>
        MessageBox.Show(this, question, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void NewCollection_Click(object sender, RoutedEventArgs e) => NewCollection(TargetCollection);

    private void NewCollection(Guid? parentId)
    {
        var name = TextDialog.Ask(this, L.T("Nuova collezione"), L.T("_Nome"), "",
            n => _tree.NameTaken(parentId, n) ? L.T("Esiste già una collezione con questo nome.") : null);
        if (name == null) return;
        var c = new SessionCollection { Name = name, ParentId = parentId };
        _tree.Collections.Add(c);
        Save();
        RebuildTree(c);
    }

    private void Rename(SessionCollection c)
    {
        var name = TextDialog.Ask(this, L.T("Rinomina collezione"), L.T("_Nome"), c.Name,
            n => _tree.NameTaken(c.ParentId, n, c.Id) ? L.T("Esiste già una collezione con questo nome.") : null);
        if (name == null) return;
        c.Name = name;
        Save();
        RebuildTree(c);
    }

    private void MoveTo(object item, Guid? parentId)
    {
        switch (item)
        {
            case SessionCollection c: _tree.Move(c, parentId); break;
            case SessionConfig s: _tree.Move(s, parentId); break;
        }
        if (parentId is { } p) _expanded.Add(p);
        Save();
        RebuildTree(item);
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is SessionConfig s) Open(s);
    }

    private void SessionTree_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        // collections toggle on double click by themselves
        if (Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext is SessionNode n) { e.Handled = true; Open(n.Session); }
    }

    private void SessionTree_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when SessionTreeView.SelectedItem is CollectionNode n:
                n.IsExpanded = !n.IsExpanded;
                RebuildTree();
                break;
            case Key.Enter: Start_Click(sender, e); break;
            case Key.Delete: Delete_Click(sender, e); break;
            case Key.F2: Edit_Click(sender, e); break;
            default: return;
        }
        e.Handled = true;
    }

    // ---- context menu ----

    private void SessionTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var tvi = Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (tvi != null) tvi.IsSelected = true;
        var menu = SessionTreeView.ContextMenu!;
        menu.Items.Clear();
        void Add(string header, Action action) => menu.Items.Add(Item(L.T(header), action));
        void Sep() => menu.Items.Add(new Separator());

        switch (tvi?.DataContext)
        {
            case SessionNode { Session: var s }:
                Add("Avvia", () => Open(s));
                Add("Modifica", () => EditSession(s));
                Add("Duplica", () => Duplicate(s));
                menu.Items.Add(MoveMenu(s, s.CollectionId));
                Sep();
                Add("Elimina", () => DeleteSession(s));
                break;
            case CollectionNode { Collection: var c }:
                Add("Nuova sessione qui", () => AddSession(new SessionConfig { CollectionId = c.Id }));
                Add("Nuova sottocollezione", () => NewCollection(c.Id));
                Add("Rinomina", () => Rename(c));
                Add("Duplica", () => { var copy = _tree.Duplicate(c.Id); Save(); RebuildTree(copy); });
                menu.Items.Add(MoveMenu(c, c.ParentId));
                Sep();
                Add("Esporta…", () => Export(c.Id, c.Name));
                Add("Importa qui…", () => Import(c.Id));
                Sep();
                Add("Elimina", () => DeleteCollection(c));
                break;
            default:
                Add("Nuova sessione", () => AddSession(new SessionConfig()));
                Add("Nuova collezione", () => NewCollection(null));
                Sep();
                Add("Esporta tutto…", () => Export(null, "sessioni"));
                Add("Importa…", () => Import(null));
                break;
        }
    }

    private MenuItem MoveMenu(object item, Guid? current)
    {
        var menu = new MenuItem { Header = L.T("Sposta in…") };
        menu.Items.Add(Item(L.T("(radice)"), () => MoveTo(item, null), current != null));
        foreach (var (c, path) in _tree.Collections.Select(c => (c, _tree.PathOf(c.Id))).OrderBy(x => x.Item2, StringComparer.CurrentCultureIgnoreCase))
        {
            var ok = c.Id != current && (item is not SessionCollection moving || _tree.CanMove(moving.Id, c.Id));
            menu.Items.Add(Item(path, () => MoveTo(item, c.Id), ok));
        }
        return menu;
    }

    // TextBlock header: names like "DOB_SDL" must not turn "_" into an access key.
    private static MenuItem Item(string header, Action action, bool enabled = true)
    {
        var mi = new MenuItem { Header = new TextBlock { Text = header }, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(mi, header); // screen readers / UI Automation
        mi.Click += (_, _) => action();
        return mi;
    }

    // ---- export / import ----

    private void Export(Guid? collectionId, string suggestedName)
    {
        var dlg = new SaveFileDialog { Filter = ExportFilter, FileName = suggestedName + ".rlv.json", Title = L.T("Esporta collezione") };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            SessionStore.WriteExport(dlg.FileName, _tree.Export(collectionId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.F("Esportazione non riuscita: {0}", ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import(Guid? targetId)
    {
        var dlg = new OpenFileDialog { Filter = ExportFilter, Title = L.T("Importa collezione") };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var file = SessionStore.ReadExport(dlg.FileName);
            var first = _tree.Import(file, targetId); // validates first: on error the tree is unchanged
            if (targetId is { } t) _expanded.Add(t);
            if (first is SessionCollection c) _expanded.Add(c.Id);
            Save();
            RebuildTree(first);
            MessageBox.Show(this, L.F("Importate {0} sessioni. Le password non sono incluse: inseriscile con Modifica.", file.Sessions.Count),
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.F("Importazione non riuscita: {0}", ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---- drag & drop in the tree ----

    private void SessionTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragItem = Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext switch
        {
            SessionNode n => n.Session,
            CollectionNode n => n.Collection,
            _ => null,
        };
    }

    private void SessionTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed || !DragDistanceReached(e)) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(SessionTreeView, new DataObject(TreeDragFormat, item), DragDropEffects.Move);
    }

    /// Drop target: a collection (inside it) or empty space (root). Onto a session: not allowed.
    private bool TryGetDropTarget(DragEventArgs e, out object item, out Guid? parentId)
    {
        item = e.Data.GetData(TreeDragFormat)!;
        parentId = null;
        if (item == null) return false;
        switch (Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext)
        {
            case SessionNode: return false;
            case CollectionNode n: parentId = n.Collection.Id; break;
        }
        return item is not SessionCollection c || _tree.CanMove(c.Id, parentId);
    }

    private void SessionTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDropTarget(e, out _, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void SessionTree_Drop(object sender, DragEventArgs e)
    {
        if (TryGetDropTarget(e, out var item, out var parentId)) MoveTo(item, parentId);
        e.Handled = true;
    }

    private bool DragDistanceReached(MouseEventArgs e)
    {
        var d = e.GetPosition(this) - _dragStart;
        return Math.Abs(d.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(d.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private static T? Ancestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null && d is not T)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    // ---- active sessions ----

    private void Open(SessionConfig config)
    {
        DateTime? date = null;
        if (DatePath.Has(config.FilePath))
        {
            var dlg = new DateDialog(config.WithCredential(_tree.Credentials)) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            date = dlg.SelectedDate;
        }
        Open(config, date);
    }

    private void Open(SessionConfig config, DateTime? date)
    {
        var session = new ActiveSession(config.WithCredential(_tree.Credentials), date); // clone: later edits don't touch a running session
        var view = new LogView(session);
        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 0), Fill = view.StatusBrush };
        var close = new Button { Content = "✕", Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        var title = new TextBlock { Text = session.DisplayName };
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)), CornerRadius = new CornerRadius(7), Padding = new Thickness(5, 0, 5, 0),
            Margin = new Thickness(6, 0, 0, 0), Visibility = Visibility.Collapsed, 
            Child = new TextBlock { Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold },
        };
        badge.SetBinding(ToolTipProperty, L.Bind("Errori arrivati mentre la scheda non era visibile"));
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(dot);
        header.Children.Add(title);
        header.Children.Add(badge);
        header.Children.Add(close);
        var tab = new TabItem { Header = header, Tag = view, ToolTip = session.Path, AllowDrop = true };
        view.StatusChanged += () =>
        {
            dot.Fill = view.StatusBrush;
            title.Text = session.DisplayName; // changes when a "today" session rolls over to the next day
            tab.ToolTip = session.Path;
            ((TextBlock)badge.Child).Text = view.UnreadErrors.ToString();
            badge.Visibility = view.UnreadErrors > 0 ? Visibility.Visible : Visibility.Collapsed;
            System.Windows.Automation.AutomationProperties.SetName(tab, view.UnreadErrors > 0 ? L.F("{0} ({1} errori)", session.DisplayName, view.UnreadErrors) : session.DisplayName);
        };
        System.Windows.Automation.AutomationProperties.SetName(tab, session.DisplayName);
        view.ErrorsArrived += _ => { if (!IsActive) Flash(); };
        close.Click += (_, _) => CloseTab(tab);
        tab.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) CloseTab(tab); };

        // Reorder by dragging the header onto another tab. The LogView stays in Tag, so the session is untouched.
        tab.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragTab = Ancestor<Button>(e.OriginalSource as DependencyObject) == null ? tab : null;
            _dragStart = e.GetPosition(this);
        };
        tab.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetData(TabDragFormat) is TabItem ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        tab.Drop += (_, e) =>
        {
            if (e.Data.GetData(TabDragFormat) is not TabItem src || src == tab) return;
            var index = Tabs.Items.IndexOf(tab);
            Tabs.Items.Remove(src);
            Tabs.Items.Insert(index, src);
            Tabs.SelectedItem = src;
            Relayout();
            e.Handled = true;
        };

        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
        Relayout();
    }

    // On the TabControl, not the TabItem: a fast drag leaves the small header before the first move event.
    private void Tabs_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab is not { } tab || e.LeftButton != MouseButtonState.Pressed || !DragDistanceReached(e)) return;
        _dragTab = null;
        DragDrop.DoDragDrop(tab, new DataObject(TabDragFormat, tab), DragDropEffects.Move);
    }

    private void Tabs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _dragTab = null;

    private void CloseTab(TabItem tab)
    {
        ((LogView)tab.Tag).Close();
        Tabs.Items.Remove(tab);
        Relayout();
    }

    // ---- local files ----

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = L.T("Apri file di log"),
            Filter = L.T("Log e testo (*.log;*.txt)|*.log;*.txt|Tutti i file (*.*)|*.*"),
        };
        if (dlg.ShowDialog(this) == true) OpenFiles(dlg.FileNames);
    }

    /// Opens each file in its own tab (dialog or drag & drop). Folders are ignored; a file already open is just selected.
    private void OpenFiles(IEnumerable<string> paths)
    {
        var files = paths.Where(File.Exists).ToList();
        if (files.Count == 0)
        {
            MessageBox.Show(this, L.T("Nessun file valido"), "Remote Log Viewer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        foreach (var file in files)
        {
            var full = System.IO.Path.GetFullPath(file);
            var existing = Tabs.Items.Cast<TabItem>().FirstOrDefault(t =>
                ((LogView)t.Tag).Session.Config is { IsLocal: true } c && string.Equals(c.FilePath, full, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { Tabs.SelectedItem = existing; continue; }
            try { using var _ = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, L.F("Impossibile aprire «{0}»: {1}", full, ex.Message), "Remote Log Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
                continue;
            }
            Open(LocalConfig(full), null);
        }
    }

    // Files dragged from Explorer. Tunneling (Preview) so the tab and tree handlers, which reject foreign data, never see them;
    // internal drags use their own formats (rlv-tab, rlv-tree-item) and are left alone.
    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        OpenFiles(paths);
    }

    /// Config for a local file; the title is `folder\name` when another local tab already has that file name.
    private SessionConfig LocalConfig(string path)
    {
        var config = SessionConfig.Local(path);
        var taken = Tabs.Items.Cast<TabItem>().Any(t => ((LogView)t.Tag).Session.Config is { IsLocal: true } c && c.Name == config.Name);
        if (taken && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(config.FilePath)) is { Length: > 0 } dir)
            config.Name = System.IO.Path.Combine(dir, config.Name);
        return config;
    }

    public static readonly RoutedCommand Search = new();
    private SearchWindow? _search;

    private void Search_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_search != null)
        {
            if (_search.WindowState == WindowState.Minimized) _search.WindowState = WindowState.Normal;
            _search.Activate();
            return;
        }
        _search = new SearchWindow(() => Tabs.Items.Cast<TabItem>().Select(t => (LogView)t.Tag).ToList(), RevealLine) { Owner = this };
        _search.Closed += (_, _) => _search = null;
        _search.Show();
    }

    private RevealResult RevealLine(LogView view, LogLine line)
    {
        var tab = Tabs.Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == view);
        if (tab == null) return RevealResult.NotFound; // session closed since the search
        var result = view.Reveal(line);
        if (result != RevealResult.NotFound && SideBySideButton.IsChecked != true) Tabs.SelectedItem = tab;
        return result;
    }

    private void Credentials_Click(object sender, RoutedEventArgs e) => new CredentialsWindow(_tree, Save) { Owner = this }.ShowDialog();

    private void SideBySide_Click(object sender, RoutedEventArgs e) => Relayout();

    // Tabs are the source of truth (also for order); side-by-side mode re-parents the same LogViews into the grid.
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
            var title = new TextBlock { Text = v.Session.DisplayName, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 2, 4, 2) };
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

    // ---- workspace: tabs reopened at the next start ----

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var ws = _tree.Workspace;
        foreach (var t in ws.Tabs)
        {
            if (t.LocalPath != null)
            {
                if (File.Exists(t.LocalPath)) Open(LocalConfig(t.LocalPath), null); // gone since last time: skipped silently
                continue;
            }
            if (_tree.Sessions.FirstOrDefault(s => s.Id == t.SessionId) is not { } config) continue; // deleted meanwhile
            DateTime? date = !DatePath.Has(config.FilePath) ? null : t.FollowToday ? DateTime.Today : t.Date ?? DateTime.Today;
            Open(config, date);
        }
        SideBySideButton.IsChecked = ws.SideBySide;
        if (ws.Selected >= 0 && ws.Selected < Tabs.Items.Count) Tabs.SelectedIndex = ws.Selected;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var views = Tabs.Items.Cast<TabItem>().Select(t => (LogView)t.Tag).ToList();
        _tree.Workspace = new Workspace
        {
            Tabs = views.Select(v => v.Session.Config.IsLocal
                ? new OpenTab { LocalPath = v.Session.Config.FilePath }
                : new OpenTab { SessionId = v.Session.Config.Id, Date = v.Session.Date, FollowToday = v.Session.FollowToday }).ToList(),
            Selected = Tabs.SelectedIndex,
            SideBySide = SideBySideButton.IsChecked == true,
        };
        try { Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // closing must never fail
        foreach (var v in views) v.Close();
    }

    // ---- alerts ----

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO info);

    /// Taskbar button flashes until the window comes back to the foreground.
    private void Flash()
    {
        const uint FLASHW_ALL = 3, FLASHW_TIMERNOFG = 12;
        var info = new FLASHWINFO { hwnd = new WindowInteropHelper(this).Handle, dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG };
        info.cbSize = (uint)Marshal.SizeOf(info);
        FlashWindowEx(ref info);
    }

    // ---- user guide ----

    private void Help_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteLogViewer", L.GuideFile);
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var res = asm.GetManifestResourceStream(L.GuideResource)!;
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, new StreamReader(res).ReadToEnd().Replace("%VERSION%", version));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L.F("Impossibile aprire la guida ({0}).\nFile: {1}", ex.Message, path), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

/// Tree view nodes, rebuilt from SessionTree after every change.
public class CollectionNode(SessionCollection collection, HashSet<Guid> expanded)
{
    public SessionCollection Collection => collection;
    public string Name => collection.Name;
    public List<object> Children { get; } = [];
    public bool IsSelected { get; set; }
    public bool IsExpanded
    {
        get => expanded.Contains(collection.Id);
        set { if (value) expanded.Add(collection.Id); else expanded.Remove(collection.Id); }
    }
}

public class SessionNode(SessionConfig session)
{
    public SessionConfig Session => session;
    public string Name => session.Name;
    public string FullPath => session.FullPath;
    public bool IsSelected { get; set; }
    public bool IsExpanded { get; set; }
}
