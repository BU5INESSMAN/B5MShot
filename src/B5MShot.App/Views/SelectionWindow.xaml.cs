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

    public event EventHandler<BitmapSource>? CaptureFinished;

    public SelectionWindow(BitmapSource screenshot, CaptureService captureService)
    {
        InitializeComponent();
        _screenshot = screenshot;
        _captureService = captureService;
        ScreenshotImage.Source = screenshot;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        KeyDown += OnKeyDown;
        Loaded += (_, _) => UpdateDimLayer(Rect.Empty);
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
        SizeText.Text = $"{Math.Round(selection.Width)} × {Math.Round(selection.Height)}";
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
        var full = new RectangleGeometry(new Rect(0, 0, CaptureArea.ActualWidth, CaptureArea.ActualHeight));
        DimLayer.Data = selection.IsEmpty
            ? full
            : new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(selection));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
