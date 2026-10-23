using System.Windows;
using System.Windows.Controls;

namespace RemoteLogViewer;

/// Picks which open session tabs go into the merged view (all ticked by default, at least two).
public partial class MergeDialog : Window
{
    private readonly List<CheckBox> _boxes = [];

    private MergeDialog(IReadOnlyList<LogView> views)
    {
        InitializeComponent();
        foreach (var v in views)
        {
            var box = new CheckBox { Content = v.Session.DisplayName, Tag = v, IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
            box.Checked += (_, _) => Update();
            box.Unchecked += (_, _) => Update();
            _boxes.Add(box);
            List.Children.Add(box);
        }
        Update();
    }

    /// The chosen views in tab order, or null if cancelled.
    public static List<LogView>? Pick(Window owner, IReadOnlyList<LogView> views)
    {
        var dlg = new MergeDialog(views) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg._boxes.Where(b => b.IsChecked == true).Select(b => (LogView)b.Tag).ToList() : null;
    }

    private void Update()
    {
        var enough = _boxes.Count(b => b.IsChecked == true) >= 2;
        OkButton.IsEnabled = enough;
        Hint.Visibility = enough ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
