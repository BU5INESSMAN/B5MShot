using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using B5MShot.App.Services;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Orientation = System.Windows.Controls.Orientation;

namespace B5MShot.App.Views;

public sealed class ResultHudWindow : Window
{
    public ResultHudWindow(string message, string? url)
    {
        Width = 390; Height = url is null ? 58 : 104;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        var content = new StackPanel { Margin = new Thickness(20, 12, 20, 14) };
        var status = new TextBlock { Text = message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        content.Children.Add(status);
        if (url is not null)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            var open = new Button { Content = "Открыть", Margin = new Thickness(4, 0, 4, 0) };
            var copy = new Button { Content = "Копировать", Margin = new Thickness(4, 0, 4, 0) };
            open.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex) { ErrorLogService.Write(ex, "Opening result"); status.Text = "Не удалось открыть браузер"; }
            };
            copy.Click += async (_, _) =>
            {
                try { await ClipboardService.SetTextAsync(url); status.Text = "Ссылка скопирована"; }
                catch { status.Text = "Буфер занят — попробуйте ещё раз"; }
            };
            actions.Children.Add(open); actions.Children.Add(copy); content.Children.Add(actions);
        }
        var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(8, 10, 14)), CornerRadius = new CornerRadius(0, 0, 24, 24), Child = content };
        Content = surface;
        Loaded += (_, _) =>
        {
            var monitor = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = (monitor.Left + (monitor.Width - Width * dpi.DpiScaleX) / 2) / dpi.DpiScaleX;
            Top = monitor.Top / dpi.DpiScaleY;
            if (SystemParameters.ClientAreaAnimation)
            {
                surface.RenderTransformOrigin = new Point(.5, 0);
                var scale = new ScaleTransform(1, 1);
                surface.RenderTransform = scale;
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.1, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => { if (!IsMouseOver) Close(); };
        Closed += (_, _) => timer.Stop();
        timer.Start();
    }
}
