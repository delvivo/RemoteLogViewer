using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace RemoteLogViewer;

public partial class MainWindow : Window
{
    private const string TreeDragFormat = "rlv-tree-item";
    private const string TabDragFormat = "rlv-tab";
    private const string ExportFilter = "Collezioni Remote Log Viewer (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json";

    private readonly SessionStore _store = new(SessionStore.DefaultPath);
    private readonly SessionTree _tree;
    private readonly HashSet<Guid> _expanded = [];

    private Point _dragStart;
    private object? _dragItem;   // SessionConfig / SessionCollection being dragged in the tree
    private TabItem? _dragTab;

    public MainWindow()
    {
        InitializeComponent();
        _tree = _store.Load();
        RebuildTree();
        if (_store.Warning != null) Loaded += (_, _) => MessageBox.Show(this, _store.Warning, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Save() => _store.Save(_tree);

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
        copy.Name += " (copia)";
        AddSession(copy);
    }

    private void AddSession(SessionConfig config)
    {
        var dlg = new SessionEditWindow(config) { Owner = this };
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
        var dlg = new SessionEditWindow(s) { Owner = this };
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
        if (!Confirm($"Eliminare la sessione \"{s.Name}\"?")) return;
        _tree.Sessions.Remove(s);
        Save();
        RebuildTree();
    }

    private void DeleteCollection(SessionCollection c)
    {
        int sessions = _tree.CountSessions(c.Id), collections = _tree.CountCollections(c.Id);
        var question = sessions + collections == 0
            ? $"Eliminare la collezione \"{c.Name}\"?"
            : $"Eliminare la collezione \"{c.Name}\" con {sessions} sessioni e {collections} sottocollezioni?";
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
        var name = TextDialog.Ask(this, "Nuova collezione", "_Nome", "",
            n => _tree.NameTaken(parentId, n) ? "Esiste già una collezione con questo nome." : null);
        if (name == null) return;
        var c = new SessionCollection { Name = name, ParentId = parentId };
        _tree.Collections.Add(c);
        Save();
        RebuildTree(c);
    }

    private void Rename(SessionCollection c)
    {
        var name = TextDialog.Ask(this, "Rinomina collezione", "_Nome", c.Name,
            n => _tree.NameTaken(c.ParentId, n, c.Id) ? "Esiste già una collezione con questo nome." : null);
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
        void Add(string header, Action action) => menu.Items.Add(Item(header, action));
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
        var menu = new MenuItem { Header = "Sposta in…" };
        menu.Items.Add(Item("(radice)", () => MoveTo(item, null), current != null));
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
        var dlg = new SaveFileDialog { Filter = ExportFilter, FileName = suggestedName + ".rlv.json", Title = "Esporta collezione" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            SessionStore.WriteExport(dlg.FileName, _tree.Export(collectionId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Esportazione non riuscita: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import(Guid? targetId)
    {
        var dlg = new OpenFileDialog { Filter = ExportFilter, Title = "Importa collezione" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var file = SessionStore.ReadExport(dlg.FileName);
            var first = _tree.Import(file, targetId); // validates first: on error the tree is unchanged
            if (targetId is { } t) _expanded.Add(t);
            if (first is SessionCollection c) _expanded.Add(c.Id);
            Save();
            RebuildTree(first);
            MessageBox.Show(this, $"Importate {file.Sessions.Count} sessioni. Le password non sono incluse: inseriscile con Modifica.",
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Importazione non riuscita: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
            var dlg = new DateDialog(config) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            date = dlg.SelectedDate;
        }
        var session = new ActiveSession(config.Clone(), date); // clone: later edits don't touch a running session
        var view = new LogView(session);
        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 0), Fill = view.StatusBrush };
        var close = new Button { Content = "✕", Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        var title = new TextBlock { Text = session.DisplayName };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(dot);
        header.Children.Add(title);
        header.Children.Add(close);
        var tab = new TabItem { Header = header, Tag = view, ToolTip = session.Path, AllowDrop = true };
        view.StatusChanged += () =>
        {
            dot.Fill = view.StatusBrush;
            title.Text = session.DisplayName; // changes when a "today" session rolls over to the next day
            tab.ToolTip = session.Path;
        };
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

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (TabItem t in Tabs.Items) ((LogView)t.Tag).Close();
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
