using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RemoteLogViewer;

/// Errors-per-minute bars (see ErrorHistogram.Window). One bar per minute, BarWidth px each; a click raises MinuteClicked.
public sealed class ErrorStrip : FrameworkElement
{
    public const double BarWidth = 6;

    private (DateTime Minute, int Count)[] _bars = [];
    private int _max = 1;

    public event Action<DateTime>? MinuteClicked;

    public ErrorStrip()
    {
        Height = 36;
        ToolTipService.SetInitialShowDelay(this, 0);
    }

    /// Bars that fit the current width.
    public int MaxBars => (int)(ActualWidth / BarWidth);

    public void Update((DateTime Minute, int Count)[] bars)
    {
        _bars = bars;
        _max = Math.Max(1, bars.Select(b => b.Count).DefaultIfEmpty(0).Max());
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight)); // hit-testable background
        var red = Brushes.Firebrick;
        for (var i = 0; i < _bars.Length; i++)
        {
            if (_bars[i].Count == 0) continue;
            var h = Math.Max(2, (ActualHeight - 2) * _bars[i].Count / _max);
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
        ToolTip = i < 0 ? null : L.F("{0:HH:mm} — {1} errori", _bars[i].Minute, _bars[i].Count);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var i = BarAt(e);
        if (i >= 0) MinuteClicked?.Invoke(_bars[i].Minute);
    }
}
