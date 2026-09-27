using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using B5MShot.App.Services;
using B5MShot.App.Models;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfTextBox = System.Windows.Controls.TextBox;
using Cursors = System.Windows.Input.Cursors;

namespace B5MShot.App.Views;

public partial class PreviewWindow : Window
{
    private enum EditorTool { Pen, Line, Arrow, Rectangle, Text, Blur, Move }

    private BitmapSource _image;
    private readonly UploadService _uploadService;
    private readonly List<List<UIElement>> _history = [];
    private EditorTool _currentTool = EditorTool.Pen;
    private WpfColor _currentColor = WpfColor.FromRgb(237, 74, 90);
    private Point _start;
    private bool _drawing;
    private List<UIElement>? _activeBatch;
    private Polyline? _activePolyline;
    private Line? _activeLine;
    private Polygon? _activeArrowHead;
    private WpfRectangle? _activeRectangle;
    private WpfTextBox? _activeTextBox;
    private string? _uploadedUrl;
    private bool _busy;
    private readonly CancellationTokenSource _lifetime = new();

    public PreviewWindow(BitmapSource image, UploadService uploadService, BitmapSource? desktop = null, Rect? region = null, HudPreferences? hudPreferences = null)
    {
        InitializeComponent();
        _image = image;
        _uploadService = uploadService;
        _hudPreferences = hudPreferences ?? new();
        PreviewImage.Source = image;
        ImageCanvas.Width = image.PixelWidth;
        ImageCanvas.Height = image.PixelHeight;
        AnnotationCanvas.Width = image.PixelWidth;
        AnnotationCanvas.Height = image.PixelHeight;
        ImageSizeText.Text = $"{image.PixelWidth} × {image.PixelHeight} px";
        ConfigureSelectionEditing(desktop, region);
        ConfigureHud(desktop, region);
        if (desktop is not null && region is not null) ToolButton_Click(MoveToolButton, new RoutedEventArgs());
        Closed += (_, _) => _lifetime.Cancel();
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton selected || selected.Tag is not string toolName || !Enum.TryParse(toolName, out EditorTool tool))
        {
            return;
        }

        CommitActiveText();
        foreach (var child in ToolPanel.Children.OfType<ToggleButton>())
        {
            child.IsChecked = ReferenceEquals(child, selected);
        }

        _currentTool = tool;
        Animate(ToolIndicator, _hudPreferences.IsVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty,
            _hudPreferences.IsVertical ? ToolIndicator.Y : ToolIndicator.X,
            ToolPanel.Children.IndexOf(selected) * 44 + 2, B5MShot.App.Controls.Motion.Settle);
        AnnotationCanvas.Cursor = tool == EditorTool.Move ? Cursors.SizeAll : tool == EditorTool.Text ? Cursors.IBeam : Cursors.Cross;
        StatusText.Text = tool switch
        {
            EditorTool.Pen => "Рисуйте свободной линией",
            EditorTool.Line => "Протяните прямую линию",
            EditorTool.Arrow => "Протяните стрелку",
            EditorTool.Rectangle => "Выделите область рамкой",
            EditorTool.Text => "Щёлкните по снимку и введите текст",
            EditorTool.Blur => "Выделите приватную область для мозаики",
            EditorTool.Move => "Перетащите область · края и углы — размер · стрелки — сдвиг · Shift+стрелки — размер",
            _ => string.Empty
        };
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string colorText })
        {
            _currentColor = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(colorText);
            SetPaletteColor(_currentColor);
        }
    }

    private void AnnotationCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy || _closing || _currentTool == EditorTool.Move) return;
        InvalidatePublishedImage();
        CommitActiveText();
        _start = e.GetPosition(AnnotationCanvas);

        if (_currentTool == EditorTool.Text)
        {
            StartTextInput(_start);
            return;
        }

        _drawing = true;
        _activeBatch = [];
        var brush = new SolidColorBrush(_currentColor);
        var thickness = StrokeSlider.Value;

        switch (_currentTool)
        {
            case EditorTool.Pen:
                _activePolyline = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                _activePolyline.Points.Add(_start);
                AddActiveElement(_activePolyline);
                break;
            case EditorTool.Line:
            case EditorTool.Arrow:
                _activeLine = new Line
                {
                    X1 = _start.X,
                    Y1 = _start.Y,
                    X2 = _start.X,
                    Y2 = _start.Y,
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                AddActiveElement(_activeLine);
                if (_currentTool == EditorTool.Arrow)
                {
                    _activeArrowHead = new Polygon { Fill = brush };
                    AddActiveElement(_activeArrowHead);
                }
                break;
            case EditorTool.Rectangle:
            case EditorTool.Blur:
                _activeRectangle = new WpfRectangle
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    StrokeDashArray = _currentTool == EditorTool.Blur ? new DoubleCollection([6, 4]) : null
                };
                AddActiveElement(_activeRectangle);
                break;
        }

        AnnotationCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        var current = ClampPoint(e.GetPosition(AnnotationCanvas));
        if (_activePolyline is not null)
        {
            _activePolyline.Points.Add(current);
        }

        if (_activeLine is not null)
        {
            _activeLine.X2 = current.X;
            _activeLine.Y2 = current.Y;
            if (_activeArrowHead is not null)
            {
                UpdateArrowHead(_start, current, _activeArrowHead, StrokeSlider.Value);
            }
        }

        if (_activeRectangle is not null)
        {
            var rect = MakeRect(_start, current);
            Canvas.SetLeft(_activeRectangle, rect.X);
            Canvas.SetTop(_activeRectangle, rect.Y);
            _activeRectangle.Width = rect.Width;
            _activeRectangle.Height = rect.Height;
        }
    }

    private void AnnotationCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        var end = ClampPoint(e.GetPosition(AnnotationCanvas));
        _drawing = false;
        AnnotationCanvas.ReleaseMouseCapture();

        if (_currentTool == EditorTool.Blur && _activeRectangle is not null)
        {
            AnnotationCanvas.Children.Remove(_activeRectangle);
            _activeBatch?.Clear();
            var rect = MakeRect(_start, end);
            var mosaic = CreateMosaic(rect);
            if (mosaic is not null)
            {
                AddActiveElement(mosaic);
            }
        }

        if (_activeBatch is { Count: > 0 })
        {
            _history.Add(_activeBatch);
        }

        ResetActiveDrawing();
    }

    private void StartTextInput(Point point)
    {
        var textBox = new WpfTextBox
        {
            Width = 240,
            MinHeight = 34,
            FontSize = Math.Max(16, StrokeSlider.Value * 3.2),
            Foreground = new SolidColorBrush(_currentColor),
            Background = new SolidColorBrush(WpfColor.FromArgb(210, 22, 27, 34)),
            BorderBrush = new SolidColorBrush(_currentColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3, 6, 3),
            AcceptsReturn = false
        };
        Canvas.SetLeft(textBox, Math.Min(point.X, Math.Max(0, AnnotationCanvas.Width - textBox.Width)));
        Canvas.SetTop(textBox, Math.Min(point.Y, Math.Max(0, AnnotationCanvas.Height - 40)));
        textBox.KeyDown += ActiveTextBox_KeyDown;
        textBox.LostKeyboardFocus += (_, _) => CommitActiveText();
        AnnotationCanvas.Children.Add(textBox);
        _activeTextBox = textBox;
        textBox.Focus();
    }

    private void ActiveTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitActiveText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelActiveText();
            e.Handled = true;
        }
    }

    private void CommitActiveText()
    {
        if (_activeTextBox is null)
        {
            return;
        }

        var textBox = _activeTextBox;
        _activeTextBox = null;
        var text = textBox.Text.Trim();
        var left = Canvas.GetLeft(textBox);
        var top = Canvas.GetTop(textBox);
        AnnotationCanvas.Children.Remove(textBox);
        if (text.Length == 0)
        {
            return;
        }

        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = textBox.FontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBox.Foreground,
            Background = new SolidColorBrush(WpfColor.FromArgb(115, 10, 12, 16)),
            Padding = new Thickness(4, 2, 4, 2)
        };
        Canvas.SetLeft(textBlock, left);
        Canvas.SetTop(textBlock, top);
        AnnotationCanvas.Children.Add(textBlock);
        _history.Add([textBlock]);
    }

    private void CancelActiveText()
    {
        if (_activeTextBox is not null)
        {
            AnnotationCanvas.Children.Remove(_activeTextBox);
            _activeTextBox = null;
        }
    }

    private WpfImage? CreateMosaic(Rect rect)
    {
        var x = Math.Clamp((int)Math.Round(rect.X), 0, _image.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Round(rect.Y), 0, _image.PixelHeight - 1);
        var width = Math.Clamp((int)Math.Round(rect.Width), 1, _image.PixelWidth - x);
        var height = Math.Clamp((int)Math.Round(rect.Height), 1, _image.PixelHeight - y);
        if (width < 3 || height < 3)
        {
            return null;
        }

        var crop = new CroppedBitmap(_image, new Int32Rect(x, y, width, height));
        var scale = Math.Min(1, 14d / Math.Max(width, height));
        var pixelated = new TransformedBitmap(crop, new ScaleTransform(scale, scale));
        pixelated.Freeze();
        var image = new WpfImage
        {
            Source = pixelated,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);
        return image;
    }

    private void AddActiveElement(UIElement element)
    {
        AnnotationCanvas.Children.Add(element);
        _activeBatch?.Add(element);
    }

    private void ResetActiveDrawing()
    {
        _activeBatch = null;
        _activePolyline = null;
        _activeLine = null;
        _activeArrowHead = null;
        _activeRectangle = null;
    }

    private static void UpdateArrowHead(Point start, Point end, Polygon polygon, double thickness)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var size = Math.Max(12, thickness * 3.5);
        var spread = Math.PI / 7;
        polygon.Points =
        [
            end,
            new Point(end.X - size * Math.Cos(angle - spread), end.Y - size * Math.Sin(angle - spread)),
            new Point(end.X - size * Math.Cos(angle + spread), end.Y - size * Math.Sin(angle + spread))
        ];
    }

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, AnnotationCanvas.Width),
        Math.Clamp(point.Y, 0, AnnotationCanvas.Height));

    private static Rect MakeRect(Point first, Point second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(second.X - first.X),
        Math.Abs(second.Y - first.Y));

    private void UndoButton_Click(object sender, RoutedEventArgs e) => Undo();

    private void Undo()
    {
        CancelActiveText();
        if (_history.Count == 0)
        {
            return;
        }
        InvalidatePublishedImage();

        var last = _history[^1];
        foreach (var element in last)
        {
            AnnotationCanvas.Children.Remove(element);
        }
        _history.RemoveAt(_history.Count - 1);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        InvalidatePublishedImage();
        CancelActiveText();
        AnnotationCanvas.Children.Clear();
        _history.Clear();
    }

    private BitmapSource RenderFinalImage()
    {
        CommitActiveText();
        ImageCanvas.UpdateLayout();
        var result = new RenderTargetBitmap(_image.PixelWidth, _image.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        result.Render(ImageCanvas);
        result.Freeze();
        return result;
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (await CopyImageAsync()) Finish("Снимок скопирован");
    }

    private async Task<bool> CopyImageAsync()
    {
        if (_busy) return false;
        SetBusy(true);
        try
        {
            await ClipboardService.SetImageAsync(RenderFinalImage());
            StatusText.Text = "Изображение с правками скопировано";
            return true;
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Copying screenshot");
            StatusText.Text = "Буфер обмена занят. Попробуйте ещё раз.";
            return false;
        }
        finally { SetBusy(false); }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveImageAsync()) Finish("Снимок сохранён");
    }

    private async Task<bool> SaveImageAsync()
    {
        if (_busy) return false;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            DefaultExt = ".png",
            FileName = $"B5MShot-{DateTime.Now:yyyy-MM-dd-HHmmss}.png"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return false;
        }

        try
        {
            SetBusy(true);
            var image = RenderFinalImage();
            await Task.Run(() =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
            });
            StatusText.Text = "Снимок с правками сохранён";
            return true;
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Saving screenshot");
            StatusText.Text = "Не удалось сохранить файл. Проверьте доступ к папке.";
            return false;
        }
        finally { SetBusy(false); }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        StatusText.Text = "Загружаю в ваше облако…";

        try
        {
            var result = await _uploadService.UploadAsync(RenderFinalImage(), _lifetime.Token);
            _uploadedUrl = result.Url;
            LinkText.Text = result.Url;
            LinkText.Visibility = Visibility.Visible;
            try
            {
                await ClipboardService.SetTextAsync(result.Url);
                StatusText.Text = "Ссылка скопирована в буфер обмена";
            }
            catch (Exception clipboardException)
            {
                ErrorLogService.Write(clipboardException, "Copying uploaded URL");
                StatusText.Text = "Снимок загружен, но буфер обмена занят";
                return; // Keep the link accessible until the user closes the editor.
            }
            Finish(StatusText.Text, result.Url);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Uploading screenshot");
            StatusText.Text = exception.Message;
        }
        finally { SetBusy(false); UploadButton.IsEnabled = _uploadedUrl is null; }
    }

    private void LinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_uploadedUrl is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_uploadedUrl) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                ErrorLogService.Write(exception, "Opening uploaded screenshot");
                StatusText.Text = "Не удалось открыть ссылку в браузере";
            }
        }
    }

    private void NewCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        ((App)System.Windows.Application.Current).BeginCaptureArea();
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_selectionDragging)
        {
            if (e.Key == Key.Escape) EndSelectionDrag(cancel: true);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_paletteOpen) SetPaletteOpen(false);
            else if (_activeTextBox is not null) CancelActiveText();
            else if (!_busy) Close();
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;
        if (_busy || _closing) { e.Handled = true; return; }
        if (HandleSelectionKey(e)) return;
        if (e.IsRepeat || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.S)
        {
            e.Handled = true;
            if (await SaveImageAsync())
            {
                Finish("Снимок сохранён");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.C)
        {
            e.Handled = true;
            if (await CopyImageAsync())
            {
                Finish("Снимок скопирован");
            }
            e.Handled = true;
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy || _closing;
        HudActions.IsEnabled = !_busy;
        AnnotationCanvas.IsHitTestVisible = !_busy;
        PaletteContent.IsEnabled = !_busy && _paletteOpen;
    }

    private void InvalidatePublishedImage()
    {
        _uploadedUrl = null;
        LinkText.Visibility = Visibility.Collapsed;
        UploadButton.IsEnabled = true;
    }

    private void Finish(string message, string? url = null)
    {
        if (_lifetime.IsCancellationRequested) return;
        _resultAfterClose = (message, url);
        Close();
    }
}
