using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RemoteLogViewer;

/// Errors-per-minute bars (see ErrorHistogram.Series). One bar per minute, BarWidth px each; as wide as the whole series
/// (the host scrolls it); a click raises MinuteClicked.
public sealed class ErrorStrip : FrameworkElement
{
    public const double BarWidth = 6;

    private DateTime _first;
    private int[] _bars = [];
    private int _max = 1;

    public event Action<DateTime>? MinuteClicked;

    public ErrorStrip()
    {
        Height = 36;
        ToolTipService.SetInitialShowDelay(this, 0);
    }

    public void Update(DateTime first, int[] counts)
    {
        _first = first;
        _bars = counts;
        _max = Math.Max(1, counts.DefaultIfEmpty(0).Max());
        Width = counts.Length * BarWidth;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight)); // hit-testable background
        var red = Brushes.Firebrick;
        for (var i = 0; i < _bars.Length; i++)
        {
            if (_bars[i] == 0) continue;
            var h = Math.Max(2, (ActualHeight - 2) * _bars[i] / _max);
            dc.DrawRectangle(red, null, new Rect(i * BarWidth, ActualHeight - h, BarWidth - 1, h));
        }
    }

    private int BarAt(MouseEventArgs e)
    {
        var i = (int)(e.GetPosition(this).X / BarWidth);
        return i >= 0 && i < _bars.Length ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var i = BarAt(e);
        ToolTip = i < 0 ? null : L.F("{0:yyyy-MM-dd HH:mm} — {1} errori", _first.AddMinutes(i), _bars[i]);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var i = BarAt(e);
        if (i >= 0) MinuteClicked?.Invoke(_first.AddMinutes(i));
    }
}
