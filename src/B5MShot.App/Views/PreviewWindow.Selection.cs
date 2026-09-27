using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using B5MShot.App.Services;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;

namespace B5MShot.App.Views;

public partial class PreviewWindow
{
    private BitmapSource? _selectionSource;
    private Int32Rect _selectionPixels, _selectionDragStart;
    private Point _selectionPointerStart;
    private SelectionEdge _selectionEdge;
    private bool _selectionDragging;

    private void ConfigureSelectionEditing(BitmapSource? desktop, Rect? region)
    {
        if (desktop is null || region is null) { MoveToolButton.Visibility = Visibility.Collapsed; return; }
        _selectionSource = desktop;
        _selectionPixels = new Int32Rect(
            Math.Clamp((int)Math.Round(region.Value.X * desktop.PixelWidth), 0, desktop.PixelWidth - _image.PixelWidth),
            Math.Clamp((int)Math.Round(region.Value.Y * desktop.PixelHeight), 0, desktop.PixelHeight - _image.PixelHeight),
            _image.PixelWidth, _image.PixelHeight);
        SelectionFrame.ShowHandles = true;
        B5MShot.App.Controls.Motion.SetTextReveal(ImageSizeText, false); // Live dimensions must not restart text animations every frame.
        Root.PreviewMouseLeftButtonDown += SelectionMouseDown;
        Root.PreviewMouseMove += SelectionMouseMove;
        Root.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!_selectionDragging) return;
            UpdateSelectionDrag(e.GetPosition(Root));
            EndSelectionDrag(false); e.Handled = true;
        };
        Root.LostMouseCapture += (_, _) => { if (_selectionDragging) EndSelectionDrag(true); };
        Deactivated += (_, _) => { if (_selectionDragging) EndSelectionDrag(true); Mouse.OverrideCursor = null; };
        Closed += (_, _) => Mouse.OverrideCursor = null;
        Root.MouseLeave += (_, _) => { if (!_selectionDragging) Mouse.OverrideCursor = null; };
    }

    private Rect SelectionBounds() => new(
        _selectionPixels.X * Root.ActualWidth / _selectionSource!.PixelWidth,
        _selectionPixels.Y * Root.ActualHeight / _selectionSource.PixelHeight,
        _selectionPixels.Width * Root.ActualWidth / _selectionSource.PixelWidth,
        _selectionPixels.Height * Root.ActualHeight / _selectionSource.PixelHeight);

    private void PositionSelectionImage()
    {
        if (_selectionSource is null) return;
        var bounds = SelectionBounds();
        ImageView.Width = bounds.Width; ImageView.Height = bounds.Height;
        Canvas.SetLeft(ImageView, bounds.X); Canvas.SetTop(ImageView, bounds.Y);
        PositionFrame(bounds);
    }

    private SelectionEdge HitSelection(Point point)
    {
        var bounds = SelectionBounds();
        var hit = bounds; hit.Inflate(8, 8);
        if (!hit.Contains(point)) return SelectionEdge.None;
        var edge = SelectionEdge.None;
        if (Math.Abs(point.X - bounds.Left) <= 8) edge |= SelectionEdge.Left;
        else if (Math.Abs(point.X - bounds.Right) <= 8) edge |= SelectionEdge.Right;
        if (Math.Abs(point.Y - bounds.Top) <= 8) edge |= SelectionEdge.Top;
        else if (Math.Abs(point.Y - bounds.Bottom) <= 8) edge |= SelectionEdge.Bottom;
        if (edge != SelectionEdge.None) return edge;
        return bounds.Contains(point) && (_currentTool == EditorTool.Move || Keyboard.IsKeyDown(Key.Space))
            ? SelectionEdge.Move : SelectionEdge.None;
    }

    private static Cursor SelectionCursor(SelectionEdge edge) => edge switch
    {
        SelectionEdge.Move => Cursors.SizeAll,
        SelectionEdge.Left or SelectionEdge.Right => Cursors.SizeWE,
        SelectionEdge.Top or SelectionEdge.Bottom => Cursors.SizeNS,
        SelectionEdge.Left | SelectionEdge.Top or SelectionEdge.Right | SelectionEdge.Bottom => Cursors.SizeNWSE,
        SelectionEdge.Right | SelectionEdge.Top or SelectionEdge.Left | SelectionEdge.Bottom => Cursors.SizeNESW,
        _ => Cursors.Arrow
    };

    private void SelectionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy || _closing || _drawing || HudHost.IsMouseOver || StatusPanel.IsMouseOver || _activeTextBox?.IsMouseOver == true) return;
        var point = e.GetPosition(Root);
        var edge = HitSelection(point);
        if (edge == SelectionEdge.None) return;
        CommitActiveText();
        if (_paletteOpen) SetPaletteOpen(false);
        Keyboard.Focus(MoveToolButton);
        BeginSelectionDrag(edge, point);
        if (!Root.CaptureMouse()) EndSelectionDrag(true);
        e.Handled = true;
    }

    private void BeginSelectionDrag(SelectionEdge edge, Point point)
    {
        _selectionDragStart = _selectionPixels;
        _selectionPointerStart = point;
        _selectionEdge = edge;
        _selectionDragging = true;
        Mouse.OverrideCursor = SelectionCursor(edge);
    }

    private void SelectionMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionDragging)
        {
            UpdateSelectionDrag(e.GetPosition(Root)); e.Handled = true; return;
        }
        // Override only in the editor; never leave a global cursor behind on close/deactivation.
        if (_busy || _closing || _drawing || HudHost.IsMouseOver || StatusPanel.IsMouseOver)
        { Mouse.OverrideCursor = null; return; }
        var edge = HitSelection(e.GetPosition(Root));
        Mouse.OverrideCursor = edge == SelectionEdge.None ? null : SelectionCursor(edge);
    }

    private void UpdateSelectionDrag(Point point)
    {
        if (!_selectionDragging || _selectionSource is null) return;
        var dx = (int)Math.Round((point.X - _selectionPointerStart.X) * _selectionSource.PixelWidth / Root.ActualWidth);
        var dy = (int)Math.Round((point.Y - _selectionPointerStart.Y) * _selectionSource.PixelHeight / Root.ActualHeight);
        ApplySelection(SelectionGeometry.Change(_selectionDragStart, _selectionEdge, dx, dy,
            _selectionSource.PixelWidth, _selectionSource.PixelHeight));
    }

    private void EndSelectionDrag(bool cancel)
    {
        _selectionDragging = false;
        if (cancel) ApplySelection(_selectionDragStart);
        if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private void ApplySelection(Int32Rect next)
    {
        if (_selectionSource is null || next == _selectionPixels) return;
        // Shift annotations in crop-local pixels so they remain attached to the desktop.
        // Keep off-crop elements: expanding again restores them, including undo batches.
        var dx = _selectionPixels.X - next.X; var dy = _selectionPixels.Y - next.Y;
        foreach (UIElement child in AnnotationCanvas.Children)
        {
            var x = Canvas.GetLeft(child); var y = Canvas.GetTop(child);
            Canvas.SetLeft(child, (double.IsNaN(x) ? 0 : x) + dx);
            Canvas.SetTop(child, (double.IsNaN(y) ? 0 : y) + dy);
        }
        _selectionPixels = next;
        _image = new CroppedBitmap(_selectionSource, next); _image.Freeze();
        PreviewImage.Source = _image;
        ImageCanvas.Width = AnnotationCanvas.Width = next.Width;
        ImageCanvas.Height = AnnotationCanvas.Height = next.Height;
        ImageSizeText.Text = $"{next.Width} × {next.Height} px";
        PositionSelectionImage();
        InvalidatePublishedImage();
    }

    private bool HandleSelectionKey(KeyEventArgs e)
    {
        if (_selectionSource is null) return false;
        if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.None)
        { ToolButton_Click(MoveToolButton, new RoutedEventArgs()); e.Handled = true; return true; }
        if (_currentTool != EditorTool.Move || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return false;
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 10 : 1;
        var dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
        var dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
        var edge = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? SelectionEdge.Right | SelectionEdge.Bottom : SelectionEdge.Move;
        CommitActiveText();
        ApplySelection(SelectionGeometry.Change(_selectionPixels, edge, dx, dy, _selectionSource.PixelWidth, _selectionSource.PixelHeight));
        e.Handled = true; return true;
    }
}
