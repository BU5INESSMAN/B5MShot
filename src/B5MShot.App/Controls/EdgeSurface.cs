using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using B5MShot.App.Models;

namespace B5MShot.App.Controls;

/// <summary>Reveal an edge-attached surface without rotating its content.</summary>
public sealed class EdgeSurface : Border
{
    public static readonly DependencyProperty RevealProperty = DependencyProperty.Register(nameof(Reveal), typeof(double), typeof(EdgeSurface), new PropertyMetadata(1d, Changed));
    public double Reveal { get => (double)GetValue(RevealProperty); set => SetValue(RevealProperty, value); }
    public double CollapsedWidth { get; set; } = 180;
    public double CollapsedHeight { get; set; } = 0;
    public HudEdge Edge { get; set; } = HudEdge.Top;
    private readonly RectangleGeometry _clip = new();
    public EdgeSurface() { Clip = _clip; SizeChanged += (_, _) => UpdateClip(); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EdgeSurface)d).UpdateClip();
    private void UpdateClip()
    {
        var progress = Math.Clamp(Reveal, 0, 1);
        var width = Math.Min(ActualWidth, CollapsedWidth) + (ActualWidth - Math.Min(ActualWidth, CollapsedWidth)) * progress;
        var height = CollapsedHeight + (ActualHeight - CollapsedHeight) * progress;
        var radius = 12 + 14 * progress;
        _clip.RadiusX = _clip.RadiusY = radius;
        _clip.Rect = width <= 0 || height <= 0 ? Rect.Empty : Edge switch
        {
            HudEdge.Bottom => new Rect((ActualWidth-width)/2, ActualHeight-height, width, height+radius),
            HudEdge.Left => new Rect(-radius, (ActualHeight-height)/2, width+radius, height),
            HudEdge.Right => new Rect(ActualWidth-width, (ActualHeight-height)/2, width+radius, height),
            _ => new Rect((ActualWidth-width)/2, -radius, width, height+radius)
        };
    }
}
