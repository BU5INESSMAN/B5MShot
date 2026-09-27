using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using B5MShot.App.Services;
using B5MShot.App.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, 960, 540));
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(120, 100, 720, 340));
            }
            var fixture = new RenderTargetBitmap(960, 540, 96, 96, PixelFormats.Pbgra32);
            fixture.Render(visual); fixture.Freeze();
            var capture = new CaptureService();
            var crop = capture.Crop(fixture, new Rect(60, 50, 360, 170), 480, 270);
            Check(crop.PixelWidth == 720 && crop.PixelHeight == 340, "DPI-scaled crop preserves pixel dimensions");
            var corner = capture.Crop(fixture, new Rect(479, 269, 20, 20), 480, 270);
            Check(corner.PixelWidth == 2 && corner.PixelHeight == 2, "Crop clamps at screen edge");
            var window = new PreviewWindow(fixture, new UploadService());
            window.Show(); Pump(350);
            if (args.Contains("--interactive"))
            {
                window.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                Dispatcher.Run(); return 0;
            }
            var initial = (BitmapSource)Call(window, "RenderFinalImage")!;
            Check(PixelsEqual(fixture, initial), "Export contains original image only, no HUD");
            Call(window, "SetPaletteOpen", true); Pump(400);
            var clip = (Border)window.FindName("PaletteClip");
            var bar = (Border)window.FindName("HudBar");
            Check(clip.ActualWidth < bar.ActualWidth && clip.ActualHeight > 250, "Palette opens narrow and fully visible");
            Check(PixelsEqual(fixture, (BitmapSource)Call(window, "RenderFinalImage")!), "Open palette never enters export");
            var hex = (TextBox)window.FindName("HexText");
            hex.Text = "#12AB34"; Call(window, "HexCommitted", hex, new RoutedEventArgs()); Pump(160);
            var dot = (System.Windows.Shapes.Ellipse)window.FindName("ColorDot");
            var visibleColor = ((SolidColorBrush)dot.Fill).Color;
            Check(visibleColor.R == 18 && visibleColor.G == 171 && visibleColor.B == 52, $"HEX and visible color match ({visibleColor})");
            var slider = (Slider)window.FindName("StrokeSlider"); slider.Value = 20; Pump(220);
            Check(Math.Abs(dot.ActualWidth - 26) < .1, "One button displays thickness through diameter");
            var tool = ((StackPanel)window.FindName("ToolPanel")).Children.OfType<ToggleButton>().Single(b => (string)b.Tag == "Arrow");
            Call(window, "ToolButton_Click", tool, new RoutedEventArgs()); Pump(250);
            Check(tool.IsChecked == true, "Tool switch updates selection");
            Call(window, "SetPaletteOpen", false); Pump(280);
            Check(clip.ActualHeight < .1 && !clip.IsHitTestVisible && !((StackPanel)window.FindName("PaletteContent")).IsEnabled, "Closed palette cannot intercept input");
            window.Close();
            var inline = new PreviewWindow(crop, new UploadService(), fixture, new Rect(.125, .185185, .75, .62963));
            inline.Show(); Pump(350);
            Check(inline.WindowStyle == WindowStyle.None && !inline.ShowInTaskbar, "Inline editor uses desktop overlay");
            Check(PixelsEqual(crop, (BitmapSource)Call(inline, "RenderFinalImage")!), "Inline export preserves crop without desktop or HUD");
            inline.Close();
            var times = new List<double>();
            for (var i = 0; i < 12; i++)
            {
                var watch = Stopwatch.StartNew();
                var image = Task.Run(capture.CaptureVirtualScreen).GetAwaiter().GetResult();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Check(image.IsFrozen && image.PixelWidth > 0, "Background capture is frozen and valid", quiet: true);
            }
            times.Sort();
            Console.WriteLine($"Capture only: median={times[6]:F1} ms, p95={times[11]:F1} ms (12 samples; not hotkey-to-display latency)");
            Console.WriteLine("PASS: smoke suite");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close(); }
    }
    private static object? Call(object instance, string name, params object[] arguments) => instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, arguments);
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static bool PixelsEqual(BitmapSource left, BitmapSource right)
    {
        if (left.PixelWidth != right.PixelWidth || left.PixelHeight != right.PixelHeight) return false;
        var a = new byte[left.PixelWidth * left.PixelHeight * 4]; var b = new byte[a.Length];
        left.CopyPixels(a, left.PixelWidth * 4, 0); right.CopyPixels(b, right.PixelWidth * 4, 0);
        return a.SequenceEqual(b);
    }
    private static void Check(bool result, string message, bool quiet = false)
    {
        if (!result) throw new InvalidOperationException("FAIL: " + message);
        if (!quiet) Console.WriteLine("PASS: " + message);
    }

}
