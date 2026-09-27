using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using B5MShot.App.Services;
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
    private readonly RectangleGeometry _outer = new();
    private readonly RectangleGeometry _hole = new();
    public Rect SelectedRegion { get; private set; }

    public event EventHandler<BitmapSource>? CaptureFinished;

    public SelectionWindow(BitmapSource screenshot, CaptureService captureService)
    {
        InitializeComponent();
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
            ScreenPlacement.PositionHud(SelectionHud, this);
        };
        SizeChanged += (_, _) => { if (!_selecting) UpdateDimLayer(Rect.Empty); };
    }

    private void CaptureArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(CaptureArea);
        _selecting = true;
        CaptureArea.CaptureMouse();
        SelectionRectangle.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
