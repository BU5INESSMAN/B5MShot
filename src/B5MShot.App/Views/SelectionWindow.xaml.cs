using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using B5MShot.App.Services;
using B5MShot.App.Controls;
using B5MShot.App.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace B5MShot.App.Views;

public partial class SelectionWindow : Window
{
    private readonly BitmapSource _screenshot;
    private readonly CaptureService _captureService;
    private Point _start;
    private bool _selecting;
    private bool _canceling;
    private readonly RectangleGeometry _outer = new();
    private readonly RectangleGeometry _hole = new();
    public Rect SelectedRegion { get; private set; }
    public string HudMonitorDeviceName { get; }

    public event EventHandler<BitmapSource>? CaptureFinished;

    public SelectionWindow(BitmapSource screenshot, CaptureService captureService, HudPreferences? preferences = null)
    {
        InitializeComponent();
        preferences ??= new();
        var monitor = ScreenPlacement.HudMonitor(preferences);
        HudMonitorDeviceName = monitor.DeviceName;
        SelectionHud.Edge = preferences.Edge;
        SelectionHud.CornerRadius = ScreenPlacement.EdgeCorners(preferences.Edge);
        if (preferences.IsVertical)
        {
            SelectionHud.Width = 60; SelectionHud.Height = 200;
            SelectionHud.Padding = new Thickness(4,12,4,12);
            SelectionHud.CollapsedWidth = 8; SelectionHud.CollapsedHeight = 130;
            HintContent.Orientation = System.Windows.Controls.Orientation.Vertical;
            ((System.Windows.Controls.Image)HintContent.Children[0]).Margin = new Thickness(0,0,0,10);
            var hint = (TextBlock)HintContent.Children[1];
            hint.Text = "Выделите\nобласть\n\nEsc\nотмена"; hint.FontSize = 11; hint.TextAlignment = TextAlignment.Center;
        }
        _screenshot = screenshot;
        _captureService = captureService;
        ScreenshotImage.Source = screenshot;
        var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };
        mask.Children.Add(_outer);
        mask.Children.Add(_hole);
        DimLayer.Data = mask;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        KeyDown += OnKeyDown;
        Loaded += (_, _) =>
        {
            ScreenPlacement.CoverDesktop(this);
            UpdateDimLayer(Rect.Empty);
            ScreenPlacement.PlaceHud(SelectionHud, ScreenPlacement.MonitorBounds(this,monitor), new System.Windows.Size(SelectionHud.Width,SelectionHud.Height),preferences.Edge);
        };
        SizeChanged += (_, _) => { if (!_selecting) UpdateDimLayer(Rect.Empty); };
        ContentRendered += (_, _) =>
        {
            Motion.To(SelectionHud, EdgeSurface.RevealProperty, 1, Motion.Enter, from: 0);
            Motion.Reveal(HintContent, 40);
        };
    }

    private void CaptureArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_canceling) return;
        _start = e.GetPosition(CaptureArea);
        _selecting = true;
        CaptureArea.CaptureMouse();
        SelectionRectangle.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
        Motion.Reveal(SizeBadge, distance: -4);
        UpdateSelection(_start);
    }

    private void CaptureArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_selecting)
        {
            UpdateSelection(e.GetPosition(CaptureArea));
        }
    }

    private void CaptureArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        CaptureArea.ReleaseMouseCapture();
        var selection = GetSelection(e.GetPosition(CaptureArea));
        if (selection.Width < 4 || selection.Height < 4)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            UpdateDimLayer(Rect.Empty);
            return;
        }

        SelectedRegion = new Rect(selection.X / CaptureArea.ActualWidth, selection.Y / CaptureArea.ActualHeight,
            selection.Width / CaptureArea.ActualWidth, selection.Height / CaptureArea.ActualHeight);
        var result = _captureService.Crop(_screenshot, selection, CaptureArea.ActualWidth, CaptureArea.ActualHeight);
        CaptureFinished?.Invoke(this, result);
        Close();
    }

    private void UpdateSelection(Point current)
    {
        var selection = GetSelection(current);
        Canvas.SetLeft(SelectionRectangle, selection.X);
        Canvas.SetTop(SelectionRectangle, selection.Y);
        SelectionRectangle.Width = selection.Width;
        SelectionRectangle.Height = selection.Height;
        Canvas.SetLeft(SizeBadge, selection.X);
        Canvas.SetTop(SizeBadge, Math.Max(4, selection.Y - 32));
        SizeText.Text = $"{Math.Round(selection.Width * _screenshot.PixelWidth / CaptureArea.ActualWidth)} × {Math.Round(selection.Height * _screenshot.PixelHeight / CaptureArea.ActualHeight)}";
        UpdateDimLayer(selection);
    }

    private Rect GetSelection(Point current)
    {
        var x = Math.Max(0, Math.Min(_start.X, current.X));
        var y = Math.Max(0, Math.Min(_start.Y, current.Y));
        var right = Math.Min(CaptureArea.ActualWidth, Math.Max(_start.X, current.X));
        var bottom = Math.Min(CaptureArea.ActualHeight, Math.Max(_start.Y, current.Y));
        return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private void UpdateDimLayer(Rect selection)
    {
        _outer.Rect = new Rect(0, 0, CaptureArea.ActualWidth, CaptureArea.ActualHeight);
        _hole.Rect = selection.IsEmpty ? Rect.Empty : selection;
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_canceling) return;
            _canceling = true;
            _selecting = false;
            CaptureArea.ReleaseMouseCapture();
            CaptureArea.IsHitTestVisible = false;
            SelectionHud.CollapsedHeight = 0;
            if (SelectionHud.Edge is HudEdge.Left or HudEdge.Right) SelectionHud.CollapsedWidth = 0;
            Motion.To(SelectionHud, EdgeSurface.RevealProperty, 0, Motion.Exit, false);
            Motion.To(DimLayer, OpacityProperty, 0, Motion.Exit, false);
            Motion.To(SelectionRectangle, OpacityProperty, 0, 120, false);
            Motion.To(SizeBadge, OpacityProperty, 0, 120, false);
            if (Motion.Enabled) await Task.Delay(Motion.Exit);
            if (!IsVisible) return;
            Close();
        }
    }
}
