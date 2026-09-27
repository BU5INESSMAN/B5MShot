using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using B5MShot.App.Models;
using B5MShot.App.Services;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace B5MShot.App.Views;

public partial class PreviewWindow
{
    private readonly HudPreferences _hudPreferences;
    private System.Windows.Forms.Screen _hudMonitor = null!;
    private bool _desktopHud;

    private void ConfigureHudOrientation(bool desktop)
    {
        _desktopHud = desktop;
        _hudMonitor = ScreenPlacement.HudMonitor(_hudPreferences);
        var vertical = _hudPreferences.IsVertical;
        HudActions.Orientation = ToolPanel.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        foreach (var button in ToolPanel.Children.OfType<ButtonBase>().Concat(HudActions.Children.OfType<ButtonBase>()))
            button.Margin = vertical ? new Thickness(0,2,0,2) : new Thickness(2,0,2,0);
        foreach (var separator in HudActions.Children.OfType<Border>())
        {
            separator.Width = vertical ? 22 : 1;
            separator.Height = vertical ? 1 : 22;
            separator.Margin = vertical ? new Thickness(0,8,0,8) : new Thickness(8,0,8,0);
        }
        ToolIndicator.X = vertical ? 0 : 2;
        ToolIndicator.Y = vertical ? 2 : 0;
        HudBar.Padding = vertical ? new Thickness(10,14,10,14) : new Thickness(14,10,14,10);
        HudBar.Edge = PaletteClip.Edge = _hudPreferences.Edge;
        HudBar.CornerRadius = PaletteClip.CornerRadius = ScreenPlacement.EdgeCorners(_hudPreferences.Edge);
        HudBar.CollapsedWidth = vertical ? 10 : 280;
        HudBar.CollapsedHeight = vertical ? 280 : 10;
        PaletteClip.CollapsedWidth = vertical ? 0 : 130;
        PaletteClip.CollapsedHeight = vertical ? 130 : 0;
        if (vertical)
        {
            HandoffHint.Orientation = Orientation.Vertical;
            HandoffHint.Width = 52;
            ((System.Windows.Controls.Image)HandoffHint.Children[0]).Margin = new Thickness(0,0,0,10);
            var hint = (TextBlock)HandoffHint.Children[1];
            hint.Text = "Выделите\nобласть\n\nEsc\nотмена";
            hint.FontSize = 11; hint.TextAlignment = TextAlignment.Center;
        }
        if (!desktop) EditorStage.Margin = _hudPreferences.Edge switch
        {
            HudEdge.Bottom => new Thickness(24,75,24,90),
            HudEdge.Left => new Thickness(90,24,24,75),
            HudEdge.Right => new Thickness(24,24,90,75),
            _ => new Thickness(24,90,24,75)
        };
        Root.SizeChanged += (_, _) => PositionEditorHud();
    }

    private void PositionEditorHud()
    {
        if (!IsLoaded || Root.ActualWidth <= 0 || Root.ActualHeight <= 0) return;
        var bounds = _desktopHud ? ScreenPlacement.MonitorBounds(this, _hudMonitor) : new Rect(Root.RenderSize);
        HudBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = HudBar.DesiredSize;
        HudMain.Width = HudHost.Width = size.Width;
        HudMain.Height = HudHost.Height = size.Height;
        // Fit compact displays/high scaling without losing any actions beyond the edge.
        var scale = Math.Min(1, Math.Min((bounds.Width-16)/size.Width, (bounds.Height-16)/size.Height));
        scale = Math.Max(.1, scale);
        HudHost.RenderTransform = new ScaleTransform(scale,scale);
        var shown = new Size(size.Width*scale,size.Height*scale);
        ScreenPlacement.PlaceHud(HudHost,bounds,shown,_hudPreferences.Edge);
        var origin = ScreenPlacement.EdgeOrigin(bounds,shown,_hudPreferences.Edge);
        PaletteClip.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        var palette = PaletteClip.DesiredSize;
        var x = _hudPreferences.Edge switch { HudEdge.Left => size.Width, HudEdge.Right => -palette.Width, _ => (size.Width-palette.Width)/2 };
        var y = _hudPreferences.Edge switch { HudEdge.Top => size.Height, HudEdge.Bottom => -palette.Height, _ => (size.Height-palette.Height)/2 };
        x = Math.Clamp(x,(bounds.Left-origin.X)/scale,Math.Max((bounds.Left-origin.X)/scale,(bounds.Right-origin.X)/scale-palette.Width));
        y = Math.Clamp(y,(bounds.Top-origin.Y)/scale,Math.Max((bounds.Top-origin.Y)/scale,(bounds.Bottom-origin.Y)/scale-palette.Height));
        Canvas.SetLeft(PaletteClip,x); Canvas.SetTop(PaletteClip,y);
        StatusPanel.MaxWidth = Math.Min(650,Math.Max(80,bounds.Width-32));
        StatusPanel.Margin = new Thickness(0);
        StatusPanel.Measure(new Size(StatusPanel.MaxWidth,double.PositiveInfinity));
        var statusSize = StatusPanel.DesiredSize;
        StatusPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        StatusPanel.VerticalAlignment = VerticalAlignment.Top;
        StatusPanel.Margin = new Thickness(bounds.Left+(bounds.Width-statusSize.Width)/2,
            _hudPreferences.Edge == HudEdge.Bottom ? bounds.Top+12 : Math.Max(bounds.Top,bounds.Bottom-statusSize.Height-12),0,0);
    }
}
