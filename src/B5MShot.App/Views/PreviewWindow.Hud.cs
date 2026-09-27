using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using B5MShot.App.Services;
using B5MShot.App.Controls;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace B5MShot.App.Views;

public partial class PreviewWindow
{
    private bool _paletteOpen;
    private bool _updatingColor;
    private bool _closing;
    private bool _allowClose;
    private (string Message, string? Url)? _resultAfterClose;
    private double _hue = 354, _saturation = .688, _value = .929;

    private void ConfigureHud(BitmapSource? desktop, Rect? region)
    {
        ConfigureHudOrientation(desktop is not null);
        void ApplyTheme()
        {
            if (desktop is not null) return;
            var light = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not int theme || theme != 0;
            Background = new SolidColorBrush(light ? Color.FromRgb(233, 239, 246) : Color.FromRgb(22, 31, 48));
        }
        ApplyTheme();
        Closing += async (_, args) =>
        {
            if (_allowClose || !IsLoaded || !Motion.Enabled || System.Windows.Application.Current is App { IsShuttingDown: true }) return;
            args.Cancel = true;
            if (_closing) return;
            _closing = true;
            _lifetime.Cancel();
            SetBusy(true);
            HudBar.CollapsedHeight = 0;
            HudBar.CollapsedWidth = _hudPreferences.IsVertical ? 0 : HudBar.CollapsedWidth;
            Motion.To(PaletteClip, EdgeSurface.RevealProperty, 0, 150, false);
            Motion.To(HudBar, EdgeSurface.RevealProperty, 0, Motion.Exit, false);
            Motion.To(HudActions, OpacityProperty, 0, 120, false);
            Motion.To(StatusPanel, OpacityProperty, 0, 120, false);
            Motion.To(SelectionFrame, OpacityProperty, 0, Motion.Exit, false);
            await Task.Delay(Motion.Exit);
            _allowClose = true;
            if (IsVisible) Close();
        };
        Closed += (_, _) =>
        {
            if (_resultAfterClose is { } result) new ResultHudWindow(result.Message, result.Url).Show();
        };
        Microsoft.Win32.UserPreferenceChangedEventHandler themeChanged = (_, _) =>
        {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke((Action)ApplyTheme);
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += themeChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= themeChanged;
        Loaded += (_, _) =>
        {
            SetPaletteColor(_currentColor);
            UpdateColorDot();
            if (desktop is not null && region is { } selected)
            {
                ScreenPlacement.CoverDesktop(this);
                void PositionImage()
                {
                    ImageView.Width = selected.Width * Root.ActualWidth;
                    ImageView.Height = selected.Height * Root.ActualHeight;
                    Canvas.SetLeft(ImageView, selected.X * Root.ActualWidth);
                    Canvas.SetTop(ImageView, selected.Y * Root.ActualHeight);
                    PositionFrame(new Rect(selected.X * Root.ActualWidth, selected.Y * Root.ActualHeight, ImageView.Width, ImageView.Height));
                    PositionEditorHud();
                }
                SizeChanged += (_, _) => PositionImage();
                PositionImage();
            }
            else
            {
                void PositionImportedFrame()
                {
                    if (ImageView.ActualWidth <= 0) return;
                    PositionFrame(ImageView.TransformToAncestor(Root).TransformBounds(new Rect(ImageView.RenderSize)));
                }
                ImageView.SizeChanged += (_, _) => PositionImportedFrame();
                Root.SizeChanged += (_, _) => PositionImportedFrame();
                PositionImportedFrame();
            }
            PositionEditorHud();
        };
        // Start after the first displayed frame, not while loading the full-size bitmap.
        ContentRendered += async (_, _) =>
        {
            Motion.To(HudBar, EdgeSurface.RevealProperty, 1, Motion.Enter, from: 0);
            Motion.To(HudActions, OpacityProperty, 1, 200, false, 0, HandoffHint.Visibility == Visibility.Visible ? 80 : 0);
            Motion.To(HandoffHint, OpacityProperty, 0, 140, false);
            var buttons = ToolPanel.Children.OfType<FrameworkElement>().Concat(HudActions.Children.OfType<System.Windows.Controls.Button>());
            var index = 0;
            foreach (var button in buttons) Motion.Reveal(button, index++ * 12);
            Motion.Reveal(StatusPanel, 50, -5);
            Motion.To(SelectionFrame, OpacityProperty, 1, 250, false, .2);
            if (Motion.Enabled) await Task.Delay(180);
            HandoffHint.Visibility = Visibility.Collapsed;
        };
        if (desktop is null || region is null) return;
        if (region.Value.Width < 1 || region.Value.Height < 1)
        {
            HudBar.CollapsedWidth = _hudPreferences.IsVertical ? 60 : 350;
            HudBar.CollapsedHeight = _hudPreferences.IsVertical ? 200 : 60;
            HandoffHint.Visibility = Visibility.Visible;
            HudActions.Opacity = 0;
        }
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;
        Topmost = true;
        MinWidth = MinHeight = 0;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        DesktopImage.Source = desktop;
        DesktopImage.Visibility = DesktopDim.Visibility = InlineStage.Visibility = Visibility.Visible;
        EditorStage.Children.Remove(ImageView);
        InlineStage.Children.Add(ImageView);
        EditorStage.Visibility = Visibility.Collapsed;
    }

    private void PositionFrame(Rect bounds)
    {
        Canvas.SetLeft(SelectionFrame, bounds.X); Canvas.SetTop(SelectionFrame, bounds.Y);
        SelectionFrame.Width = bounds.Width; SelectionFrame.Height = bounds.Height;
    }

    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds)
    {
        Motion.To(target, property, to, milliseconds, from: from);
    }

    private static void Animate(FrameworkElement target, DependencyProperty property, double from, double to, int milliseconds)
    {
        Motion.To(target, property, to, milliseconds, from: from);
    }

    private void PaletteButton_Click(object sender, RoutedEventArgs e) => SetPaletteOpen(!_paletteOpen);

    private void SetPaletteOpen(bool open)
    {
        PositionEditorHud();
        _paletteOpen = open;
        if (!open && PaletteContent.IsKeyboardFocusWithin) PaletteButton.Focus();
        PaletteClip.IsHitTestVisible = open;
        PaletteContent.IsEnabled = open;
        Motion.To(PaletteClip, EdgeSurface.RevealProperty, open ? 1 : 0, open ? Motion.Enter : Motion.Exit, open);
        Motion.To(PaletteContent, OpacityProperty, open ? 1 : 0, open ? 180 : 120, false);
        if (open)
        {
            var index = 0;
            foreach (FrameworkElement row in PaletteContent.Children) Motion.Reveal(row, index++ * 18, 9);
        }
    }

    private void CloseEditor_Click(object sender, RoutedEventArgs e) { if (!_busy) Close(); }

    private void StrokeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ColorDot is null || StrokeValue is null) return;
        UpdateColorDot();
    }

    private void UpdateColorDot()
    {
        var diameter = 6 + StrokeSlider.Value;
        Animate(ColorDot, WidthProperty, ColorDot.ActualWidth > 0 ? ColorDot.ActualWidth : 10, diameter, 140);
        Animate(ColorDot, HeightProperty, ColorDot.ActualHeight > 0 ? ColorDot.ActualHeight : 10, diameter, 140);
        StrokeValue.Text = $"{StrokeSlider.Value:0} px";
        if (ColorDot.Fill is SolidColorBrush previous && !previous.IsFrozen && SystemParameters.ClientAreaAnimation)
            previous.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_currentColor, TimeSpan.FromMilliseconds(100)));
        else ColorDot.Fill = new SolidColorBrush(_currentColor);
    }

    private void HueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingColor || HueSurface is null || HexText is null || StrokeSlider is null) return;
        _hue = e.NewValue;
        ApplyHsv();
    }

    private void Sv_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SvField.CaptureMouse();
        PickColor(e.GetPosition(SvField));
        e.Handled = true;
    }
    private void Sv_MouseMove(object sender, MouseEventArgs e)
    {
        if (SvField.IsMouseCaptured) PickColor(e.GetPosition(SvField));
    }
    private void Sv_MouseUp(object sender, MouseButtonEventArgs e) => SvField.ReleaseMouseCapture();
    private void PickColor(Point position)
    {
        _saturation = Math.Clamp(position.X / Math.Max(1, SvField.ActualWidth), 0, 1);
        _value = 1 - Math.Clamp(position.Y / Math.Max(1, SvField.ActualHeight), 0, 1);
        ApplyHsv();
    }
    private static Color Hsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    private void ApplyHsv()
    {
        _currentColor = Hsv(_hue, _saturation, _value);
        HueSurface.Background = new SolidColorBrush(Hsv(_hue, 1, 1));
        HexText.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
        Canvas.SetLeft(SvMarker, _saturation * SvField.ActualWidth - 6);
        Canvas.SetTop(SvMarker, (1 - _value) * SvField.ActualHeight - 6);
        UpdateColorDot();
    }
    private void SetPaletteColor(Color color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        _hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        _hue = (_hue + 360) % 360;
        _saturation = max == 0 ? 0 : delta / max;
        _value = max;
        _updatingColor = true;
        HueSlider.Value = _hue;
        _updatingColor = false;
        ApplyHsv();
    }
    private void HexCommitted(object sender, RoutedEventArgs e)
    {
        var input = HexText.Text.Trim().TrimStart('#');
        if (input.Length == 6 && uint.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            SetPaletteColor(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        else { StatusText.Text = "Введите цвет в формате #RRGGBB"; ApplyHsv(); }
    }
    private void HexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        HexCommitted(sender, e);
        e.Handled = true;
    }
}
