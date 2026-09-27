using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;

namespace B5MShot.App.Controls;

/// <summary>Dual-contrast selection chrome. Always outside the image's export tree.</summary>
public sealed class SelectionOutline : FrameworkElement
{
    private static Pen Pen(string color, double thickness)
    {
        var pen = new Pen((SolidColorBrush)new BrushConverter().ConvertFromString(color)!, thickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze(); return pen;
    }
    private static readonly Pen Shadow = Pen("#D9101722", 5);
    private static readonly Pen Edge = Pen("#FFFFFFFF", 1);
    private static readonly Pen CornerShadow = Pen("#F0101722", 6);
    private static readonly Pen Corner = Pen("#FF82C6FF", 3);
    public bool ShowHandles { get; set; }
    public SelectionOutline() { IsHitTestVisible = false; SnapsToDevicePixels = true; }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 2 || ActualHeight < 2) return;
        var rect = new Rect(.5, .5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
        dc.DrawRectangle(null, Shadow, rect); dc.DrawRectangle(null, Edge, rect);
        var leg = Math.Min(20, Math.Min(rect.Width, rect.Height) / 3);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            foreach (var (x, y, sx, sy) in new[] { (rect.Left, rect.Top, 1, 1), (rect.Right, rect.Top, -1, 1), (rect.Left, rect.Bottom, 1, -1), (rect.Right, rect.Bottom, -1, -1) })
            {
                path.BeginFigure(new Point(x, y + sy * leg), false, false);
                path.LineTo(new Point(x, y), true, false); path.LineTo(new Point(x + sx * leg, y), true, false);
            }
        }
        geometry.Freeze(); dc.DrawGeometry(null, CornerShadow, geometry); dc.DrawGeometry(null, Corner, geometry);
        if (ShowHandles)
        {
            foreach (var point in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight,
                new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Left + rect.Width / 2, rect.Bottom),
                new Point(rect.Left, rect.Top + rect.Height / 2), new Point(rect.Right, rect.Top + rect.Height / 2) })
            {
                var handle = new Rect(point.X - 4, point.Y - 4, 8, 8);
                dc.DrawRoundedRectangle(null, Shadow, handle, 2, 2);
                dc.DrawRoundedRectangle(System.Windows.Media.Brushes.White, Edge, handle, 2, 2);
            }
        }
    }
}
