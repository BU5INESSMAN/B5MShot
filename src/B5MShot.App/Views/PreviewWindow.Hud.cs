using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using B5MShot.App.Services;
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
    private double _hue = 354, _saturation = .688, _value = .929;

    private void ConfigureHud(BitmapSource? desktop, Rect? region)
    {
        void ApplyTheme()
        {
            if (desktop is not null) return;
            var light = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not int theme || theme != 0;
            Background = new SolidColorBrush(light ? Color.FromRgb(233, 239, 246) : Color.FromRgb(22, 31, 48));
        }
        ApplyTheme();
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
            HudBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            HudHost.Width = HudBar.DesiredSize.Width;
            HudBar.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            if (desktop is not null && region is { } selected)
            {
                ScreenPlacement.CoverDesktop(this);
                void PositionImage()
                {
                    ImageView.Width = selected.Width * Root.ActualWidth;
                    ImageView.Height = selected.Height * Root.ActualHeight;
                    Canvas.SetLeft(ImageView, selected.X * Root.ActualWidth);
                    Canvas.SetTop(ImageView, selected.Y * Root.ActualHeight);
                    ScreenPlacement.PositionHud(HudHost, this);
                }
                SizeChanged += (_, _) => PositionImage();
                PositionImage();
            }
            // Animate only the chrome; the image and input surface are ready immediately.
            Animate(HudBar, WidthProperty, 260, HudBar.DesiredSize.Width, 260);
        };
        if (desktop is null || region is null) return;
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

    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation) { target.SetValue(property, to); return; }
        target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private static void Animate(FrameworkElement target, DependencyProperty property, double from, double to, int milliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation) { target.SetValue(property, to); return; }
        target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void PaletteButton_Click(object sender, RoutedEventArgs e) => SetPaletteOpen(!_paletteOpen);

    private void SetPaletteOpen(bool open)
    {
        _paletteOpen = open;
        PaletteClip.IsHitTestVisible = open;
        PaletteContent.IsEnabled = open;
        PaletteContent.Measure(new Size(330, double.PositiveInfinity));
        Animate(PaletteClip, HeightProperty, PaletteClip.ActualHeight, open ? PaletteContent.DesiredSize.Height : 0, open ? 320 : 230);
        Animate(PaletteContent, OpacityProperty, PaletteContent.Opacity, open ? 1 : 0, 180);
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
